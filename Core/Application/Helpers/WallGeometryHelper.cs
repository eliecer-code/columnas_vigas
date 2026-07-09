using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public static class WallGeometryHelper
{
    public static (double ZMin, double ZMax, Level BaseLevel) GetWallElevationInfo(Document doc, Wall wall)
    {
        var (zMin, zMax) = RealGeometryHelper.GetSolidElevation(wall, doc);

        Level baseLevel = null;
        Parameter baseConstraint = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
        if (baseConstraint != null && baseConstraint.AsElementId() != ElementId.InvalidElementId)
        {
            baseLevel = doc.GetElement(baseConstraint.AsElementId()) as Level;
        }

        // If for some reason there's no base level, find the closest one
        if (baseLevel == null)
        {
            baseLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - zMin))
                .FirstOrDefault();
        }

        return (zMin, zMax, baseLevel);
    }

    public static XYZ GetExtendedEndpoint(Wall wall, int end)
    {
        LocationCurve lc = wall.Location as LocationCurve;
        if (lc == null || !(lc.Curve is Line line1)) return null;

        ElementArray joined = lc.get_ElementsAtJoin(end);
        if (joined == null || joined.IsEmpty) return null;

        XYZ p1 = line1.GetEndPoint(0);
        XYZ p2 = line1.GetEndPoint(1);

        foreach (Element e in joined)
        {
            if (e is Wall jw && jw.Id != wall.Id)
            {
                LocationCurve lcJw = jw.Location as LocationCurve;
                if (lcJw != null && lcJw.Curve is Line line2)
                {
                    XYZ p3 = line2.GetEndPoint(0);
                    XYZ p4 = line2.GetEndPoint(1);

                    double denom = (p1.X - p2.X) * (p3.Y - p4.Y) - (p1.Y - p2.Y) * (p3.X - p4.X);
                    if (Math.Abs(denom) > 1e-6) // No son paralelos
                    {
                        double t = ((p1.X - p3.X) * (p3.Y - p4.Y) - (p1.Y - p3.Y) * (p3.X - p4.X)) / denom;
                        
                        double x = p1.X + t * (p2.X - p1.X);
                        double y = p1.Y + t * (p2.Y - p1.Y);
                        
                        return new XYZ(x, y, p1.Z);
                    }
                }
            }
        }
        return null;
    }
}
