using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

public class StructuralPlan
{
    public List<PlannedColumn> Columns { get; set; } = new();
    public List<PlannedBeam> TopBeams { get; set; } = new();
    public List<PlannedBeam> BottomBeams { get; set; } = new();
    public List<PlannedWallCut> WallCuts { get; set; } = new();
}

public class PlannedColumn
{
    public XYZ InsertionPoint { get; set; }
    public double RotationAngle { get; set; }
    public Level BaseLevel { get; set; }
    public Wall PrimaryWall { get; set; }
    public FamilySymbol ColumnType { get; set; }
}

public class PlannedBeam
{
    public XYZ StartPoint { get; set; }
    public XYZ EndPoint { get; set; }
    public Level BaseLevel { get; set; }
    public double ZOffset { get; set; }
    public FamilySymbol FramingType { get; set; }
    public Wall ParentWall { get; set; }
}

public class PlannedWallCut
{
    public Wall Wall { get; set; }
    public int EndIndex { get; set; } // 0 for Start, 1 for End
    public XYZ NewEndPoint { get; set; }
}
