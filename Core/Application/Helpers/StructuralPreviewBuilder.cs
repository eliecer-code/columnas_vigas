using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Autodesk.Revit.DB;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

// Alias para evitar ambigüedad entre Autodesk.Revit.DB.Material y System.Windows.Media.Media3D.Material
using WpfMaterial = System.Windows.Media.Media3D.Material;
using WpfColor = System.Windows.Media.Color;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

/// <summary>
/// Convierte un StructuralPlan en geometría WPF 3D para el visor Helix.
/// No crea ningún elemento Revit. No inicia transacciones.
/// </summary>
public static class StructuralPreviewBuilder
{
    // Colores de preview diferenciados del gris de los muros
    private static readonly WpfMaterial _columnMaterial  = MakeMaterial(WpfColor.FromArgb(180, 30, 120, 220));   // Azul
    private static readonly WpfMaterial _topBeamMaterial = MakeMaterial(WpfColor.FromArgb(180, 40, 180, 80));    // Verde
    private static readonly WpfMaterial _botBeamMaterial = MakeMaterial(WpfColor.FromArgb(180, 240, 140, 30));   // Naranja

    private static WpfMaterial MakeMaterial(WpfColor color)
    {
        var mat = new DiffuseMaterial(new SolidColorBrush(color));
        mat.Freeze();
        return mat;
    }

    /// <summary>
    /// Genera la geometría de previsualización a partir del plan estructural.
    /// Respeta los mismos CheckBoxes que la generación real.
    /// </summary>
    public static Model3DGroup Build(
        StructuralPlan plan,
        GenerationOptions options,
        Document doc)
    {
        var group = new Model3DGroup();

        if (options.GenerateColumns)
        {
            foreach (var col in plan.Columns)
            {
                var solid = BuildColumnBox(col, doc);
                if (solid != null) group.Children.Add(solid);
            }
        }

        if (options.GenerateTopBeams)
        {
            foreach (var beam in plan.TopBeams)
            {
                var solid = BuildBeamBox(beam, doc, isTop: true);
                if (solid != null) group.Children.Add(solid);
            }
        }

        if (options.GenerateBottomBeams)
        {
            foreach (var beam in plan.BottomBeams)
            {
                var solid = BuildBeamBox(beam, doc, isTop: false);
                if (solid != null) group.Children.Add(solid);
            }
        }

        return group;
    }

    // ─── Columneta ────────────────────────────────────────────────────────────

    private static GeometryModel3D BuildColumnBox(PlannedColumn col, Document doc)
    {
        // Dimensiones desde el símbolo de familia
        BoundingBoxXYZ bb = col.ColumnType?.get_BoundingBox(null);
        double halfW = bb != null ? (bb.Max.X - bb.Min.X) / 2.0 : (0.25 / 0.3048);
        double halfD = bb != null ? (bb.Max.Y - bb.Min.Y) / 2.0 : (0.15 / 0.3048);

        // Altura: usar la altura real del muro desde la API
        double zMin = col.BaseLevel?.Elevation ?? 0;
        double zMax = zMin;
        if (col.PrimaryWall != null)
        {
            var (wMin, wMax, _) = WallGeometryHelper.GetWallElevationInfo(doc, col.PrimaryWall);
            zMin = wMin;
            zMax = wMax;
        }
        double height = zMax - zMin;
        if (height < 0.01) height = 3.0 / 0.3048;

        // Posición central de la columneta
        XYZ ins = col.InsertionPoint;
        double cx = ins.X;
        double cy = ins.Y;

        // Aplicar rotación al rectángulo de sección
        double angle = col.RotationAngle;
        var pts = RotateRect(cx, cy, halfW, halfD, angle);

        return BuildExtrudedBox(pts, zMin, height, _columnMaterial, col.RotationAngle);
    }

    // ─── Vigueta ──────────────────────────────────────────────────────────────

    private static GeometryModel3D BuildBeamBox(PlannedBeam beam, Document doc, bool isTop)
    {
        // Sección de la vigueta: ancho del muro × 20 cm alto (representación visual)
        double wallThickness = beam.ParentWall?.Width ?? (0.15 / 0.3048);
        double beamHeight = 0.20 / 0.3048; // 20 cm de altura visual

        XYZ p0 = beam.StartPoint;
        XYZ p1 = beam.EndPoint;

        // La elevación real de la vigueta = nivel de referencia + zOffset
        double levelElev = beam.BaseLevel?.Elevation ?? 0;
        double realZ = levelElev + beam.ZOffset;

        XYZ s = new XYZ(p0.X, p0.Y, realZ);
        XYZ e = new XYZ(p1.X, p1.Y, realZ);

        if (s.DistanceTo(e) < 0.05) return null;

        WpfMaterial mat = isTop ? _topBeamMaterial : _botBeamMaterial;
        return BuildBeamGeometry(s, e, wallThickness, beamHeight, mat);
    }

    // ─── Primitivas WPF 3D ────────────────────────────────────────────────────

    /// <summary>Construye un paralelepípedo a partir de 4 puntos de planta y la altura de extrusión.</summary>
    private static GeometryModel3D BuildExtrudedBox(
        (double x, double y)[] corners4,
        double zBase, double height,
        WpfMaterial mat,
        double rotationAngle = 0)
    {
        double zTop = zBase + height;

        // 8 vértices: 4 abajo + 4 arriba
        var pos = new Point3DCollection
        {
            new Point3D(corners4[0].x, corners4[0].y, zBase),
            new Point3D(corners4[1].x, corners4[1].y, zBase),
            new Point3D(corners4[2].x, corners4[2].y, zBase),
            new Point3D(corners4[3].x, corners4[3].y, zBase),
            new Point3D(corners4[0].x, corners4[0].y, zTop),
            new Point3D(corners4[1].x, corners4[1].y, zTop),
            new Point3D(corners4[2].x, corners4[2].y, zTop),
            new Point3D(corners4[3].x, corners4[3].y, zTop),
        };

        // 6 caras × 2 triángulos
        var tri = new Int32Collection
        {
            // Base
            0,2,1,  0,3,2,
            // Tapa
            4,5,6,  4,6,7,
            // Frente (0-1-5-4)
            0,1,5,  0,5,4,
            // Derecha (1-2-6-5)
            1,2,6,  1,6,5,
            // Fondo (2-3-7-6)
            2,3,7,  2,7,6,
            // Izquierda (3-0-4-7)
            3,0,4,  3,4,7,
        };

        var mesh = new MeshGeometry3D { Positions = pos, TriangleIndices = tri };
        mesh.Freeze();

        var geom = new GeometryModel3D(mesh, mat);
        geom.Freeze();
        return geom;
    }

    /// <summary>Construye una vigueta como caja rectangular entre dos puntos.</summary>
    private static GeometryModel3D BuildBeamGeometry(
        XYZ start, XYZ end,
        double width, double height,
        WpfMaterial mat)
    {
        XYZ dir = (end - start).Normalize();
        XYZ perp = XYZ.BasisZ.CrossProduct(dir).Normalize();
        if (perp.GetLength() < 0.001) perp = XYZ.BasisY;

        double halfW = width / 2.0;
        double halfH = height / 2.0;

        // 4 esquinas en el plano de inicio
        XYZ p0 = ToPoint(start + perp * halfW - XYZ.BasisZ * halfH);
        XYZ p1 = ToPoint(start - perp * halfW - XYZ.BasisZ * halfH);
        XYZ p2 = ToPoint(start - perp * halfW + XYZ.BasisZ * halfH);
        XYZ p3 = ToPoint(start + perp * halfW + XYZ.BasisZ * halfH);

        // 4 esquinas en el plano de fin
        XYZ p4 = ToPoint(end + perp * halfW - XYZ.BasisZ * halfH);
        XYZ p5 = ToPoint(end - perp * halfW - XYZ.BasisZ * halfH);
        XYZ p6 = ToPoint(end - perp * halfW + XYZ.BasisZ * halfH);
        XYZ p7 = ToPoint(end + perp * halfW + XYZ.BasisZ * halfH);

        var pos = new Point3DCollection {
            ToWpf(p0), ToWpf(p1), ToWpf(p2), ToWpf(p3),
            ToWpf(p4), ToWpf(p5), ToWpf(p6), ToWpf(p7)
        };

        var tri = new Int32Collection
        {
            0,2,1,  0,3,2,   // inicio
            4,5,6,  4,6,7,   // fin
            0,1,5,  0,5,4,   // abajo
            3,7,6,  3,6,2,   // arriba
            0,4,7,  0,7,3,   // derecha
            1,2,6,  1,6,5,   // izquierda
        };

        var mesh = new MeshGeometry3D { Positions = pos, TriangleIndices = tri };
        mesh.Freeze();
        var geom = new GeometryModel3D(mesh, mat);
        geom.Freeze();
        return geom;
    }

    // ─── Utilidades ───────────────────────────────────────────────────────────

    private static (double x, double y)[] RotateRect(double cx, double cy, double hw, double hd, double angle)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        (double lx, double ly)[] local =
        {
            (-hw, -hd), ( hw, -hd), ( hw,  hd), (-hw,  hd)
        };

        var result = new (double x, double y)[4];
        for (int i = 0; i < 4; i++)
        {
            result[i] = (
                cx + local[i].lx * cos - local[i].ly * sin,
                cy + local[i].lx * sin + local[i].ly * cos
            );
        }
        return result;
    }

    private static XYZ ToPoint(XYZ v) => v;  // identidad; mantiene XYZ
    private static Point3D ToWpf(XYZ v) => new Point3D(v.X, v.Y, v.Z);
}
