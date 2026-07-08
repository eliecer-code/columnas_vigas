using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

/// <summary>
/// Motor geométrico centralizado para el cálculo de mampostería confinada.
/// Implementa la filosofía de "Reemplazo Volumétrico": la columneta NO se añade al muro, 
/// sino que REEMPLAZA una porción exacta del mismo, conservando la longitud total original.
/// </summary>
public static class WallConfinementCalculator
{
    /// <summary>
    /// Calcula las dimensiones reales de la columneta basándose en el símbolo y el muro principal.
    /// El espesor de la columneta siempre será igual al espesor del muro principal.
    /// </summary>
    public static (double Width, double Thickness) GetColumnetaDimensions(FamilySymbol symbol, Wall primaryWall)
    {
        double width = 0.30 / 0.3048; // Default 30cm
        Parameter bParam = symbol.LookupParameter("b");
        if (bParam != null) width = bParam.AsDouble();
        else
        {
            Parameter hParam = symbol.LookupParameter("h");
            if (hParam != null) width = hParam.AsDouble();
            else
            {
                BoundingBoxXYZ bb = symbol.get_BoundingBox(null);
                if (bb != null) width = bb.Max.X - bb.Min.X;
            }
        }

        // REGLA: "Ese espesor SIEMPRE debe ser igual al espesor real del muro. No debe tomarse de un valor fijo."
        double thickness = primaryWall.Width;

        return (width, thickness);
    }

    /// <summary>
    /// Calcula cuánto debe recortarse un muro específico en un nodo.
    /// </summary>
    public static double CalculateCutLength(Wall wall, Wall primaryWall, double colWidth, double colThickness)
    {
        // Si el muro es paralelo al principal (o es el principal), se le descuenta el ancho de la columneta.
        // Si es perpendicular (secundario), se le descuenta el espesor de la columneta.
        LocationCurve lcWall = wall.Location as LocationCurve;
        LocationCurve lcPrim = primaryWall.Location as LocationCurve;

        XYZ dirWall = (lcWall.Curve.GetEndPoint(1) - lcWall.Curve.GetEndPoint(0)).Normalize();
        XYZ dirPrim = (lcPrim.Curve.GetEndPoint(1) - lcPrim.Curve.GetEndPoint(0)).Normalize();

        double dot = Math.Abs(dirWall.DotProduct(dirPrim));
        
        // Si son paralelos (dot ~ 1), recortamos el ancho completo (ej. 30cm).
        // Si son perpendiculares (dot ~ 0), recortamos el espesor (ej. 15cm).
        if (dot > 0.9) return colWidth;
        else return colThickness;
    }

    /// <summary>
    /// Calcula el nuevo punto final del muro tras aplicarle el recorte (Longitud Útil).
    /// </summary>
    public static (XYZ NewEndpoint, bool IsStart, XYZ InwardDir) CalculateTrimmedEndpoint(Wall wall, XYZ nodePoint, double cutLength)
    {
        LocationCurve lc = wall.Location as LocationCurve;
        XYZ p0 = lc.Curve.GetEndPoint(0);
        XYZ p1 = lc.Curve.GetEndPoint(1);
        
        double dist0 = p0.DistanceTo(nodePoint);
        bool isStart = (dist0 < 0.05);
        
        // Vector que apunta desde el nodo hacia adentro del muro
        XYZ inwardDir = isStart ? (p1 - p0).Normalize() : (p0 - p1).Normalize();
        
        XYZ newEndpoint = nodePoint + inwardDir * cutLength;
        return (newEndpoint, isStart, inwardDir);
    }

    /// <summary>
    /// Calcula la posición exacta de inserción de la columneta en el modelo.
    /// No utiliza offsets mágicos. Interseca geométricamente las direcciones de los muros para centrar la columneta.
    /// </summary>
    public static XYZ CalculateColumnetaPosition(List<Wall> connectedWalls, Wall primaryWall, XYZ nodePoint, double colWidth, double colThickness, double baseElevation)
    {
        XYZ center = nodePoint;

        // Vector del muro principal
        LocationCurve lcPrim = primaryWall.Location as LocationCurve;
        XYZ p0P = lcPrim.Curve.GetEndPoint(0);
        XYZ p1P = lcPrim.Curve.GetEndPoint(1);
        bool isStartP = p0P.DistanceTo(nodePoint) < 0.05;
        XYZ dirPrimInward = isStartP ? (p1P - p0P).Normalize() : (p0P - p1P).Normalize();

        // Desplazamos el centro a lo largo del muro principal por la mitad del ancho de la columneta
        center += dirPrimInward * (colWidth / 2.0);

        // Buscar un muro secundario (perpendicular) para desplazar el centro en el eje transversal
        Wall secondaryWall = connectedWalls.FirstOrDefault(w => w.Id != primaryWall.Id);
        if (secondaryWall != null)
        {
            LocationCurve lcSec = secondaryWall.Location as LocationCurve;
            XYZ p0S = lcSec.Curve.GetEndPoint(0);
            XYZ p1S = lcSec.Curve.GetEndPoint(1);
            bool isStartS = p0S.DistanceTo(nodePoint) < 0.05;
            XYZ dirSecInward = isStartS ? (p1S - p0S).Normalize() : (p0S - p1S).Normalize();

            // Desplazamos el centro a lo largo del muro secundario por la mitad del espesor
            center += dirSecInward * (colThickness / 2.0);
        }

        return new XYZ(center.X, center.Y, baseElevation);
    }

    /// <summary>
    /// Devuelve el vector desde el Origen (Insertion Point) de la familia hasta su Centro Geométrico Real en coordenadas locales.
    /// </summary>
    public static XYZ GetFamilyOriginOffset(FamilySymbol symbol)
    {
        BoundingBoxXYZ bb = symbol.get_BoundingBox(null);
        if (bb != null)
        {
            return (bb.Min + bb.Max) / 2.0;
        }
        return XYZ.Zero;
    }

    /// <summary>
    /// Calcula el desplazamiento transversal necesario para enrasar la columneta con la cara exterior del muro.
    /// Considera el espesor real de la familia, el espesor del muro y si el muro está invertido (Flipped).
    /// </summary>
    public static XYZ CalculateTransversalAlignmentOffset(Wall wall, FamilySymbol columnType, XYZ wallDir)
    {
        double wallThickness = wall.Width;
        
        double colTransversal = wallThickness; // Default
        BoundingBoxXYZ bb = columnType.get_BoundingBox(null);
        if (bb != null)
        {
            colTransversal = bb.Max.Y - bb.Min.Y;
        }

        XYZ normal = XYZ.BasisZ.CrossProduct(wallDir).Normalize();

        XYZ extDir = wall.Flipped ? -normal : normal;

        double offsetDist = (wallThickness - colTransversal) / 2.0;
        
        return extDir * offsetDist;
    }
}
