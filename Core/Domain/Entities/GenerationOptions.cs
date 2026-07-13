namespace CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

public class GenerationOptions
{
    public bool GenerateColumns { get; set; } = true;
    public bool GenerateTopBeams { get; set; } = true;
    public bool GenerateBottomBeams { get; set; } = true;

    /// <summary>Indica si se debe aplicar un desfase vertical a la vigueta superior.</summary>
    public bool UseTopBeamOffset { get; set; } = false;

    /// <summary>
    /// Desfase desde la coronación del muro hacia abajo, expresado en metros.
    /// Solo aplica cuando UseTopBeamOffset es true.
    /// </summary>
    public double TopBeamVerticalOffsetMeters { get; set; } = 0.0;
}

