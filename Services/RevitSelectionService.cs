using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Services;

public class WallSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem is Wall;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}

public class RevitSelectionService : IRevitSelectionService
{
    public List<WallDataModel> PromptWallSelection(UIApplication uiapp)
    {
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;

        List<WallDataModel> selectedWalls = new List<WallDataModel>();

        try
        {
            IList<Reference> references = uidoc.Selection.PickObjects(
                ObjectType.Element,
                new WallSelectionFilter(),
                "Selecciona uno o varios muros. Selecciona Finalizar cuando termines."
            );

            foreach (Reference reference in references)
            {
                Element element = doc.GetElement(reference);
                if (element is Wall wall)
                {
                    var wallModel = new WallDataModel
                    {
                        Id = wall.Id.Value
                    };

                    Element typeElem = doc.GetElement(wall.GetTypeId());
                    wallModel = wallModel with { NombreTipo = typeElem?.Name ?? "Desconocido" };

                    Parameter baseConstraintParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    if (baseConstraintParam != null)
                    {
                        ElementId levelId = baseConstraintParam.AsElementId();
                        if (levelId != ElementId.InvalidElementId)
                        {
                            Level level = doc.GetElement(levelId) as Level;
                            wallModel = wallModel with { NivelMuro = level?.Name ?? "N/A" };
                        }
                        else
                        {
                            wallModel = wallModel with { NivelMuro = "N/A" };
                        }
                    }
                    else
                    {
                        wallModel = wallModel with { NivelMuro = "N/A" };
                    }

                    selectedWalls.Add(wallModel);
                }
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // Ignorar error cuando el usuario cancela
        }

        return selectedWalls;
    }
}
