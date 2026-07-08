using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public static class WallGeometryCalculator
{
    public static double GetRealLength(Wall wall)
    {
        if (wall.Location is LocationCurve lc)
            return lc.Curve.Length;
        return 0;
    }

    public static (double ZMin, double ZMax, Level BaseLevel) GetElevationInfo(Document doc, Wall wall)
    {
        return WallGeometryHelper.GetWallElevationInfo(doc, wall);
    }

    public static double GetThickness(Wall wall)
    {
        return wall.Width;
    }

    public static Line GetAxis(Wall wall)
    {
        if (wall.Location is LocationCurve lc && lc.Curve is Line line)
            return line;
        return null;
    }

    public static XYZ GetStartPoint(Wall wall)
    {
        if (wall.Location is LocationCurve lc)
            return lc.Curve.GetEndPoint(0);
        return null;
    }

    public static XYZ GetEndPoint(Wall wall)
    {
        if (wall.Location is LocationCurve lc)
            return lc.Curve.GetEndPoint(1);
        return null;
    }

    public static double GetColumnetaWidth(Document doc, FamilySymbol symbol)
    {
        Parameter bParam = symbol.LookupParameter("b");
        if (bParam != null) return bParam.AsDouble();
        
        Parameter hParam = symbol.LookupParameter("h");
        if (hParam != null) return hParam.AsDouble();

        BoundingBoxXYZ bb = symbol.get_BoundingBox(null);
        if (bb != null)
        {
            return bb.Max.X - bb.Min.X;
        }
        
        return 0.30 / 0.3048; 
    }

    public static XYZ CalculateColumnetaPosition(Wall primaryWall, XYZ nodePoint, double columnWidth)
    {
        XYZ p0 = GetStartPoint(primaryWall);
        XYZ p1 = GetEndPoint(primaryWall);
        
        // El vector director apunta desde nodePoint hacia el interior del muro
        XYZ inwardDir = (nodePoint.DistanceTo(p0) < 0.01) ? (p1 - p0).Normalize() : (p0 - p1).Normalize();
        XYZ normalDir = XYZ.BasisZ.CrossProduct(inwardDir).Normalize();
        
        // 1. Desplazamiento Longitudinal (Adentro del muro)
        // Movemos el centro geométrico de la columneta exactamente la mitad de su ancho
        // para que su cara exterior caiga de forma milimétrica en el nodePoint original.
        XYZ longitudinalShift = inwardDir * (columnWidth / 2.0);
        
        // 2. Desplazamiento Lateral (Centrado en el núcleo real)
        // Para que las caras laterales no sobresalgan, deben coincidir con las caras del Sólido real del muro.
        XYZ wallLeft = RealGeometryHelper.GetExtremePoint(primaryWall, primaryWall.Document, normalDir);
        XYZ wallRight = RealGeometryHelper.GetExtremePoint(primaryWall, primaryWall.Document, -normalDir);
        
        XYZ lateralShift = XYZ.Zero;
        if (wallLeft != null && wallRight != null)
        {
            // Centro geométrico real del muro
            XYZ wallCenter = (wallLeft + wallRight) / 2.0;
            
            // La diferencia entre el nodePoint y el centro lateral del muro
            double lateralDist = (wallCenter - nodePoint).DotProduct(normalDir);
            lateralShift = normalDir * lateralDist;
        }

        // La nueva coordenada XYZ absoluta de inserción es:
        // Origen + Desplazamiento Longitudinal + Desplazamiento Lateral
        return nodePoint + longitudinalShift + lateralShift;
    }
}
