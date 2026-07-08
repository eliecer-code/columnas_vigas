namespace CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

public record WallDataModel
{
    public long Id { get; init; }
    public string NivelMuro { get; init; } = string.Empty;
    public string NombreTipo { get; init; } = string.Empty;
}
