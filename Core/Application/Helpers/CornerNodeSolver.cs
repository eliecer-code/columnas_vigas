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
}

public class CornerNode
{
    public XYZ Point { get; set; }
    public List<NodeWall> ConnectedWalls { get; set; } = new List<NodeWall>();
    public NodeWall PrimaryWall { get; set; }
    
    public XYZ TargetCenter { get; set; }
    public double RotationAngle { get; set; }
    public Dictionary<ElementId, double> WallCutLengths { get; set; } = new Dictionary<ElementId, double>();
}

public static class CornerNodeSolver
{
    public static List<CornerNode> BuildTopologicalNodes(Document doc, List<Wall> selectedWalls)
    {
        var nodeDict = new Dictionary<string, CornerNode>();

        foreach (var wall in selectedWalls)
        {
            LocationCurve lc = wall.Location as LocationCurve;
            if (lc == null) continue;

            XYZ p0 = lc.Curve.GetEndPoint(0);
            XYZ p1 = lc.Curve.GetEndPoint(1);

            AddWallToDict(nodeDict, p0, wall, true);
            AddWallToDict(nodeDict, p1, wall, false);
            
            // Detectar muros conectados no seleccionados
            AddJoinedWalls(nodeDict, p0, lc, 0, true);
            AddJoinedWalls(nodeDict, p1, lc, 1, false);
        }

        return nodeDict.Values.ToList();
    }

    private static void AddJoinedWalls(Dictionary<string, CornerNode> dict, XYZ pt, LocationCurve lc, int end, bool isStart)
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
                        double dist0 = lcJw.Curve.GetEndPoint(0).DistanceTo(pt);
                        double dist1 = lcJw.Curve.GetEndPoint(1).DistanceTo(pt);
                        bool jwIsStart = dist0 < dist1;
                        AddWallToDict(dict, pt, jw, jwIsStart);
                    }
                }
            }
        }
    }

    private static void AddWallToDict(Dictionary<string, CornerNode> dict, XYZ pt, Wall wall, bool isStart)
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
            InwardDir = inwardDir
        });
    }

    public static void SolveNodeGeometry(CornerNode node, FamilySymbol columnType, double baseElevation)
    {
        // 1. Determinar el muro principal (el más grueso o el más largo en caso de empate)
        node.PrimaryWall = node.ConnectedWalls.OrderByDescending(w => w.Thickness).ThenByDescending(w => (w.Wall.Location as LocationCurve).Curve.Length).First();
        
        // 2. Obtener dimensiones de la columneta
        double w = 0.30 / 0.3048; 
        Parameter bParam = columnType.LookupParameter("b");
        if (bParam != null) w = bParam.AsDouble();
        else
        {
            Parameter hParam = columnType.LookupParameter("h");
            if (hParam != null) w = hParam.AsDouble();
            else
            {
                BoundingBoxXYZ bb = columnType.get_BoundingBox(null);
                if (bb != null) w = bb.Max.X - bb.Min.X;
            }
        }
        double t = node.PrimaryWall.Thickness; // Espesor SIEMPRE igual al muro principal

        // 3. Ejes locales de la columneta
        XYZ localX = node.PrimaryWall.InwardDir;
        XYZ localY = XYZ.BasisZ.CrossProduct(localX).Normalize();

        node.RotationAngle = XYZ.BasisX.AngleTo(localX);
        if (localX.Y < 0) node.RotationAngle = -node.RotationAngle;

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
            double dotX = nw.InwardDir.DotProduct(localX);
            double dotY = nw.InwardDir.DotProduct(localY);
            
            double cutLength = 0;

            if (dotX > 0.9) // Muro principal
            {
                cutLength = xMax; 
            }
            else if (dotX < -0.9) // Muro colineal opuesto
            {
                cutLength = -xMin; 
            }
            else // Muros perpendiculares (dotY > 0.9 o dotY < -0.9)
            {
                cutLength = t / 2.0;
            }

            node.WallCutLengths[nw.Wall.Id] = cutLength;
        }
    }
}
