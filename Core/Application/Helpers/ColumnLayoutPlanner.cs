using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public class ColumnRecord
{
    public ElementId Id { get; set; }
    public XYZ Center { get; set; }
    public BoundingBoxXYZ BoundingBox { get; set; }
}

public class FreeSpan
{
    public Wall Wall { get; set; }
    public XYZ StartPoint { get; set; }
    public XYZ EndPoint { get; set; }
    public XYZ Direction { get; set; }
    public double Length => StartPoint.DistanceTo(EndPoint);
}

public static class ColumnLayoutPlanner
{
    public static List<ColumnRecord> CornerColumns { get; set; } = new List<ColumnRecord>();

    public static void RegisterColumn(FamilyInstance col)
    {
        var lc = col.Location as LocationPoint;
        var bb = col.get_BoundingBox(null);
        if (lc != null && bb != null)
        {
            CornerColumns.Add(new ColumnRecord
            {
                Id = col.Id,
                Center = lc.Point,
                BoundingBox = bb
            });
        }
    }

    public static void ClearRegisteredColumns()
    {
        CornerColumns.Clear();
    }

    public static List<FreeSpan> CalculateFreeSpans(Document doc, Wall wall, List<FamilyInstance> existingColumns)
    {
        List<FreeSpan> spans = new List<FreeSpan>();
        LocationCurve lc = wall.Location as LocationCurve;
        if (lc == null) return spans;

        Curve curve = lc.Curve;
        XYZ p0 = curve.GetEndPoint(0);
        XYZ p1 = curve.GetEndPoint(1);
        XYZ dir = (p1 - p0).Normalize();
        double totalLength = p0.DistanceTo(p1);

        // Intervalos ocupados en el parámetro t del muro (donde t va de 0 a totalLength)
        List<Tuple<double, double>> occupiedIntervals = new List<Tuple<double, double>>();

        // Unir las columnetas recién creadas en esquinas con las que ya existían en el modelo
        var allCols = new List<ColumnRecord>(CornerColumns);
        
        foreach (var exCol in existingColumns)
        {
            if (allCols.Any(c => c.Id == exCol.Id)) continue;
            var cLc = exCol.Location as LocationPoint;
            var cBb = exCol.get_BoundingBox(null);
            if (cLc != null && cBb != null)
            {
                allCols.Add(new ColumnRecord { Id = exCol.Id, Center = cLc.Point, BoundingBox = cBb });
            }
        }

        // Detectar columnetas que intersectan este muro
        Line infiniteLine = Line.CreateUnbound(p0, dir);
        double wallWidth = wall.Width;

        foreach (var col in allCols)
        {
            // Proyectar el centro de la columneta sobre la línea del muro
            IntersectionResult result = infiniteLine.Project(col.Center);
            if (result != null)
            {
                double distToLine = result.XYZPoint.DistanceTo(col.Center);
                // Tolerancia: si el centro de la columneta está razonablemente cerca del eje del muro
                if (distToLine < wallWidth * 2) 
                {
                    // Calcular el rango del BoundingBox proyectado sobre la dirección del muro
                    XYZ min = col.BoundingBox.Min;
                    XYZ max = col.BoundingBox.Max;
                    
                    // Puntos de las 4 esquinas del BoundingBox
                    List<XYZ> bbCorners = new List<XYZ>
                    {
                        new XYZ(min.X, min.Y, col.Center.Z),
                        new XYZ(max.X, min.Y, col.Center.Z),
                        new XYZ(min.X, max.Y, col.Center.Z),
                        new XYZ(max.X, max.Y, col.Center.Z)
                    };

                    double tMin = double.MaxValue;
                    double tMax = double.MinValue;

                    foreach (var pt in bbCorners)
                    {
                        IntersectionResult proj = infiniteLine.Project(pt);
                        if (proj != null)
                        {
                            // Encontrar el parámetro t a lo largo del segmento [p0, p1]
                            double t = (proj.XYZPoint - p0).DotProduct(dir);
                            if (t < tMin) tMin = t;
                            if (t > tMax) tMax = t;
                        }
                    }

                    // Añadir un pequeño margen de seguridad
                    tMin -= 0.01;
                    tMax += 0.01;

                    occupiedIntervals.Add(new Tuple<double, double>(tMin, tMax));
                }
            }
        }

        // Consolidar intervalos ocupados
        occupiedIntervals = occupiedIntervals.OrderBy(i => i.Item1).ToList();
        List<Tuple<double, double>> mergedIntervals = new List<Tuple<double, double>>();
        
        foreach (var interval in occupiedIntervals)
        {
            if (!mergedIntervals.Any())
            {
                mergedIntervals.Add(interval);
            }
            else
            {
                var last = mergedIntervals.Last();
                if (interval.Item1 <= last.Item2) // Solape
                {
                    mergedIntervals[mergedIntervals.Count - 1] = new Tuple<double, double>(last.Item1, Math.Max(last.Item2, interval.Item2));
                }
                else
                {
                    mergedIntervals.Add(interval);
                }
            }
        }

        // Construir los FreeSpans invirtiendo los intervalos ocupados sobre [0, totalLength]
        double currentT = 0;
        foreach (var interval in mergedIntervals)
        {
            if (interval.Item1 > currentT)
            {
                double startT = Math.Max(0, currentT);
                double endT = Math.Min(totalLength, interval.Item1);
                
                if (endT > startT + 0.1) // Tramo libre mínimo de 0.1 pies
                {
                    spans.Add(new FreeSpan
                    {
                        Wall = wall,
                        StartPoint = p0 + dir * startT,
                        EndPoint = p0 + dir * endT,
                        Direction = dir
                    });
                }
            }
            currentT = Math.Max(currentT, interval.Item2);
        }

        // El último tramo si queda espacio al final
        if (currentT < totalLength - 0.1)
        {
            spans.Add(new FreeSpan
            {
                Wall = wall,
                StartPoint = p0 + dir * currentT,
                EndPoint = p0 + dir * totalLength,
                Direction = dir
            });
        }

        return spans;
    }
}
