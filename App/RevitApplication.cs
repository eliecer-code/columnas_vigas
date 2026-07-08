using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using CQIng.Revit.ColumnasVigasMuros.Commands;
using CQIng.Revit.ColumnasVigasMuros.Core;

namespace CQIng.Revit.ColumnasVigasMuros.App;

public sealed class RevitApplication : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        RevitEventExecutor.Initialize();
        CreateRibbon(application);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }

    private static void CreateRibbon(UIControlledApplication application)
    {
        TryCreateRibbonTab(application, AppConstants.RibbonTabName);

        RibbonPanel panel = GetOrCreateRibbonPanel(application);

        if (RibbonButtonExists(panel, AppConstants.CommandName))
        {
            return;
        }

        string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
        string commandClassName = typeof(OpenCommand).FullName!;

        PushButtonData buttonData = new(
            AppConstants.CommandName,
            AppConstants.RibbonButtonText,
            thisAssemblyPath,
            commandClassName
        )
        {
            ToolTip = "Abre el generador de columnetas y viguetas en muros.",
        };

        string assemblyDir = System.IO.Path.GetDirectoryName(thisAssemblyPath) ?? AppContext.BaseDirectory;
        string largeImagePath = System.IO.Path.Combine(assemblyDir, "Resources", "Logo_Muros.png");
        string smallImagePath = System.IO.Path.Combine(assemblyDir, "Resources", "Logo_Muros_32.png");

        if (System.IO.File.Exists(largeImagePath))
        {
            BitmapImage largeImage = new BitmapImage(new Uri(largeImagePath));
            largeImage.Freeze(); 
            buttonData.LargeImage = largeImage;
        }

        if (System.IO.File.Exists(smallImagePath))
        {
            BitmapImage smallImage = new BitmapImage(new Uri(smallImagePath));
            smallImage.Freeze(); 
            buttonData.Image = smallImage;
        }

        panel.AddItem(buttonData);
    }

    private static RibbonPanel GetOrCreateRibbonPanel(UIControlledApplication application)
    {
        foreach (RibbonPanel panel in application.GetRibbonPanels(AppConstants.RibbonTabName))
        {
            if (panel.Name == AppConstants.RibbonPanelName)
            {
                return panel;
            }
        }

        return application.CreateRibbonPanel(
            AppConstants.RibbonTabName,
            AppConstants.RibbonPanelName
        );
    }

    private static bool RibbonButtonExists(RibbonPanel panel, string buttonName)
    {
        foreach (RibbonItem item in panel.GetItems())
        {
            if (item.Name == buttonName)
            {
                return true;
            }
        }

        return false;
    }

    private static void TryCreateRibbonTab(UIControlledApplication application, string tabName)
    {
        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            
        }
    }
}
