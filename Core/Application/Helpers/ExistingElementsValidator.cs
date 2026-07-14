using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public class ExistingElementsReport
{
    public bool HasExistingElements { get; set; }
    public string FormattedMessage { get; set; } = string.Empty;
}

/// <summary>
/// Valida si los muros seleccionados ya poseen elementos generados por el Addin.
/// Es de solo lectura y no requiere transacciones.
/// </summary>
public static class ExistingElementsValidator
{
    public static ExistingElementsReport Validate(Document doc, List<WallDataModel> selectedWalls)
    {
        if (selectedWalls == null || !selectedWalls.Any())
            return new ExistingElementsReport { HasExistingElements = false };

        // Convertimos a HashSet para búsquedas O(1)
        var selectedWallIds = new HashSet<ElementId>(
            selectedWalls.Select(w => new ElementId(w.Id))
        );

        // Buscar TODAS las columnetas y viguetas en el modelo.
        // Como el filtro de Extensible Storage directo en FilteredElementCollector 
        // a veces es problemático o requiere ExtensibleStorageFilter (no disponible en versiones antiguas),
        // traemos todo el Framing/Columns y filtramos en memoria. Sigue siendo muy rápido.
        var allElements = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .WherePasses(new LogicalOrFilter(
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming)
            ))
            .OfClass(typeof(FamilyInstance))
            .ToList();

        // Agrupar elementos encontrados por ID del muro
        // Diccionario: WallId -> Lista de Tipos ("Columneta", "Vigueta Superior", etc.)
        var existingPerWall = new Dictionary<ElementId, HashSet<string>>();

        foreach (var element in allElements)
        {
            if (CQIngExtensibleStorageMarker.TryGetMarkerData(element, out ElementId parentWallId, out string elementType))
            {
                // Si el elemento fue generado para uno de los muros actualmente seleccionados
                if (selectedWallIds.Contains(parentWallId))
                {
                    if (!existingPerWall.ContainsKey(parentWallId))
                        existingPerWall[parentWallId] = new HashSet<string>();

                    existingPerWall[parentWallId].Add(elementType);
                }
            }
        }

        if (!existingPerWall.Any())
            return new ExistingElementsReport { HasExistingElements = false };

        // Construir el reporte detallado
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Se detectaron muros que ya contienen elementos generados por el Addin.");
        sb.AppendLine("Revise los muros seleccionados antes de continuar.");
        sb.AppendLine("No se realizó ninguna modificación.");
        sb.AppendLine();
        sb.AppendLine($"Cantidad de muros afectados: {existingPerWall.Count}");
        sb.AppendLine();

        foreach (var kvp in existingPerWall)
        {
            ElementId wallId = kvp.Key;
            string elementsStr = string.Join(", ", kvp.Value);
            sb.AppendLine($"- Muro {wallId.ToString()} → {elementsStr} existentes.");
        }

        return new ExistingElementsReport
        {
            HasExistingElements = true,
            FormattedMessage = sb.ToString()
        };
    }
}
