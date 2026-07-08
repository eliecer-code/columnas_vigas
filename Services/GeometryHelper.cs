using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Services;

public static class GeometryHelper
{
    public static GeometryModel3D GetWallGeometryModel(Wall wall)
    {
        var options = new Options
        {
            DetailLevel = ViewDetailLevel.Fine,
            ComputeReferences = false
        };

        var geometryElement = wall.get_Geometry(options);
        if (geometryElement == null) return null;

        var meshGeometry = new MeshGeometry3D();

        foreach (var geomObj in geometryElement)
        {
            if (geomObj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 0)
            {
                AddSolidToMesh(solid, meshGeometry);
            }
            else if (geomObj is GeometryInstance geomInstance)
            {
                var instanceGeometry = geomInstance.GetInstanceGeometry();
                foreach (var instObj in instanceGeometry)
                {
                    if (instObj is Solid instSolid && instSolid.Faces.Size > 0 && instSolid.Volume > 0)
                    {
                        AddSolidToMesh(instSolid, meshGeometry);
                    }
                }
            }
        }

        if (meshGeometry.Positions.Count == 0) return null;

        // Material semitransparente o gris claro para muros
        var material = new DiffuseMaterial(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 200, 200, 200)));
        
        var model = new GeometryModel3D(meshGeometry, material);
        return model;
    }

    private static void AddSolidToMesh(Solid solid, MeshGeometry3D mesh)
    {
        foreach (Face face in solid.Faces)
        {
            var meshObj = face.Triangulate();
            if (meshObj == null) continue;

            int startIndex = mesh.Positions.Count;

            foreach (var vertex in meshObj.Vertices)
            {
                // Revit Coordinate system (Z is up) -> WPF Coordinate system
                mesh.Positions.Add(new Point3D(vertex.X, vertex.Y, vertex.Z));
            }

            for (int i = 0; i < meshObj.NumTriangles; i++)
            {
                var triangle = meshObj.get_Triangle(i);
                mesh.TriangleIndices.Add(startIndex + (int)triangle.get_Index(0));
                mesh.TriangleIndices.Add(startIndex + (int)triangle.get_Index(1));
                mesh.TriangleIndices.Add(startIndex + (int)triangle.get_Index(2));
            }
        }
    }
}
