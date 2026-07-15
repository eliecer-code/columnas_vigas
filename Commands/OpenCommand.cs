using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CQIng.Revit.ColumnasVigasMuros.App;
using CQIng.Revit.ColumnasVigasMuros.UI;
using Microsoft.Extensions.DependencyInjection;

namespace CQIng.Revit.ColumnasVigasMuros.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class OpenCommand : IExternalCommand
{
    private static MainWindow _currentWindow;

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiApplication = commandData.Application;

        if (uiApplication.ActiveUIDocument?.Document is null)
        {
            message = "Abre un documento de Revit.";
            return Result.Failed;
        }

        // Si la ventana ya existe y está abierta
        if (_currentWindow != null && _currentWindow.IsLoaded)
        {
            // Restaurar si está minimizada
            if (_currentWindow.WindowState == System.Windows.WindowState.Minimized)
            {
                _currentWindow.WindowState = System.Windows.WindowState.Normal;
            }
            
            // Traer al frente y enfocar
            _currentWindow.Activate();
            _currentWindow.Focus();
            
            return Result.Succeeded;
        }

        string docPath = uiApplication.ActiveUIDocument.Document.PathName;
        
        AppServiceProvider.Initialize(docPath, uiApplication);

        if (AppServiceProvider.ServiceProvider is not null)
        {
            _currentWindow = AppServiceProvider.ServiceProvider.GetRequiredService<MainWindow>();
            
            // Liberar la referencia cuando el usuario cierre la ventana
            _currentWindow.Closed += (s, e) => _currentWindow = null;
            
            // Establecer la ventana principal de Revit como propietaria
            System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(_currentWindow);
            helper.Owner = uiApplication.MainWindowHandle;
            
            _currentWindow.Show();
        }

        return Result.Succeeded;
    }
}
