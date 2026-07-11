using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

namespace CQIng.Revit.ColumnasVigasMuros.Services;

public static class StructuralExecutionService
{
    private static void LogStep(string step)
    {
        try
        {
            string path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "CQIng_Diagnostic.log"
            );
            System.IO.File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [EXECUTION] {step}\n");
        }
        catch { }
    }

    public static void ExecutePlan(Document doc, StructuralPlan plan, GenerationOptions genOptions)
    {
        LogStep("Iniciando fase de ejecución de plan...");
        
        List<FamilyInstance> createdColumns = new List<FamilyInstance>();
        List<FamilyInstance> createdFramings = new List<FamilyInstance>();

        if (genOptions.GenerateColumns)
        {
            LogStep($"Ejecutando recortes de muro ({plan.WallCuts.Count} recortes).");
            foreach (var cut in plan.WallCuts)
            {
                LocationCurve lcW = cut.Wall.Location as LocationCurve;
                XYZ p0 = lcW.Curve.GetEndPoint(0);
                XYZ p1 = lcW.Curve.GetEndPoint(1);
                
                if (cut.EndIndex == 0) p0 = cut.NewEndPoint;
                else p1 = cut.NewEndPoint;

                if (p0.DistanceTo(p1) > 0.5)
                {
                    WallUtils.DisallowWallJoinAtEnd(cut.Wall, cut.EndIndex);
                    lcW.Curve = Line.CreateBound(p0, p1);
                }
            }
            doc.Regenerate();

            LogStep($"Creando columnetas ({plan.Columns.Count}).");
            foreach (var pCol in plan.Columns)
            {
                FamilySymbol specificColType = FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(doc, pCol.ColumnType, pCol.PrimaryWall.Width);
                FamilyInstance col = doc.Create.NewFamilyInstance(pCol.InsertionPoint, specificColType, pCol.BaseLevel, Autodesk.Revit.DB.Structure.StructuralType.Column);
                WallConfinementCalculator.ApplyColumnetaConstraints(col, pCol.PrimaryWall);
                doc.Regenerate();
                
                if (Math.Abs(pCol.RotationAngle) > 0.001)
                {
                    Line axis = Line.CreateBound(pCol.InsertionPoint, pCol.InsertionPoint + XYZ.BasisZ);
                    ElementTransformUtils.RotateElement(doc, col.Id, axis, pCol.RotationAngle);
                    doc.Regenerate();
                }
                createdColumns.Add(col);
            }
        }
        else
        {
            LogStep("Generación de columnetas y recortes desactivada.");
        }

        if (genOptions.GenerateTopBeams)
        {
            LogStep($"Creando viguetas superiores ({plan.TopBeams.Count}).");
            foreach (var pBeam in plan.TopBeams)
            {
                FamilySymbol specificFramingType = FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(doc, pBeam.FramingType, pBeam.ParentWall.Width);
                Line beamLine = Line.CreateBound(pBeam.StartPoint, pBeam.EndPoint);
                FamilyInstance topFraming = doc.Create.NewFamilyInstance(beamLine, specificFramingType, pBeam.BaseLevel, Autodesk.Revit.DB.Structure.StructuralType.Beam);

                topFraming.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE)?.Set(pBeam.ZOffset);
                topFraming.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION)?.Set(0.0);
                topFraming.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION)?.Set(0.0);
                topFraming.get_Parameter(BuiltInParameter.Y_JUSTIFICATION)?.Set(1);
                topFraming.get_Parameter(BuiltInParameter.Z_JUSTIFICATION)?.Set(0);

                createdFramings.Add(topFraming);
            }
        }
        else
        {
            LogStep("Generación de viguetas superiores desactivada.");
        }

        if (genOptions.GenerateBottomBeams)
        {
            LogStep($"Creando viguetas inferiores ({plan.BottomBeams.Count}).");
            foreach (var pBeam in plan.BottomBeams)
            {
                FamilySymbol specificFramingType = FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(doc, pBeam.FramingType, pBeam.ParentWall.Width);
                Line beamLine = Line.CreateBound(pBeam.StartPoint, pBeam.EndPoint);
                FamilyInstance botFraming = doc.Create.NewFamilyInstance(beamLine, specificFramingType, pBeam.BaseLevel, Autodesk.Revit.DB.Structure.StructuralType.Beam);

                botFraming.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE)?.Set(0.0);
                botFraming.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION)?.Set(0.0);
                botFraming.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION)?.Set(0.0);
                botFraming.get_Parameter(BuiltInParameter.Y_JUSTIFICATION)?.Set(1);
                botFraming.get_Parameter(BuiltInParameter.Z_JUSTIFICATION)?.Set(0);

                createdFramings.Add(botFraming);
            }
        }
        else
        {
            LogStep("Generación de viguetas inferiores desactivada.");
        }

        doc.Regenerate();

        LogStep("Ejecutando JoinAndCut...");
        foreach (var col in createdColumns)
        {
            var intFramings = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .WherePasses(new ElementIntersectsElementFilter(col))
                .ToList();
            foreach (var frm in intFramings) JoinAndCut(doc, cutter: col, cuttee: frm);

            var intWalls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WherePasses(new ElementIntersectsElementFilter(col))
                .ToList();
            foreach (var w in intWalls) JoinAndCut(doc, cutter: col, cuttee: w);
        }

        foreach (var framing in createdFramings)
        {
            var intWalls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .WherePasses(new ElementIntersectsElementFilter(framing))
                .ToList();
            foreach (var w in intWalls) JoinAndCut(doc, cutter: framing, cuttee: w);
        }
        LogStep("Ejecución del plan completada.");
    }

    private static void JoinAndCut(Document doc, Element cutter, Element cuttee)
    {
        try
        {
            if (!JoinGeometryUtils.AreElementsJoined(doc, cuttee, cutter))
                JoinGeometryUtils.JoinGeometry(doc, cuttee, cutter);

            if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, cutter, cuttee))
                JoinGeometryUtils.SwitchJoinOrder(doc, cuttee, cutter);
        }
        catch { }
    }
}
