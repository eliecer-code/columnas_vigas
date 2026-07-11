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

                var newModelGroup = new Model3DGroup();
                var doc = app.ActiveUIDocument.Document;

                foreach (var wallModel in walls)
                {
                    var wall = doc.GetElement(new ElementId(wallModel.Id)) as Wall;
                    if (wall != null)
                    {
                        var geom = GeometryHelper.GetWallGeometryModel(wall);
                        if (geom != null)
                        {
                            newModelGroup.Children.Add(geom);
                        }
                    }
                }

                newModelGroup.Children.Add(new AmbientLight(System.Windows.Media.Colors.White));

                _dispatcher.Invoke(() =>
                {
                    SelectedWalls = new ObservableCollection<WallDataModel>(walls);
                    ModelGroup = newModelGroup;
                });
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

        IsBusy = true;

        RevitEventExecutor.Execute(app =>
        {
            try
            {
                var options = new GenerationOptions
                {
                    GenerateColumns = this.GenerateColumns,
                    GenerateTopBeams = this.GenerateTopBeams,
                    GenerateBottomBeams = this.GenerateBottomBeams
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
