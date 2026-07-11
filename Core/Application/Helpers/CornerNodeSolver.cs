using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public class NodeWall
{
    public Wall Wall { get; set; }
    public bool IsStart { get; set; }
    public XYZ InwardDir { get; set; }
    public double Thickness => Wall.Width;
    public bool IsContinuous { get; set; }
}

public class CornerNode
{
    public XYZ Point { get; set; }
    public List<NodeWall> ConnectedWalls { get; set; } = new List<NodeWall>();
    public NodeWall PrimaryWall { get; set; }
    
    public XYZ TargetCenter { get; set; }
    public double RotationAngle { get; set; }
    public Dictionary<ElementId, double> WallCutLengths { get; set; } = new Dictionary<ElementId, double>();
    
    public enum NodeType
    {
        End,
        L,
        T,
        Cross,
        Unknown
    }
    public NodeType Type { get; set; }
}

public static class CornerNodeSolver
{
    public static List<CornerNode> BuildTopologicalNodes(Document doc, List<Wall> selectedWalls, Dictionary<string, WallEndState> endStates)
    {
        var nodeDict = new Dictionary<string, CornerNode>();

        foreach (var wall in selectedWalls)
        {
            LocationCurve lc = wall.Location as LocationCurve;
            if (lc == null) continue;

            XYZ p0 = lc.Curve.GetEndPoint(0);
            XYZ p1 = lc.Curve.GetEndPoint(1);

            XYZ p0Virtual = GetVirtualPoint(wall.Id, true, endStates, p0);
            XYZ p1Virtual = GetVirtualPoint(wall.Id, false, endStates, p1);

            AddWallToDict(nodeDict, p0Virtual, wall, true);
            AddWallToDict(nodeDict, p1Virtual, wall, false);
            
            // Detectar muros conectados no seleccionados
            AddJoinedWalls(nodeDict, p0Virtual, lc, 0, true, endStates);
            AddJoinedWalls(nodeDict, p1Virtual, lc, 1, false, endStates);
        }

        return nodeDict.Values.ToList();
    }

    private static XYZ GetVirtualPoint(ElementId wallId, bool isStart, Dictionary<string, WallEndState> endStates, XYZ defaultPt)
    {
        string key = $"{wallId}_{(isStart ? "Start" : "End")}";
        if (endStates != null && endStates.TryGetValue(key, out var state))
        {
            return state.VirtualCornerPoint;
        }
        return defaultPt;
    }

    private static void AddJoinedWalls(Dictionary<string, CornerNode> dict, XYZ ptVirtual, LocationCurve lc, int end, bool isStart, Dictionary<string, WallEndState> endStates)
    {
        ElementArray joined = lc.get_ElementsAtJoin(end);
        if (joined != null)
        {
            foreach (Element e in joined)
            {
                if (e is Wall jw)
                {
                    // Determinar si el muro unido llega por su inicio o fin
                    LocationCurve lcJw = jw.Location as LocationCurve;
                    if (lcJw != null)
                    {
                        double dist0 = lcJw.Curve.GetEndPoint(0).DistanceTo(ptVirtual);
                        double dist1 = lcJw.Curve.GetEndPoint(1).DistanceTo(ptVirtual);
                        bool isContinuous = dist0 > 0.5 && dist1 > 0.5; // Si ambos extremos están lejos del nodo, es continuo
                        bool jwIsStart = dist0 < dist1;
                        
                        // We use the virtual point of the main wall to group them
                        AddWallToDict(dict, ptVirtual, jw, jwIsStart, isContinuous);
                    }
                }
            }
        }
    }

    private static void AddWallToDict(Dictionary<string, CornerNode> dict, XYZ pt, Wall wall, bool isStart, bool isContinuous = false)
    {
        string key = $"{Math.Round(pt.X, 3)}_{Math.Round(pt.Y, 3)}";
        if (!dict.ContainsKey(key))
        {
            dict[key] = new CornerNode { Point = pt };
        }
        
        // Evitar duplicados
        if (dict[key].ConnectedWalls.Any(nw => nw.Wall.Id == wall.Id)) return;
        
        LocationCurve lc = wall.Location as LocationCurve;
        XYZ p0 = lc.Curve.GetEndPoint(0);
        XYZ p1 = lc.Curve.GetEndPoint(1);
        XYZ inwardDir = isStart ? (p1 - p0).Normalize() : (p0 - p1).Normalize();

        dict[key].ConnectedWalls.Add(new NodeWall 
        { 
            Wall = wall, 
            IsStart = isStart,
            InwardDir = inwardDir,
            IsContinuous = isContinuous
        });
    }

    public static void SolveNodeGeometry(CornerNode node, FamilySymbol columnType, double baseElevation)
    {
        // 1. Analizar la topología y definir el muro principal
        NodeTopologyAnalyzer.AnalyzeNodeTopology(node);
        
        double w = 0.30 / 0.3048; 
        double colTransversal = 0; // Espesor transversal real de la columneta
        BoundingBoxXYZ bb = columnType.get_BoundingBox(null);
        if (bb != null)
        {
            double sizeX = bb.Max.X - bb.Min.X;
            double sizeY = bb.Max.Y - bb.Min.Y;
            w = Math.Max(sizeX, sizeY); // El lado longitudinal es el mayor
            colTransversal = Math.Min(sizeX, sizeY); // El lado transversal es el menor
        }
        double t = node.PrimaryWall.Thickness;
        // Extensión transversal efectiva: si la columneta es más gruesa que el muro,
        // usar su espesor real para que las esquinas se alineen con los tramos.
        double tEffective = Math.Max(t, colTransversal);

        // 3. Ejes locales de la columneta
        XYZ localX = node.PrimaryWall.InwardDir;
        XYZ localY = XYZ.BasisZ.CrossProduct(localX).Normalize();

        node.RotationAngle = WallConfinementCalculator.GetColumnetaRotationAngle(localX, columnType);

        // 4. Analizar la topología para determinar X_min y X_max del BoundingBox local
        bool hasOppositeWall = false;
        double maxPerpendicularThickness = 0;

        foreach (var nw in node.ConnectedWalls)
        {
            if (nw.Wall.Id == node.PrimaryWall.Wall.Id) continue;

            double dotX = nw.InwardDir.DotProduct(localX);
            
            if (dotX < -0.9) // Muro colineal opuesto (T-Junction continua)
            {
                hasOppositeWall = true;
            }
            else if (Math.Abs(dotX) < 0.1) // Muro perpendicular
            {
                if (nw.Thickness > maxPerpendicularThickness)
                    maxPerpendicularThickness = nw.Thickness;
            }
        }

        double xMin, xMax;
        if (hasOppositeWall)
        {
            xMin = -w / 2.0;
            xMax = w / 2.0;
        }
        else
        {
            xMin = -maxPerpendicularThickness / 2.0;
            xMax = xMin + w;
        }

        // El centro geométrico local de la columneta
        double localCenterX = (xMin + xMax) / 2.0;
        double localCenterY = 0; // Siempre centrado en el espesor del muro principal

        // Convertir el centro local a coordenadas mundiales
        node.TargetCenter = node.Point + localX * localCenterX + localY * localCenterY;
        node.TargetCenter = new XYZ(node.TargetCenter.X, node.TargetCenter.Y, baseElevation);

        // 5. Calcular los recortes para cada muro basándonos en la intersección con el BoundingBox
        foreach (var nw in node.ConnectedWalls)
        {
            if (nw.IsContinuous)
            {
                node.WallCutLengths[nw.Wall.Id] = 0.0;
                continue;
            }

            double dotX = nw.InwardDir.DotProduct(localX);
            double dotY = nw.InwardDir.DotProduct(localY);
            
            double cutLength = 0;

            if (dotX > 0.9) // Muro principal (si no fuera continuo)
            {
                cutLength = xMax; 
            }
            else if (dotX < -0.9) // Muro colineal opuesto
            {
                cutLength = -xMin; 
            }
            else // Muros perpendiculares (dotY > 0.9 o dotY < -0.9)
            {
                cutLength = tEffective / 2.0;
            }

            node.WallCutLengths[nw.Wall.Id] = cutLength;
        }
    }
}
