
using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace CQIng.Revit.ColumnasVigasMuros.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class InspectColumns : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var columns = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            string log = "Column Report:\n";
            foreach(var col in columns)
            {
                var bb = col.get_BoundingBox(null);
                double zMin = bb != null ? bb.Min.Z : 0;
                double zMax = bb != null ? bb.Max.Z : 0;
                double height = zMax - zMin;

                var baseLevel = doc.GetElement(col.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).AsElementId()) as Level;
                var topLevel = doc.GetElement(col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).AsElementId()) as Level;
                double baseOffset = col.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM).AsDouble();
                double topOffset = col.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM).AsDouble();

                log += $"Col ID: {col.Id}\n";
                log += $"BB ZMin: {zMin * 0.3048}m, BB ZMax: {zMax * 0.3048}m\n";
                log += $"BB Height: {height * 0.3048}m\n";
                log += $"Base Level: {baseLevel?.Name}, Elev: {baseLevel?.Elevation * 0.3048}m\n";
                log += $"Top Level: {topLevel?.Name}, Elev: {topLevel?.Elevation * 0.3048}m\n";
                log += $"Base Offset: {baseOffset * 0.3048}m, Top Offset: {topOffset * 0.3048}m\n";
                log += $"Expected Level Height: {((topLevel?.Elevation ?? 0) + topOffset - ((baseLevel?.Elevation ?? 0) + baseOffset)) * 0.3048}m\n";
                log += "--------------------------\n";
            }
            File.WriteAllText(@"D:\Addins\CQIng.Revit.ColumnasVigasMuros\scratch\column_report.txt", log);
            return Result.Succeeded;
        }
    }
}

