using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public class ContinuousWallPath
{
    public Wall Wall { get; set; }
    public XYZ StartPoint { get; set; }
    public XYZ EndPoint { get; set; }
    public double Length => StartPoint.DistanceTo(EndPoint);
    public XYZ Direction => (EndPoint - StartPoint).Normalize();
}

public static class ContinuousWallPathAnalyzer
{
    public static ContinuousWallPath GetContinuousPath(Document doc, Wall wall, List<FamilyInstance> existingCols)
    {
        // Reutilizamos la segmentación para extraer puramente los nodos fronterizos
        // de inicio y fin del muro completo (ignorando las divisiones internas)
        var segments = WallSegmentAnalyzer.GetSegments(doc, wall, existingCols);
        
        if (segments == null || !segments.Any())
            return null;
            
        // El inicio del primer segmento es la cara de la columneta inicial
        XYZ startPt = segments.First().StartPoint;
        
        // El fin del último segmento es la cara de la columneta final
        XYZ endPt = segments.Last().EndPoint;
        
        return new ContinuousWallPath
        {
            Wall = wall,
            StartPoint = startPt,
            EndPoint = endPt
        };
    }
}
