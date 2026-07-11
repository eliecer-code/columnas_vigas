using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Services;

public class WarningSwallower : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
        if (fmas.Count == 0)
            return FailureProcessingResult.Continue;

        foreach (FailureMessageAccessor fma in fmas)
        {
            if (fma.GetSeverity() == FailureSeverity.Warning)
            {
                failuresAccessor.DeleteWarning(fma);
            }
        }
        return FailureProcessingResult.Continue;
    }
}

public class ElementGenerationService : IElementGenerationService
{
    private void LogStep(string step)
    {
        try
        {
            string path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "CQIng_Diagnostic.log"
            );
            System.IO.File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [MAIN] {step}\n"
            );
        }
        catch { }
    }

    public void GenerateElements(
        UIApplication uiapp,
        List<WallDataModel> selectedWalls,
        long columnTypeId,
        long framingTypeId,
        GenerationOptions genOptions
    )
    {
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;
        LogStep("Inicio del comando.");

        try
        {
            if (selectedWalls == null || !selectedWalls.Any())
                throw new InvalidOperationException("No se seleccionaron muros.");

            // Early exit if nothing is checked
            if (!genOptions.GenerateColumns && !genOptions.GenerateTopBeams && !genOptions.GenerateBottomBeams)
            {
                TaskDialog.Show("Atención", "Debe seleccionar al menos un elemento para generar.");
                return;
            }

            using (TransactionGroup transGroup = new TransactionGroup(doc, "Generar Columnetas y Viguetas (2-Stage)"))
            {
                transGroup.Start();

                FamilySymbol baseColumnType = doc.GetElement(new ElementId(columnTypeId)) as FamilySymbol;
                if (baseColumnType == null)
                    throw new InvalidOperationException($"El id {columnTypeId} no corresponde a un FamilySymbol válido.");

                if (!baseColumnType.IsActive)
                {
                    using (Transaction tActivate = new Transaction(doc, "Activar Símbolo de Columneta"))
                    {
                        tActivate.Start();
                        baseColumnType.Activate();
                        tActivate.Commit();
                    }
                }

                FamilySymbol baseFramingType = doc.GetElement(new ElementId(framingTypeId)) as FamilySymbol;
                if (baseFramingType != null && !baseFramingType.IsActive)
                {
                    using (Transaction tActivate = new Transaction(doc, "Activar Símbolo de Armazón"))
                    {
                        tActivate.Start();
                        baseFramingType.Activate();
                        tActivate.Commit();
                    }
                }

                // Check wall thickness vs column thickness
                bool columnataEsMasGruesa = false;
                BoundingBoxXYZ bbCol = baseColumnType.get_BoundingBox(null);
                if (bbCol != null)
                {
                    double colTransversal = Math.Min(bbCol.Max.X - bbCol.Min.X, bbCol.Max.Y - bbCol.Min.Y);
                    foreach (var wModel in selectedWalls)
                    {
                        Wall w = doc.GetElement(new ElementId(wModel.Id)) as Wall;
                        if (w != null && colTransversal > w.Width + 0.001)
                        {
                            columnataEsMasGruesa = true;
                            break;
                        }
                    }
                }

                if (columnataEsMasGruesa)
                {
                    TaskDialog dialog = new TaskDialog("Columnetas más gruesas que el muro");
                    dialog.MainInstruction = "Aviso de espesor";
                    dialog.MainContent = "Las columnetas seleccionadas tienen un espesor mayor que el espesor de los muros.\n\nEsto hará que sobresalgan del muro de forma simétrica.\n\n¿Desea continuar?";
                    dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                    dialog.DefaultButton = TaskDialogResult.No;

                    if (dialog.Show() != TaskDialogResult.Yes)
                    {
                        transGroup.RollBack();
                        return;
                    }
                }

                List<Wall> processedWalls = new List<Wall>();
                foreach (var wModel in selectedWalls)
                {
                    Wall wall = doc.GetElement(new ElementId(wModel.Id)) as Wall;
                    if (wall == null) throw new InvalidOperationException($"El muro {wModel.Id} no existe.");
                    processedWalls.Add(wall);
                }

                using (Transaction t = new Transaction(doc, "Generar Elementos Estructurales"))
                {
                    FailureHandlingOptions options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(new WarningSwallower());
                    t.SetFailureHandlingOptions(options);

                    t.Start();

                    // FASE 1: PLANIFICACIÓN PURA (En SubTransaction)
                    StructuralPlan plan = StructuralPlannerService.CreatePlan(doc, processedWalls, baseColumnType, baseFramingType);

                    // FASE 2: EJECUCIÓN (Mutación del documento baseada en Checkboxes)
                    StructuralExecutionService.ExecutePlan(doc, plan, genOptions);

                    t.Commit();
                }

                transGroup.Assimilate();
            }

            LogStep("Proceso completado con éxito.");
        }
        catch (Exception ex)
        {
            LogStep($"ERROR: {ex.Message}\n{ex.StackTrace}");
            TaskDialog.Show("Error", $"Ocurrió un error inesperado:\n{ex.Message}");
        }
    }
}
