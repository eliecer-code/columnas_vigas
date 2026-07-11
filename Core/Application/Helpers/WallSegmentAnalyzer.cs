using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public class WallSegment
{
    public Wall Wall { get; set; }
    public XYZ StartPoint { get; set; }
    public XYZ EndPoint { get; set; }
    public double Length => StartPoint.DistanceTo(EndPoint);
    public XYZ Direction => (EndPoint - StartPoint).Normalize();
    public double Thickness => Wall.Width;
}

public static class WallSegmentAnalyzer
{
    public static List<WallSegment> GetSegments(Document doc, Wall wall, List<FamilyInstance> existingCols)
    {
        // Reutilizamos el motor matemático del Layout Planner que descuenta el volumen de las columnetas
        var freeSpans = ColumnLayoutPlanner.CalculateFreeSpans(doc, wall, existingCols);
        
        List<WallSegment> segments = new List<WallSegment>();
        foreach (var span in freeSpans)
        {
            segments.Add(new WallSegment
            {
                Wall = span.Wall,
                StartPoint = span.StartPoint,
                EndPoint = span.EndPoint
            });
        }
        
        return segments;
    }
}
