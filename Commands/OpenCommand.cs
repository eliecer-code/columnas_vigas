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
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiApplication = commandData.Application;

        if (uiApplication.ActiveUIDocument?.Document is null)
        {
            message = "Abre un documento de Revit.";
            return Result.Failed;
        }

        string docPath = uiApplication.ActiveUIDocument.Document.PathName;
        
        AppServiceProvider.Initialize(docPath, uiApplication);

        if (AppServiceProvider.ServiceProvider is not null)
        {
            var mainWindow = AppServiceProvider.ServiceProvider.GetRequiredService<MainWindow>();
            
            // Establecer la ventana principal de Revit como propietaria
            System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
            helper.Owner = uiApplication.MainWindowHandle;
            
            mainWindow.Show();
        }

        return Result.Succeeded;
    }
}
