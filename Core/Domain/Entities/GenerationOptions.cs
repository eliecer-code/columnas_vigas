namespace CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

public class GenerationOptions
{
    public bool GenerateColumns { get; set; } = true;
    public bool GenerateTopBeams { get; set; } = true;
    public bool GenerateBottomBeams { get; set; } = true;
}
