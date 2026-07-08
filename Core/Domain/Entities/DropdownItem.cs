namespace CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

public record DropdownItem
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;

    public DropdownItem(long id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString()
    {
        return Name;
    }
}
