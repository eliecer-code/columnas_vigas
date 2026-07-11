using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Services;

public static class StructuralPlannerService
{
    private static void LogStep(string step)
    {
        try
        {
            string path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "CQIng_Diagnostic.log"
            );
            System.IO.File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [PLANNER] {step}\n"
            );
        }
        catch { }
    }

    public static StructuralPlan CreatePlan(
        Document doc,
        List<Wall> processedWalls,
        FamilySymbol baseColumnType,
        FamilySymbol baseFramingType)
    {
        var plan = new StructuralPlan();
        LogStep("Iniciando fase de planificación en SubTransaction.");
        
        using (SubTransaction subT = new SubTransaction(doc))
        {
            subT.Start();
            
            // 1. Obtener elementos existentes
            var existingCols = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(
                    new LogicalOrFilter(
                        new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                        new ElementCategoryFilter(BuiltInCategory.OST_Columns)
                    )
                )
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            var existingFramings = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            ColumnLayoutPlanner.ClearRegisteredColumns();
            foreach (var exCol in existingCols)
                ColumnLayoutPlanner.RegisterColumn(exCol);

            List<XYZ> createdColumnPoints = new List<XYZ>();
            List<FamilyInstance> createdColumns = new List<FamilyInstance>();
            List<FamilyInstance> createdFramings = new List<FamilyInstance>();

            bool HasDuplicateBeam(XYZ pt0, XYZ pt1, double elevation)
            {
                XYZ midPt = (pt0 + pt1) / 2.0;
                midPt = new XYZ(midPt.X, midPt.Y, elevation);
                
                foreach (var beam in existingFramings)
                {
                    var lc = beam.Location as LocationCurve;
                    if (lc != null && lc.Curve.Distance(midPt) < 0.5)
                        return true;
                }
                foreach (var beam in createdFramings)
                {
                    var lc = beam.Location as LocationCurve;
                    if (lc != null && lc.Curve.Distance(midPt) < 0.5)
                        return true;
                }
                return false;
            }

            bool IsColumnNearby(XYZ pt, double minZ, double maxZ)
            {
                double tolXY = 0.05 / 0.3048;
                XYZ ptFlat = new XYZ(pt.X, pt.Y, 0);

                foreach (var cp in createdColumnPoints)
                {
                    if (new XYZ(cp.X, cp.Y, 0).DistanceTo(ptFlat) < tolXY)
                        return true;
                }

                foreach (var col in existingCols)
                {
                    BoundingBoxXYZ bb = col.get_BoundingBox(null);
                    if (bb != null)
                    {
                        bool overXY =
                            pt.X >= bb.Min.X - tolXY
                            && pt.X <= bb.Max.X + tolXY
                            && pt.Y >= bb.Min.Y - tolXY
                            && pt.Y <= bb.Max.Y + tolXY;
                        bool overZ = !(maxZ < bb.Min.Z - tolXY || minZ > bb.Max.Z + tolXY);
                        if (overXY && overZ)
                            return true;
                    }
                }
                return false;
            }

            BoundingBoxXYZ bbBaseCol = baseColumnType.get_BoundingBox(null);
            double baseColTransversal = bbBaseCol != null
                ? Math.Min(bbBaseCol.Max.X - bbBaseCol.Min.X, bbBaseCol.Max.Y - bbBaseCol.Min.Y)
                : 0;

            FamilySymbol GetColumnType(double wallThick) =>
                baseColTransversal > wallThick + 0.001
                    ? baseColumnType
                    : FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(doc, baseColumnType, wallThick);

            var endStates = ConstructionStateManager.AnalyzeModelState(doc, processedWalls);
            List<CornerNode> nodes = CornerNodeSolver.BuildTopologicalNodes(doc, processedWalls, endStates);

            // COLUMNETAS DE ESQUINA Y RECORTES
            foreach (var node in nodes)
            {
                if (!node.ConnectedWalls.Any()) continue;

                Wall primaryWallBase = node.ConnectedWalls.OrderByDescending(w => w.Thickness).First().Wall;
                var (zMin, zMax, baseLevel) = WallGeometryHelper.GetWallElevationInfo(doc, primaryWallBase);

                if (IsColumnNearby(node.Point, zMin, zMax)) continue;

                FamilySymbol columnType = GetColumnType(primaryWallBase.Width);
                CornerNodeSolver.SolveNodeGeometry(node, columnType, baseLevel.Elevation);

                // RECORTES
                foreach (var nw in node.ConnectedWalls)
                {
                    string key = $"{nw.Wall.Id}_{(nw.IsStart ? "Start" : "End")}";
                    bool isAlreadyReduced = endStates.ContainsKey(key) && endStates[key].HasExistingColumneta;
                    
                    if (isAlreadyReduced || nw.IsContinuous) continue;

                    double cutLength = node.WallCutLengths[nw.Wall.Id];
                    XYZ newEnd = node.Point + nw.InwardDir * cutLength;

                    plan.WallCuts.Add(new PlannedWallCut
                    {
                        Wall = nw.Wall,
                        EndIndex = nw.IsStart ? 0 : 1,
                        NewEndPoint = newEnd
                    });

                    LocationCurve lcW = nw.Wall.Location as LocationCurve;
                    XYZ p0 = lcW.Curve.GetEndPoint(0);
                    XYZ p1 = lcW.Curve.GetEndPoint(1);
                    if (nw.IsStart) p0 = newEnd; else p1 = newEnd;
                    
                    if (p0.DistanceTo(p1) > 0.5)
                    {
                        WallUtils.DisallowWallJoinAtEnd(nw.Wall, nw.IsStart ? 0 : 1);
                        lcW.Curve = Line.CreateBound(p0, p1);
                    }
                }
                doc.Regenerate();

                double angle = node.RotationAngle;
                LocationCurve lcPrimary = primaryWallBase.Location as LocationCurve;
                XYZ wallDirOrig = (lcPrimary.Curve.GetEndPoint(1) - lcPrimary.Curve.GetEndPoint(0)).Normalize();
                XYZ transOffset = WallConfinementCalculator.CalculateTransversalAlignmentOffset(primaryWallBase, columnType, wallDirOrig);
                XYZ adjustedTargetCenter = node.TargetCenter + transOffset;
                
                XYZ localOriginOffset = WallConfinementCalculator.GetFamilyOriginOffset(columnType);
                XYZ rotatedLocalOffset = Transform.CreateRotation(XYZ.BasisZ, angle).OfVector(localOriginOffset);
                XYZ originInsertionPoint = adjustedTargetCenter - rotatedLocalOffset;

                bool nodeHasColumneta = node.ConnectedWalls.Any(nw => 
                {
                    string key = $"{nw.Wall.Id}_{(nw.IsStart ? "Start" : "End")}";
                    return endStates.ContainsKey(key) && endStates[key].HasExistingColumneta;
                });

                if (!nodeHasColumneta)
                {
                    plan.Columns.Add(new PlannedColumn
                    {
                        InsertionPoint = originInsertionPoint,
                        RotationAngle = angle,
                        BaseLevel = baseLevel,
                        PrimaryWall = primaryWallBase,
                        ColumnType = baseColumnType // Always store base type, execution will duplicate
                    });

                    FamilyInstance col = doc.Create.NewFamilyInstance(originInsertionPoint, columnType, baseLevel, Autodesk.Revit.DB.Structure.StructuralType.Column);
                    WallConfinementCalculator.ApplyColumnetaConstraints(col, primaryWallBase);
                    doc.Regenerate();
                    
                    if (Math.Abs(angle) > 0.001)
                    {
                        Line axis = Line.CreateBound(originInsertionPoint, originInsertionPoint + XYZ.BasisZ);
                        ElementTransformUtils.RotateElement(doc, col.Id, axis, angle);
                        doc.Regenerate();
                    }

                    ColumnLayoutPlanner.RegisterColumn(col);
                    createdColumns.Add(col);
                    createdColumnPoints.Add(node.Point);
                }
            }

            // COLUMNETAS INTERMEDIAS
            double xMax = 3.0 / 0.3048;
            foreach (var wall in processedWalls)
            {
                var freeSpans = ColumnLayoutPlanner.CalculateFreeSpans(doc, wall, existingCols);
                foreach (var span in freeSpans)
                {
                    if (span.Length <= xMax) continue;

                    FamilySymbol columnType = GetColumnType(wall.Width);
                    var (colWidth, colThickness) = WallConfinementCalculator.GetColumnetaDimensions(columnType, wall);
                    double B = colWidth;
                    int N = (int)Math.Ceiling((span.Length - xMax) / (xMax + B));
                    if (N <= 0) continue;

                    double xReal = (span.Length - N * B) / (N + 1);
                    XYZ p0 = span.StartPoint;
                    XYZ dir = span.Direction;
                    var (zMin, zMax, baseLevel) = WallGeometryHelper.GetWallElevationInfo(doc, wall);
                    double angle = WallConfinementCalculator.GetColumnetaRotationAngle(dir, columnType);
                    
                    XYZ localOriginOffset = WallConfinementCalculator.GetFamilyOriginOffset(columnType);
                    XYZ rotatedLocalOffset = Transform.CreateRotation(XYZ.BasisZ, angle).OfVector(localOriginOffset);

                    double currentDist = xReal;
                    for (int i = 0; i < N; i++)
                    {
                        XYZ targetCenter = p0 + dir * (currentDist + B / 2.0);
                        targetCenter = new XYZ(targetCenter.X, targetCenter.Y, baseLevel.Elevation);
                        targetCenter += WallConfinementCalculator.CalculateTransversalAlignmentOffset(wall, columnType, dir);

                        if (!IsColumnNearby(targetCenter, zMin, zMax))
                        {
                            XYZ originInsertionPoint = targetCenter - rotatedLocalOffset;

                            plan.Columns.Add(new PlannedColumn
                            {
                                InsertionPoint = originInsertionPoint,
                                RotationAngle = angle,
                                BaseLevel = baseLevel,
                                PrimaryWall = wall,
                                ColumnType = baseColumnType
                            });

                            FamilyInstance col = doc.Create.NewFamilyInstance(originInsertionPoint, columnType, baseLevel, Autodesk.Revit.DB.Structure.StructuralType.Column);
                            WallConfinementCalculator.ApplyColumnetaConstraints(col, wall);
                            doc.Regenerate();
                            
                            if (Math.Abs(angle) > 0.001)
                            {
                                Line axis = Line.CreateBound(originInsertionPoint, originInsertionPoint + XYZ.BasisZ);
                                ElementTransformUtils.RotateElement(doc, col.Id, axis, angle);
                                doc.Regenerate();
                            }

                            ColumnLayoutPlanner.RegisterColumn(col);
                            createdColumns.Add(col);
                            createdColumnPoints.Add(originInsertionPoint);
                        }
                        currentDist += xReal + B;
                    }
                }
            }

            // VIGUETAS
            if (baseFramingType != null)
            {
                var allCols = new List<FamilyInstance>(existingCols);
                allCols.AddRange(createdColumns);
                
                XYZ GetExactFacePoint(XYZ pt, XYZ dir, bool isStart)
                {
                    XYZ searchPt = isStart ? pt - dir * 0.02 : pt + dir * 0.02; 
                    foreach (var col in allCols)
                    {
                        var bb = col.get_BoundingBox(null);
                        if (bb != null &&
                            searchPt.X >= bb.Min.X - 0.1 && searchPt.X <= bb.Max.X + 0.1 &&
                            searchPt.Y >= bb.Min.Y - 0.1 && searchPt.Y <= bb.Max.Y + 0.1)
                        {
                            XYZ extremePt = RealGeometryHelper.GetExtremePoint(col, doc, isStart ? dir : -dir);
                            if (extremePt != null)
                            {
                                IntersectionResult res = Line.CreateUnbound(pt, dir).Project(extremePt);
                                if (res != null) return res.XYZPoint;
                            }
                        }
                    }
                    return pt;
                }

                foreach (var wall in processedWalls)
                {
                    var segments = WallSegmentAnalyzer.GetSegments(doc, wall, existingCols);
                    if (!segments.Any()) continue;

                    var (zMin, zMax, baseLevel) = WallGeometryHelper.GetWallElevationInfo(doc, wall);
                    FamilySymbol framingType = FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(doc, baseFramingType, wall.Width);

                    ElementId topLevelId = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).AsElementId();
                    Level topLevel = topLevelId != ElementId.InvalidElementId ? doc.GetElement(topLevelId) as Level : null;
                    Level refLevel = topLevel ?? baseLevel;
                    double zOffset = (topLevel != null)
                        ? wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).AsDouble()
                        : wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET).AsDouble() + wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();

                    // VIGUETAS SUPERIORES (Por segmentos libres)
                    foreach (var segment in segments)
                    {
                        if (segment.StartPoint.DistanceTo(segment.EndPoint) < 0.1) continue;

                        if (!HasDuplicateBeam(segment.StartPoint, segment.EndPoint, refLevel.Elevation))
                        {
                            plan.TopBeams.Add(new PlannedBeam
                            {
                                StartPoint = new XYZ(segment.StartPoint.X, segment.StartPoint.Y, refLevel.Elevation),
                                EndPoint = new XYZ(segment.EndPoint.X, segment.EndPoint.Y, refLevel.Elevation),
                                BaseLevel = refLevel,
                                ZOffset = zOffset,
                                FramingType = baseFramingType,
                                ParentWall = wall
                            });
                        }
                    }

                    // VIGUETAS INFERIORES (Continua cruzando todo el muro)
                    XYZ botRawStart = segments.First().StartPoint;
                    XYZ botRawEnd = segments.Last().EndPoint;
                    XYZ botDir = (botRawEnd - botRawStart).Normalize();

                    XYZ continuousStart = GetExactFacePoint(botRawStart, botDir, true);
                    XYZ continuousEnd = GetExactFacePoint(botRawEnd, botDir, false);

                    if (continuousStart.DistanceTo(continuousEnd) >= 0.1)
                    {
                        if (!HasDuplicateBeam(continuousStart, continuousEnd, zMin))
                        {
                            plan.BottomBeams.Add(new PlannedBeam
                            {
                                StartPoint = new XYZ(continuousStart.X, continuousStart.Y, zMin),
                                EndPoint = new XYZ(continuousEnd.X, continuousEnd.Y, zMin),
                                BaseLevel = baseLevel,
                                ZOffset = 0.0,
                                FramingType = baseFramingType,
                                ParentWall = wall
                            });
                        }
                    }
                }
            }

            subT.RollBack();
        }
        
        LogStep($"Planificación completada: {plan.Columns.Count} columnetas, {plan.TopBeams.Count} viguetas sup, {plan.BottomBeams.Count} viguetas inf, {plan.WallCuts.Count} cortes.");
        return plan;
    }
}
