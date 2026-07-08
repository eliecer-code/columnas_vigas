using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public static class RealGeometryHelper
{
    public class SolidBounds
    {
        public double MinX { get; set; } = double.MaxValue;
        public double MaxX { get; set; } = double.MinValue;
        public double MinY { get; set; } = double.MaxValue;
        public double MaxY { get; set; } = double.MinValue;
        public double MinZ { get; set; } = double.MaxValue;
        public double MaxZ { get; set; } = double.MinValue;
        public bool IsValid => MinX <= MaxX;
    }

    public static (double ZMin, double ZMax) GetSolidElevation(Element element, Document doc)
    {
        var bounds = GetSolidBounds(element, doc);
        if (bounds.IsValid) return (bounds.MinZ, bounds.MaxZ);
        
        BoundingBoxXYZ bb = element.get_BoundingBox(null);
        if (bb != null) return (bb.Min.Z, bb.Max.Z);
        
        return (0, 0);
    }

    public static SolidBounds GetSolidBounds(Element element, Document doc)
    {
        SolidBounds bounds = new SolidBounds();
        
        Options opt = new Options 
        { 
            DetailLevel = ViewDetailLevel.Fine, 
            ComputeReferences = true 
        };
        
        GeometryElement geom = element.get_Geometry(opt);
        if (geom == null) return bounds;

        void ProcessSolid(Solid solid)
        {
            if (solid != null && solid.Faces.Size > 0 && solid.Volume > 0.0001)
            {
                foreach (Face face in solid.Faces)
                {
                    Mesh mesh = face.Triangulate();
                    if (mesh != null)
                    {
                        foreach (XYZ v in mesh.Vertices)
                        {
                            if (v.X < bounds.MinX) bounds.MinX = v.X;
                            if (v.X > bounds.MaxX) bounds.MaxX = v.X;
                            if (v.Y < bounds.MinY) bounds.MinY = v.Y;
                            if (v.Y > bounds.MaxY) bounds.MaxY = v.Y;
                            if (v.Z < bounds.MinZ) bounds.MinZ = v.Z;
                            if (v.Z > bounds.MaxZ) bounds.MaxZ = v.Z;
                        }
                    }
                }
            }
        }

        foreach (GeometryObject obj in geom)
        {
            if (obj is Solid solid)
            {
                ProcessSolid(solid);
            }
            else if (obj is GeometryInstance inst)
            {
                GeometryElement instGeom = inst.GetInstanceGeometry();
                if (instGeom != null)
                {
                    foreach (GeometryObject instObj in instGeom)
                    {
                        if (instObj is Solid instSolid)
                        {
                            ProcessSolid(instSolid);
                        }
                    }
                }
            }
        }

        return bounds;
    }

    public static XYZ GetExtremePoint(Element element, Document doc, XYZ direction)
    {
        Options opt = new Options 
        { 
            DetailLevel = ViewDetailLevel.Fine, 
            ComputeReferences = true 
        };
        
        GeometryElement geom = element.get_Geometry(opt);
        if (geom == null) return null;

        XYZ extremePt = null;
        double maxDot = double.MinValue;

        void ProcessSolid(Solid solid)
        {
            if (solid != null && solid.Faces.Size > 0 && solid.Volume > 0.0001)
            {
                foreach (Face face in solid.Faces)
                {
                    Mesh mesh = face.Triangulate();
                    if (mesh != null)
                    {
                        foreach (XYZ v in mesh.Vertices)
                        {
                            double dot = v.DotProduct(direction);
                            if (dot > maxDot)
                            {
                                maxDot = dot;
                                extremePt = v;
                            }
                        }
                    }
                }
            }
        }

        foreach (GeometryObject obj in geom)
        {
            if (obj is Solid solid)
            {
                ProcessSolid(solid);
            }
            else if (obj is GeometryInstance inst)
            {
                GeometryElement instGeom = inst.GetInstanceGeometry();
                if (instGeom != null)
                {
                    foreach (GeometryObject instObj in instGeom)
                    {
                        if (instObj is Solid instSolid)
                        {
                            ProcessSolid(instSolid);
                        }
                    }
                }
            }
        }

        return extremePt;
    }
}
