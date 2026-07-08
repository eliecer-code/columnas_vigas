using System.Collections.Generic;
using Autodesk.Revit.UI;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;

public interface IElementGenerationService
{
    void GenerateElements(
        UIApplication uiapp,
        List<WallDataModel> selectedWalls,
        long columnTypeId,
        long framingTypeId);
}
