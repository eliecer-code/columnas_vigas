using System.Collections.Generic;
using Autodesk.Revit.DB;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;

public interface IRevitDataExtractionService
{
    List<DropdownItem> GetStructuralColumnTypes(Document doc);
    List<DropdownItem> GetStructuralFramingTypes(Document doc);
    List<DropdownItem> GetLevels(Document doc);
}
