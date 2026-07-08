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
}
