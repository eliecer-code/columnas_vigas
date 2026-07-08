using System.Windows;
using CQIng.Revit.ColumnasVigasMuros.UI.ViewModels;

namespace CQIng.Revit.ColumnasVigasMuros.UI;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        // Fuerza la carga de HelixToolkit.Wpf.dll en el AssemblyLoadContext de Revit
        // antes de que el XamlReader de WPF intente resolverlo y falle.
        _ = new HelixToolkit.Wpf.HelixViewport3D();

        InitializeComponent();
        DataContext = viewModel;
    }
}
