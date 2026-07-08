using System;
using CQIng.Revit.ColumnasVigasMuros.Core.Application.Interfaces;
using CQIng.Revit.ColumnasVigasMuros.Services;
using CQIng.Revit.ColumnasVigasMuros.UI.ViewModels;
using CQIng.Revit.ColumnasVigasMuros.UI;
using Microsoft.Extensions.DependencyInjection;

namespace CQIng.Revit.ColumnasVigasMuros.App;

public static class AppServiceProvider
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    public static void Initialize(
        string revitDocumentPath,
        Autodesk.Revit.UI.UIApplication uiApplication
    )
    {
        ServiceCollection services = new();

        services.AddSingleton(uiApplication);

        services.AddTransient<IRevitDataExtractionService, RevitDataExtractionService>();
        services.AddTransient<IRevitSelectionService, RevitSelectionService>();
        services.AddTransient<IElementGenerationService, ElementGenerationService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        ServiceProvider = services.BuildServiceProvider();
    }
}
