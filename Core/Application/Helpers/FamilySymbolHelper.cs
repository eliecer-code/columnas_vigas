using System;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public static class FamilySymbolHelper
{
    /// <summary>
    /// Gets or creates a FamilySymbol that matches the target width (thickness of the wall).
    /// </summary>
    public static FamilySymbol GetOrDuplicateSymbolWithWidth(Document doc, FamilySymbol baseSymbol, double targetWidthFeet)
    {
        // Parameter lookup: usually structural framing and columns use b, b1, Width, Anchura, Ancho
        Parameter widthParam = baseSymbol.get_Parameter(BuiltInParameter.STRUCTURAL_SECTION_COMMON_WIDTH) 
                            ?? baseSymbol.LookupParameter("b") 
                            ?? baseSymbol.LookupParameter("b1")
                            ?? baseSymbol.LookupParameter("Width") 
                            ?? baseSymbol.LookupParameter("Anchura") 
                            ?? baseSymbol.LookupParameter("Ancho");

        if (widthParam == null || widthParam.IsReadOnly)
        {
            // The parameter might not exist or might be instance-based.
            // If it's instance-based, we can't change it at the Type level, so we just return the base symbol.
            return baseSymbol; 
        }

        double currentWidth = widthParam.AsDouble();
        // If the width already matches within 1mm, use it
        if (Math.Abs(currentWidth - targetWidthFeet) < 0.003)
        {
            return baseSymbol;
        }

        // We need to duplicate the symbol and set the width
        // Target width in cm for the name (e.g., "15cm")
        double widthCm = Math.Round(targetWidthFeet * 30.48, 1);
        string newName = $"{baseSymbol.Name} - {widthCm}cm";

        // Check if it already exists
        FamilySymbol existing = new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .WherePasses(new FamilySymbolFilter(baseSymbol.Family.Id))
            .Cast<FamilySymbol>()
            .FirstOrDefault(s => s.Name == newName);

        if (existing != null)
        {
            if (!existing.IsActive) existing.Activate();
            return existing;
        }

        // Duplicate
        FamilySymbol newSymbol = baseSymbol.Duplicate(newName) as FamilySymbol;
        newSymbol.get_Parameter(widthParam.Definition).Set(targetWidthFeet);
        return newSymbol;
    }
}
