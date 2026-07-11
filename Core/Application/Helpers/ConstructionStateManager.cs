using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public class WallEndState
{
    public Wall Wall { get; set; }
    public bool IsStart { get; set; }
    public XYZ CurrentPoint { get; set; }
    
    // Si ya existe una columneta en este extremo
    public bool HasExistingColumneta { get; set; }
    public FamilyInstance ExistingColumneta { get; set; }
    
    // Punto virtual original (pre-recorte) calculado
    public XYZ VirtualCornerPoint { get; set; }
}

public static class ConstructionStateManager
{
    /// <summary>
    /// Analiza los extremos de los muros y detecta si ya existen columnetas o viguetas.
    /// También proyecta el punto de esquina virtual si el muro ya fue recortado.
    /// </summary>
    public static Dictionary<string, WallEndState> AnalyzeModelState(Document doc, List<Wall> walls)
    {
        var states = new Dictionary<string, WallEndState>();
        
        foreach(var wall in walls)
        {
            var lc = wall.Location as LocationCurve;
            if (lc == null) continue;
            
            XYZ p0 = lc.Curve.GetEndPoint(0);
            XYZ p1 = lc.Curve.GetEndPoint(1);
            
            AnalyzeEnd(doc, wall, p0, true, states);
            AnalyzeEnd(doc, wall, p1, false, states);
        }
        
        return states;
    }
    
    private static void AnalyzeEnd(Document doc, Wall wall, XYZ pt, bool isStart, Dictionary<string, WallEndState> states)
    {
        string key = $"{wall.Id}_{(isStart ? "Start" : "End")}";
        
        var state = new WallEndState
        {
            Wall = wall,
            IsStart = isStart,
            CurrentPoint = pt,
            VirtualCornerPoint = pt // Por defecto, asume que no está recortado
        };
        
        // Buscar elementos cercanos al extremo
        var (zMin, zMax, baseLevel) = WallGeometryHelper.GetWallElevationInfo(doc, wall);
        XYZ searchCenter = new XYZ(pt.X, pt.Y, (zMin + zMax) / 2.0);
        
        // Tolerance box 0.5 metros
        double tol = 0.5 / 0.3048; 
        BoundingBoxXYZ bbox = new BoundingBoxXYZ
        {
            Min = searchCenter - new XYZ(tol, tol, (zMax - zMin)/2.0 + tol),
            Max = searchCenter + new XYZ(tol, tol, (zMax - zMin)/2.0 + tol)
        };
        
        Outline outline = new Outline(bbox.Min, bbox.Max);
        BoundingBoxIntersectsFilter bboxFilter = new BoundingBoxIntersectsFilter(outline);
        
        // Columnetas
        var columns = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsNotElementType()
            .WherePasses(bboxFilter)
            .Cast<FamilyInstance>()
            .ToList();
            
        if (columns.Any())
        {
            state.HasExistingColumneta = true;
            state.ExistingColumneta = columns.First();
            
            // Si hay columneta, el muro ya fue recortado. El punto virtual de esquina
            // debería ser el centro de la columneta (aproximación para no recalcular cortes complejos)
            var colLc = state.ExistingColumneta.Location as LocationPoint;
            if (colLc != null)
            {
                state.VirtualCornerPoint = new XYZ(colLc.Point.X, colLc.Point.Y, pt.Z);
            }
        }
        
        states[key] = state;
    }
}
