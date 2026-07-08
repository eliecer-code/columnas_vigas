using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Services;

public class RevitDataExtractionService : IRevitDataExtractionService
{
    public List<DropdownItem> GetStructuralColumnTypes(Document doc)
    {
        var types = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralColumns)
            .WhereElementIsElementType()
            .Cast<FamilySymbol>()
            .OrderBy(f => f.FamilyName).ThenBy(f => f.Name)
            .ToList();

        var list = new List<DropdownItem>();
        foreach (var type in types)
        {
            list.Add(new DropdownItem(type.Id.Value, $"{type.FamilyName} - {type.Name}"));
        }
        return list;
    }

    public List<DropdownItem> GetStructuralFramingTypes(Document doc)
    {
        var types = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_StructuralFraming)
            .WhereElementIsElementType()
            .Cast<FamilySymbol>()
            .OrderBy(f => f.FamilyName).ThenBy(f => f.Name)
            .ToList();

        var list = new List<DropdownItem>();
        foreach (var type in types)
        {
            list.Add(new DropdownItem(type.Id.Value, $"{type.FamilyName} - {type.Name}"));
        }
        return list;
    }

    public List<DropdownItem> GetLevels(Document doc)
    {
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();

        var list = new List<DropdownItem>();
        foreach (var level in levels)
        {
            list.Add(new DropdownItem(level.Id.Value, level.Name));
        }
        return list;
    }
}
