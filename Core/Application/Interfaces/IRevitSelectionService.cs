using System.Collections.Generic;
using Autodesk.Revit.UI;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;

public interface IRevitSelectionService
{
    List<WallDataModel> PromptWallSelection(UIApplication uiapp);
}
