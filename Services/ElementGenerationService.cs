using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Services;

public class WarningSwallower : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
        if (fmas.Count == 0)
            return FailureProcessingResult.Continue;

        foreach (FailureMessageAccessor fma in fmas)
        {
            if (fma.GetSeverity() == FailureSeverity.Warning)
            {
                failuresAccessor.DeleteWarning(fma);
            }
        }
        return FailureProcessingResult.Continue;
    }
}

public class ElementGenerationService : IElementGenerationService
{
    private void LogStep(string step)
    {
        try
        {
            string path = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "CQIng_Diagnostic.log"
            );
            System.IO.File.AppendAllText(
                path,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {step}\n"
            );
        }
        catch { }
    }

    public void GenerateElements(
        UIApplication uiapp,
        List<WallDataModel> selectedWalls,
        long columnTypeId,
        long framingTypeId
    )
    {
        UIDocument uidoc = uiapp.ActiveUIDocument;
        Document doc = uidoc.Document;
        string currentStep = "Inicio del comando.";
        LogStep(currentStep);

        try
        {
            currentStep = "Validaciones previas (Listas y nulos)";
            LogStep(currentStep);
            if (selectedWalls == null || !selectedWalls.Any())
                throw new InvalidOperationException("No se seleccionaron muros.");

            currentStep = "Inicio de TransactionGroup.";
            LogStep(currentStep);
            using (
                TransactionGroup transGroup = new TransactionGroup(
                    doc,
                    "Generar Columnetas y Viguetas"
                )
            )
            {
                transGroup.Start();

                currentStep = "Familia de columneta seleccionada";
                LogStep(currentStep);
                FamilySymbol baseColumnType =
                    doc.GetElement(new ElementId(columnTypeId)) as FamilySymbol;
                if (baseColumnType == null)
                    throw new InvalidOperationException(
                        $"El id {columnTypeId} no corresponde a un FamilySymbol válido."
                    );

                if (!baseColumnType.IsActive)
                {
                    using (
                        Transaction tActivate = new Transaction(doc, "Activar Símbolo de Columneta")
                    )
                    {
                        tActivate.Start();
                        baseColumnType.Activate();
                        tActivate.Commit();
                    }
                }

                currentStep = "Paso 9: Familia de vigueta seleccionada";
                FamilySymbol baseFramingType =
                    doc.GetElement(new ElementId(framingTypeId)) as FamilySymbol;
                if (baseFramingType != null && !baseFramingType.IsActive)
                {
                    using (
                        Transaction tActivate = new Transaction(doc, "Activar Símbolo de Armazón")
                    )
                    {
                        tActivate.Start();
                        baseFramingType.Activate();
                        tActivate.Commit();
                    }
                }

                currentStep = "Inicio de Transaction.";
                LogStep(currentStep);
                using (Transaction t = new Transaction(doc, "Generar Elementos Estructurales"))
                {
                    FailureHandlingOptions options = t.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(new WarningSwallower());
                    t.SetFailureHandlingOptions(options);

                    t.Start();

                    List<FamilyInstance> createdColumns = new List<FamilyInstance>();
                    List<FamilyInstance> createdFramings = new List<FamilyInstance>();
                    List<Wall> processedWalls = new List<Wall>();

                    var existingCols = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .WherePasses(
                            new LogicalOrFilter(
                                new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                                new ElementCategoryFilter(BuiltInCategory.OST_Columns)
                            )
                        )
                        .ToList();

                    List<XYZ> createdColumnPoints = new List<XYZ>();
                    Dictionary<ElementId, Curve> originalCurves =
                        new Dictionary<ElementId, Curve>();

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

                    foreach (var wModel in selectedWalls)
                    {
                        currentStep = $"Obtención del muro {wModel.Id}";
                        LogStep(currentStep);
                        Wall wall = doc.GetElement(new ElementId(wModel.Id)) as Wall;
                        if (wall == null)
                            throw new InvalidOperationException(
                                $"El muro con Id {wModel.Id} no existe o no es de tipo Wall."
                            );

                        currentStep = $"Lectura de la LocationCurve del muro {wModel.Id}";
                        LogStep(currentStep);
                        LocationCurve locCurve = wall.Location as LocationCurve;
                        if (locCurve == null)
                            throw new InvalidOperationException(
                                $"El muro {wModel.Id} no tiene LocationCurve."
                            );

                        originalCurves[wall.Id] = locCurve.Curve;

                        currentStep = $"Dimensiones del muro {wModel.Id}";
                        LogStep(currentStep);
                        var (zMinW, zMaxW, bLevelW) = WallGeometryHelper.GetWallElevationInfo(
                            doc,
                            wall
                        );
                        if (bLevelW == null)
                            throw new InvalidOperationException(
                                $"El muro {wModel.Id} no tiene un nivel base válido."
                            );
                        if (wall.Width <= 0)
                            throw new InvalidOperationException(
                                $"El muro {wModel.Id} tiene un espesor inválido."
                            );
                        if (zMaxW <= zMinW)
                            throw new InvalidOperationException(
                                $"El muro {wModel.Id} tiene una altura inválida."
                            );
                        if (locCurve.Curve.Length <= 0)
                            throw new InvalidOperationException(
                                $"El muro {wModel.Id} tiene una longitud inválida."
                            );

                        processedWalls.Add(wall);
                    }

                    List<CornerNode> nodes = CornerNodeSolver.BuildTopologicalNodes(
                        doc,
                        processedWalls
                    );

                    currentStep = "Subtransacción para medir altura de vigueta superior";
                    double topBeamHeight = 0;
                    if (baseFramingType != null && processedWalls.Any())
                    {
                        Wall sampleWall = processedWalls.First();
                        var (zwMin, zwMax, bLevel) = WallGeometryHelper.GetWallElevationInfo(
                            doc,
                            sampleWall
                        );
                        double wThickness = sampleWall.Width;
                        FamilySymbol tempFramingType =
                            FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(
                                doc,
                                baseFramingType,
                                wThickness
                            );

                        using (SubTransaction subT = new SubTransaction(doc))
                        {
                            subT.Start();
                            XYZ pt0 = new XYZ(0, 0, zwMax);
                            XYZ pt1 = new XYZ(10, 0, zwMax);
                            Line tempLine = Line.CreateBound(pt0, pt1);
                            FamilyInstance tempBeam = doc.Create.NewFamilyInstance(
                                tempLine,
                                tempFramingType,
                                bLevel,
                                Autodesk.Revit.DB.Structure.StructuralType.Beam
                            );

                            tempBeam.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE)?.Set(0.0);
                            tempBeam.get_Parameter(BuiltInParameter.Y_JUSTIFICATION)?.Set(1);
                            tempBeam.get_Parameter(BuiltInParameter.Z_JUSTIFICATION)?.Set(0);
                            doc.Regenerate();

                            var bounds = RealGeometryHelper.GetSolidBounds(tempBeam, doc);
                            if (bounds.IsValid)
                            {
                                topBeamHeight = bounds.MaxZ - bounds.MinZ;
                            }
                            subT.RollBack();
                        }
                    }

                    int colIndex = 1;
                    foreach (var node in nodes)
                    {
                        if (!node.ConnectedWalls.Any())
                            continue;

                        currentStep = $"Cálculo de la topología para nodo {colIndex}";
                        LogStep(currentStep);

                        Wall primaryWallBase = node
                            .ConnectedWalls.OrderByDescending(w => w.Thickness)
                            .First()
                            .Wall;
                        var (zMin, zMax, baseLevel) = WallGeometryHelper.GetWallElevationInfo(
                            doc,
                            primaryWallBase
                        );

                        if (IsColumnNearby(node.Point, zMin, zMax))
                            continue;

                        double wallThickness = primaryWallBase.Width;
                        FamilySymbol columnType = FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(
                            doc,
                            baseColumnType,
                            wallThickness
                        );

                        currentStep = $"1. Resolviendo geometría del nodo {colIndex}";
                        LogStep(currentStep);

                        CornerNodeSolver.SolveNodeGeometry(node, columnType, baseLevel.Elevation);

                        currentStep = $"2. Reducción matemática de muros en nodo {colIndex}";
                        LogStep(currentStep);

                        foreach (var nw in node.ConnectedWalls)
                        {
                            double cutLength = node.WallCutLengths[nw.Wall.Id];

                            LocationCurve lcW = nw.Wall.Location as LocationCurve;
                            Curve currentCurve = lcW.Curve;
                            XYZ p0 = currentCurve.GetEndPoint(0);
                            XYZ p1 = currentCurve.GetEndPoint(1);

                            XYZ newEnd = node.Point + nw.InwardDir * cutLength;

                            if (nw.IsStart)
                                p0 = newEnd;
                            else
                                p1 = newEnd;

                            if (p0.DistanceTo(p1) > 0.5)
                            {
                                WallUtils.DisallowWallJoinAtEnd(nw.Wall, nw.IsStart ? 0 : 1);
                                lcW.Curve = Line.CreateBound(p0, p1);
                            }
                        }
                        doc.Regenerate();

                        currentStep = $"4. Calcular Punto de Inserción para nodo {colIndex}";
                        LogStep(currentStep);

                        double angle = node.RotationAngle;

                        // Obtener la dirección original del muro para el cálculo transversal
                        LocationCurve lcPrimary = primaryWallBase.Location as LocationCurve;
                        XYZ p0Prim = lcPrimary.Curve.GetEndPoint(0);
                        XYZ p1Prim = lcPrimary.Curve.GetEndPoint(1);
                        XYZ wallDirOrig = (p1Prim - p0Prim).Normalize();

                        XYZ transOffset =
                            WallConfinementCalculator.CalculateTransversalAlignmentOffset(
                                primaryWallBase,
                                columnType,
                                wallDirOrig
                            );

                        XYZ adjustedTargetCenter = node.TargetCenter + transOffset;

                        // Desplazamiento dinámico del origen real de la familia
                        XYZ localOriginOffset = WallConfinementCalculator.GetFamilyOriginOffset(
                            columnType
                        );
                        XYZ rotatedLocalOffset = Transform
                            .CreateRotation(XYZ.BasisZ, angle)
                            .OfVector(localOriginOffset);
                        XYZ originInsertionPoint = adjustedTargetCenter - rotatedLocalOffset;

                        currentStep = $"Creación de columneta en nodo {colIndex}";
                        LogStep(currentStep);
                        FamilyInstance col = doc.Create.NewFamilyInstance(
                            originInsertionPoint,
                            columnType,
                            baseLevel,
                            Autodesk.Revit.DB.Structure.StructuralType.Column
                        );

                        WallConfinementCalculator.ApplyColumnetaConstraints(col, primaryWallBase);

                        currentStep = $"Regeneración del documento 1 columneta en nodo {colIndex}";
                        LogStep(currentStep);
                        doc.Regenerate();

                        currentStep =
                            $"Alineación geométrica y rotación columneta en nodo {colIndex}";
                        LogStep(currentStep);

                        if (Math.Abs(angle) > 0.001)
                        {
                            Line axis = Line.CreateBound(
                                originInsertionPoint,
                                originInsertionPoint + XYZ.BasisZ
                            );
                            ElementTransformUtils.RotateElement(doc, col.Id, axis, angle);
                            doc.Regenerate();
                        }

                        currentStep = $"Fase 5: Validación Topológica Estricta en nodo {colIndex}";
                        LogStep(currentStep);

                        XYZ dirPrim = node.PrimaryWall.InwardDir;

                        XYZ finalColFront = RealGeometryHelper.GetExtremePoint(col, doc, -dirPrim);
                        XYZ finalColBack = RealGeometryHelper.GetExtremePoint(col, doc, dirPrim);

                        XYZ normalDir = XYZ.BasisZ.CrossProduct(dirPrim).Normalize();
                        XYZ finalColLeft = RealGeometryHelper.GetExtremePoint(col, doc, normalDir);
                        XYZ finalColRight = RealGeometryHelper.GetExtremePoint(
                            col,
                            doc,
                            -normalDir
                        );

                        if (
                            finalColFront != null
                            && finalColBack != null
                            && finalColLeft != null
                            && finalColRight != null
                        )
                        {
                            XYZ currentCenter =
                                (finalColFront + finalColBack + finalColLeft + finalColRight) / 4.0;
                            double distCenter = currentCenter.DistanceTo(adjustedTargetCenter);

                            if (distCenter > 0.01)
                            {
                                string logMsg =
                                    $"[VALIDACION FALLIDA NODO {colIndex}]\n"
                                    + $"Target Center: {adjustedTargetCenter}\n"
                                    + $"Current Center: {currentCenter}\n"
                                    + $"Distancia de Error: {distCenter:F4}";
                                LogStep(logMsg);
                            }
                            else
                            {
                                LogStep(
                                    $"[VALIDACION EXITOSA NODO {colIndex}] Geometría alineada perfectamente. Tolerancia < 0.01"
                                );
                            }
                        }

                        createdColumns.Add(col);
                        createdColumnPoints.Add(node.Point);
                        colIndex++;
                    }

                    currentStep = "Generación de columnetas intermedias";
                    LogStep(currentStep);

                    double xMax = 3.0 / 0.3048; // 3.00 m en pies

                    foreach (var wall in processedWalls)
                    {
                        LocationCurve lc = wall.Location as LocationCurve;
                        if (lc == null)
                            continue;

                        Curve curve = lc.Curve;
                        double L = curve.Length;

                        if (L <= xMax)
                            continue;

                        double wallThickness = wall.Width;
                        FamilySymbol columnType = FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(
                            doc,
                            baseColumnType,
                            wallThickness
                        );

                        var (colWidth, colThickness) =
                            WallConfinementCalculator.GetColumnetaDimensions(columnType, wall);
                        double B = colWidth;

                        double nVal = Math.Ceiling((L - xMax) / (xMax + B));
                        int N = (int)nVal;

                        if (N <= 0)
                            continue;

                        double xReal = (L - N * B) / (N + 1);

                        XYZ p0 = curve.GetEndPoint(0);
                        XYZ p1 = curve.GetEndPoint(1);
                        XYZ dir = (p1 - p0).Normalize();

                        var (zMin, zMax, baseLevel) = WallGeometryHelper.GetWallElevationInfo(
                            doc,
                            wall
                        );

                        // Utilizamos la misma lógica de orientación que las columnetas de esquina
                        double angle = WallConfinementCalculator.GetColumnetaRotationAngle(
                            dir,
                            columnType
                        );

                        XYZ localOriginOffset = WallConfinementCalculator.GetFamilyOriginOffset(
                            columnType
                        );
                        XYZ rotatedLocalOffset = Transform
                            .CreateRotation(XYZ.BasisZ, angle)
                            .OfVector(localOriginOffset);

                        double currentDist = xReal;
                        for (int i = 0; i < N; i++)
                        {
                            XYZ targetCenter = p0 + dir * (currentDist + B / 2.0);
                            targetCenter = new XYZ(
                                targetCenter.X,
                                targetCenter.Y,
                                baseLevel.Elevation
                            );
                            XYZ transOffset =
                                WallConfinementCalculator.CalculateTransversalAlignmentOffset(
                                    wall,
                                    columnType,
                                    dir
                                );
                            targetCenter += transOffset;

                            if (!IsColumnNearby(targetCenter, zMin, zMax))
                            {
                                currentStep = $"Creación de columneta intermedia en muro {wall.Id}";
                                LogStep(currentStep);

                                XYZ originInsertionPoint = targetCenter - rotatedLocalOffset;
                                FamilyInstance col = doc.Create.NewFamilyInstance(
                                    originInsertionPoint,
                                    columnType,
                                    baseLevel,
                                    Autodesk.Revit.DB.Structure.StructuralType.Column
                                );

                                // Aplicamos la misma lógica de restricciones (altura) que las columnetas de esquina
                                WallConfinementCalculator.ApplyColumnetaConstraints(col, wall);

                                doc.Regenerate();

                                if (Math.Abs(angle) > 0.001)
                                {
                                    Line axis = Line.CreateBound(
                                        originInsertionPoint,
                                        originInsertionPoint + XYZ.BasisZ
                                    );
                                    ElementTransformUtils.RotateElement(doc, col.Id, axis, angle);
                                    doc.Regenerate();
                                }

                                createdColumns.Add(col);
                                createdColumnPoints.Add(targetCenter);
                            }

                            currentDist += B + xReal;
                        }
                    }

                    if (baseFramingType != null)
                    {
                        foreach (var wall in processedWalls)
                        {
                            Curve curve = originalCurves[wall.Id];
                            XYZ startPt = curve.GetEndPoint(0);
                            XYZ endPt = curve.GetEndPoint(1);

                            var (zMin, zMax, baseLevel) = WallGeometryHelper.GetWallElevationInfo(
                                doc,
                                wall
                            );
                            double wallThickness = wall.Width;

                            FamilySymbol framingType =
                                FamilySymbolHelper.GetOrDuplicateSymbolWithWidth(
                                    doc,
                                    baseFramingType,
                                    wallThickness
                                );

                            currentStep = $"Creación de la vigueta superior para muro {wall.Id}";
                            LogStep(currentStep);

                            ElementId topLevelId = wall.get_Parameter(
                                    BuiltInParameter.WALL_HEIGHT_TYPE
                                )
                                .AsElementId();
                            Level topLevel =
                                topLevelId != ElementId.InvalidElementId
                                    ? doc.GetElement(topLevelId) as Level
                                    : null;
                            Level refLevel = topLevel ?? baseLevel;

                            double zOffset =
                                (topLevel != null)
                                    ? wall.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET)
                                        .AsDouble()
                                    : wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)
                                        .AsDouble()
                                        + wall.get_Parameter(
                                                BuiltInParameter.WALL_USER_HEIGHT_PARAM
                                            )
                                            .AsDouble();

                            XYZ ptTop0 = new XYZ(startPt.X, startPt.Y, refLevel.Elevation);
                            XYZ ptTop1 = new XYZ(endPt.X, endPt.Y, refLevel.Elevation);
                            Line topBeamLine = Line.CreateBound(ptTop0, ptTop1);

                            FamilyInstance topFraming = doc.Create.NewFamilyInstance(
                                topBeamLine,
                                framingType,
                                refLevel,
                                Autodesk.Revit.DB.Structure.StructuralType.Beam
                            );

                            topFraming.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE)?.Set(zOffset);
                            topFraming
                                .get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION)
                                ?.Set(0.0);
                            topFraming
                                .get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION)
                                ?.Set(0.0);
                            topFraming.get_Parameter(BuiltInParameter.Y_JUSTIFICATION)?.Set(1);
                            topFraming.get_Parameter(BuiltInParameter.Z_JUSTIFICATION)?.Set(0);

                            createdFramings.Add(topFraming);

                            currentStep = $"Creación de la vigueta inferior para muro {wall.Id}";
                            LogStep(currentStep);
                            XYZ ptBot0 = new XYZ(startPt.X, startPt.Y, zMin);
                            XYZ ptBot1 = new XYZ(endPt.X, endPt.Y, zMin);
                            Line botBeamLine = Line.CreateBound(ptBot0, ptBot1);

                            FamilyInstance botFraming = doc.Create.NewFamilyInstance(
                                botBeamLine,
                                framingType,
                                baseLevel,
                                Autodesk.Revit.DB.Structure.StructuralType.Beam
                            );

                            botFraming.get_Parameter(BuiltInParameter.Z_OFFSET_VALUE)?.Set(0.0);
                            botFraming
                                .get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION)
                                ?.Set(0.0);
                            botFraming
                                .get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION)
                                ?.Set(0.0);
                            botFraming.get_Parameter(BuiltInParameter.Y_JUSTIFICATION)?.Set(1);
                            botFraming.get_Parameter(BuiltInParameter.Z_JUSTIFICATION)?.Set(0);

                            createdFramings.Add(botFraming);
                        }
                    }

                    currentStep = "Regeneración antes de Cut/Join";
                    LogStep(currentStep);
                    doc.Regenerate();

                    currentStep = "Ejecutando JoinAndCut Columnetas";
                    LogStep(currentStep);
                    foreach (var col in createdColumns)
                    {
                        var intFramings = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_StructuralFraming)
                            .WhereElementIsNotElementType()
                            .WherePasses(new ElementIntersectsElementFilter(col))
                            .ToList();
                        foreach (var frm in intFramings)
                            JoinAndCut(doc, cutter: col, cuttee: frm);

                        var intWalls = new FilteredElementCollector(doc)
                            .OfClass(typeof(Wall))
                            .WherePasses(new ElementIntersectsElementFilter(col))
                            .ToList();
                        foreach (var w in intWalls)
                            JoinAndCut(doc, cutter: col, cuttee: w);
                    }

                    currentStep = "Ejecutando JoinAndCut Viguetas";
                    LogStep(currentStep);
                    foreach (var framing in createdFramings)
                    {
                        var intWalls = new FilteredElementCollector(doc)
                            .OfClass(typeof(Wall))
                            .WherePasses(new ElementIntersectsElementFilter(framing))
                            .ToList();
                        foreach (var wall in intWalls)
                            JoinAndCut(doc, cutter: framing, cuttee: wall);

                        var intCols = new FilteredElementCollector(doc)
                            .WherePasses(
                                new LogicalOrFilter(
                                    new ElementCategoryFilter(
                                        BuiltInCategory.OST_StructuralColumns
                                    ),
                                    new ElementCategoryFilter(BuiltInCategory.OST_Columns)
                                )
                            )
                            .WhereElementIsNotElementType()
                            .WherePasses(new ElementIntersectsElementFilter(framing))
                            .ToList();
                        foreach (var c in intCols)
                            JoinAndCut(doc, cutter: c, cuttee: framing);
                    }

                    currentStep = "Commit.";
                    LogStep(currentStep);
                    t.Commit();
                }

                currentStep = "Assimilate.";
                LogStep(currentStep);
                transGroup.Assimilate();
            }
        }
        catch (Exception ex)
        {
            LogStep($"EXCEPTION INTERCEPTADA: {ex.GetType().FullName} | {ex.Message}");
            TaskDialog td = new TaskDialog("Error Diagnóstico Crítico (Ponytail FASE 2)");
            td.MainInstruction = "Excepción interceptada en GenerateElements";
            td.MainContent =
                $"Paso actual: {currentStep}\n\n"
                + $"Mensaje: {ex.Message}\n\n"
                + $"Tipo: {ex.GetType().FullName}\n\n"
                + $"Inner Exception: {ex.InnerException?.Message ?? "Ninguna"}\n\n"
                + $"Stack Trace:\n{ex.StackTrace}";
            td.Show();
        }
    }

    private void JoinAndCut(Document doc, Element cutter, Element cuttee)
    {
        if (cutter == null || cuttee == null || cutter.Id == cuttee.Id)
            return;

        try
        {
            if (JoinGeometryUtils.AreElementsJoined(doc, cutter, cuttee))
            {
                if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, cutter, cuttee))
                    JoinGeometryUtils.SwitchJoinOrder(doc, cuttee, cutter);
            }
            else
            {
                JoinGeometryUtils.JoinGeometry(doc, cutter, cuttee);
                if (JoinGeometryUtils.AreElementsJoined(doc, cutter, cuttee))
                {
                    if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, cutter, cuttee))
                        JoinGeometryUtils.SwitchJoinOrder(doc, cuttee, cutter);
                }
            }
        }
        catch { }
    }
}
