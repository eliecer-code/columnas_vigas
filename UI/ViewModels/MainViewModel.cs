using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CQIng.Revit.ColumnasVigasMuros.Core;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;
using CQIng.Revit.ColumnasVigasMuros.Services;

namespace CQIng.Revit.ColumnasVigasMuros.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IRevitDataExtractionService _dataExtractionService;
    private readonly IRevitSelectionService _selectionService;
    private readonly IElementGenerationService _generationService;
    private readonly Dispatcher _dispatcher;

    // Estado para regenerar la previsualización sin re-seleccionar muros
    private List<Wall> _cachedRevitWalls = new();
    private UIApplication _uiapp;

    [ObservableProperty]
    private ObservableCollection<WallDataModel> _selectedWalls = new();

    [ObservableProperty]
    private ObservableCollection<DropdownItem> _columnTypes = new();

    [ObservableProperty]
    private DropdownItem _selectedColumnType;

    [ObservableProperty]
    private ObservableCollection<DropdownItem> _framingTypes = new();

    [ObservableProperty]
    private DropdownItem _selectedFramingType;

    [ObservableProperty]
    private Model3DGroup _modelGroup = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _generateColumns = true;

    [ObservableProperty]
    private bool _generateTopBeams = true;

    [ObservableProperty]
    private bool _generateBottomBeams = true;

    [ObservableProperty]
    private bool _useTopBeamOffset = false;

    /// <summary>Desfase desde la coronación del muro, en metros. Solo activo cuando UseTopBeamOffset es true.</summary>
    [ObservableProperty]
    private double _topBeamVerticalOffset = 0.0;

    public MainViewModel(
        IRevitDataExtractionService dataExtractionService,
        IRevitSelectionService selectionService,
        IElementGenerationService generationService
    )
    {
        _dataExtractionService = dataExtractionService;
        _selectionService = selectionService;
        _generationService = generationService;
        _dispatcher = Dispatcher.CurrentDispatcher;

        LoadData();
    }

    private void LoadData()
    {
        RevitEventExecutor.Execute(app =>
        {
            var doc = app.ActiveUIDocument.Document;

            var columns = _dataExtractionService.GetStructuralColumnTypes(doc);
            var framings = _dataExtractionService.GetStructuralFramingTypes(doc);

            _dispatcher.Invoke(() =>
            {
                ColumnTypes = new ObservableCollection<DropdownItem>(columns);
                FramingTypes = new ObservableCollection<DropdownItem>(framings);

                SelectedColumnType = ColumnTypes.FirstOrDefault();
                SelectedFramingType = FramingTypes.FirstOrDefault();
            });
        });
    }

    [RelayCommand]
    private void SelectWalls()
    {
        RevitEventExecutor.Execute(app =>
        {
            try
            {
                var walls = _selectionService.PromptWallSelection(app);

                var wallGeomGroup = new Model3DGroup();
                var doc = app.ActiveUIDocument.Document;
                var revitWalls = new List<Wall>();

                foreach (var wallModel in walls)
                {
                    var wall = doc.GetElement(new ElementId(wallModel.Id)) as Wall;
                    if (wall != null)
                    {
                        revitWalls.Add(wall);
                        var geom = GeometryHelper.GetWallGeometryModel(wall);
                        if (geom != null)
                            wallGeomGroup.Children.Add(geom);
                    }
                }

                wallGeomGroup.Children.Add(new AmbientLight(System.Windows.Media.Colors.White));

                // Guardar referencias para la previsualización automática
                _cachedRevitWalls = revitWalls;
                _uiapp = app;

                _dispatcher.Invoke(() =>
                {
                    SelectedWalls = new ObservableCollection<WallDataModel>(walls);
                });

                // Generar previsualización inmediata tras selección
                RefreshPreview(app);
            }
            catch (Exception ex)
            {
                _dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Error seleccionando muros: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                });
            }
        });
    }

    /// <summary>
    /// Regenera la geometría del visor 3D combinando los muros con la previsualización estructural.
    /// No crea ningún elemento en Revit. No inicia transacciones.
    /// </summary>
    private void RefreshPreview(UIApplication app = null)
    {
        app ??= _uiapp;
        if (app == null || !_cachedRevitWalls.Any()) return;

        var doc = app.ActiveUIDocument.Document;

        // Opciones actuales del ViewModel
        var options = new CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities.GenerationOptions
        {
            GenerateColumns      = this.GenerateColumns,
            GenerateTopBeams     = this.GenerateTopBeams,
            GenerateBottomBeams  = this.GenerateBottomBeams,
            UseTopBeamOffset     = this.UseTopBeamOffset,
            TopBeamVerticalOffsetMeters = this.TopBeamVerticalOffset
        };

        // Obtener los tipos de familia seleccionados (pueden ser null si aún no se eligieron)
        FamilySymbol colType = SelectedColumnType != null
            ? doc.GetElement(new ElementId(SelectedColumnType.Id)) as FamilySymbol
            : null;
        FamilySymbol framingType = SelectedFramingType != null
            ? doc.GetElement(new ElementId(SelectedFramingType.Id)) as FamilySymbol
            : null;

        // Si no hay tipos seleccionados aún, solo mostrar los muros
        if (colType == null || framingType == null)
        {
            _dispatcher.Invoke(() => ModelGroup = BuildWallsOnly(doc));
            return;
        }

        try
        {
            CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities.StructuralPlan plan = null;

            // StructuralPlannerService.CreatePlan requiere una SubTransaction internamente.
            // Para iniciar una SubTransaction, DEBE haber una Transaction principal activa.
            // Iniciamos una transacción temporal solo para planificación y luego hacemos RollBack.
            using (Transaction t = new Transaction(doc, "Preview Temporal"))
            {
                t.Start();
                plan = CQIng.Revit.ColumnasVigasMuros.Services.StructuralPlannerService.CreatePlan(
                    doc, _cachedRevitWalls, colType, framingType, options);
                t.RollBack(); // NUNCA confirmar, es solo vista previa
            }

            // Construir la geometría de preview
            var previewGroup = CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers
                .StructuralPreviewBuilder.Build(plan, options, doc);

            // Combinar muros + preview en un solo Model3DGroup
            var combined = BuildWallsOnly(doc);
            foreach (var child in previewGroup.Children)
                combined.Children.Add(child);

            _dispatcher.Invoke(() => ModelGroup = combined);
        }
        catch (Exception)
        {
            // Si el cálculo falla (ej. desfase inválido) mostrar solo los muros sin romper la UI
            _dispatcher.Invoke(() => ModelGroup = BuildWallsOnly(doc));
        }
    }

    private Model3DGroup BuildWallsOnly(Document doc)
    {
        var g = new Model3DGroup();
        foreach (var wall in _cachedRevitWalls)
        {
            var geom = GeometryHelper.GetWallGeometryModel(wall);
            if (geom != null) g.Children.Add(geom);
        }
        g.Children.Add(new AmbientLight(System.Windows.Media.Colors.White));
        return g;
    }

    // ─── Actualizaciones automáticas al cambiar opciones ─────────────────────

    partial void OnGenerateColumnsChanged(bool value)     => TriggerPreviewRefresh();
    partial void OnGenerateTopBeamsChanged(bool value)    => TriggerPreviewRefresh();
    partial void OnGenerateBottomBeamsChanged(bool value) => TriggerPreviewRefresh();
    partial void OnUseTopBeamOffsetChanged(bool value)    => TriggerPreviewRefresh();
    partial void OnTopBeamVerticalOffsetChanged(double v) => TriggerPreviewRefresh();
    partial void OnSelectedColumnTypeChanged(DropdownItem v)  => TriggerPreviewRefresh();
    partial void OnSelectedFramingTypeChanged(DropdownItem v) => TriggerPreviewRefresh();

    private void TriggerPreviewRefresh()
    {
        if (!_cachedRevitWalls.Any()) return;
        RevitEventExecutor.Execute(app => RefreshPreview(app));
    }

    [RelayCommand]
    private void Generate()
    {
        if (!SelectedWalls.Any())
        {
            MessageBox.Show(
                "Debes seleccionar muros primero.",
                "Advertencia",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        if (!GenerateColumns && !GenerateTopBeams && !GenerateBottomBeams)
        {
            MessageBox.Show(
                "Seleccione al menos un elemento para generar.",
                "Advertencia",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        if (GenerateColumns && SelectedColumnType == null)
        {
            MessageBox.Show(
                "Debes seleccionar un tipo de columneta para generar columnetas.",
                "Advertencia",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        if ((GenerateTopBeams || GenerateBottomBeams) && SelectedFramingType == null)
        {
            MessageBox.Show(
                "Debes seleccionar un tipo de vigueta para generar viguetas.",
                "Advertencia",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            return;
        }

        // Validar desfase de vigueta superior
        if (UseTopBeamOffset)
        {
            if (TopBeamVerticalOffset < 0)
            {
                MessageBox.Show(
                    "El desfase vertical no puede ser negativo.",
                    "Advertencia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            // La validación de que el desfase no supera la altura del muro se realiza
            // en el servicio de generación porque cada muro tiene su propia altura.
            // Aquí solo comprobamos que el valor es un número positivo.
        }

        IsBusy = true;

        RevitEventExecutor.Execute(app =>
        {
            try
            {
                var doc = app.ActiveUIDocument.Document;
                
                // 1. VALIDACIÓN PREVIA DE DUPLICADOS (Evitar recrear en muros ya procesados)
                var validationReport = CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers
                    .ExistingElementsValidator.Validate(doc, SelectedWalls.ToList());
                    
                if (validationReport.HasExistingElements)
                {
                    _dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            validationReport.FormattedMessage,
                            "Elementos Existentes Detectados",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                        IsBusy = false;
                    });
                    return; // Cancelar ejecución sin iniciar transacciones
                }

                var options = new GenerationOptions
                {
                    GenerateColumns = this.GenerateColumns,
                    GenerateTopBeams = this.GenerateTopBeams,
                    GenerateBottomBeams = this.GenerateBottomBeams,
                    UseTopBeamOffset = this.UseTopBeamOffset,
                    TopBeamVerticalOffsetMeters = this.TopBeamVerticalOffset
                };

                _generationService.GenerateElements(
                    app,
                    SelectedWalls.ToList(),
                    SelectedColumnType.Id,
                    SelectedFramingType.Id,
                    options
                );

                _dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        "Columnetas y viguetas generadas correctamente.",
                        "Éxito",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    IsBusy = false;
                });
            }
            catch (Exception ex)
            {
                _dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Error generando elementos: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    IsBusy = false;
                });
            }
        });
    }
}
