# Plantilla Base para Add-ins de Revit (WPF + MVVM)

Esta es una plantilla independiente y genérica diseñada para acelerar el desarrollo de nuevos Add-ins de Revit utilizando C#, WPF y el patrón MVVM.

## Características Incluidas
* **Arquitectura Limpia:** Separación clara entre `Core`, `UI`, `Services` e `Infrastructure`.
* **Patrón MVVM:** Configurado con `CommunityToolkit.Mvvm` para una gestión eficiente del estado y comandos.
* **Ribbon Preconfigurado:** Código base para inyectar botones, paneles e iconos directamente en la interfaz de Revit.
* **Manejo de Entorno:** Integración de carga de configuraciones desde archivos `.env`.
* **Cliente API:** Estructura base para conectar el Add-in con backends externos.
* **Generación Automática del Manifiesto:** El script de MSBuild en el archivo `.csproj` autogenera e instala el `.addin` en el directorio de Revit al compilar.

## ¿Cómo iniciar un proyecto?
Por favor, lee el archivo [TEMPLATE_SETUP.md](TEMPLATE_SETUP.md) para ver la guía paso a paso sobre cómo clonar, renombrar y configurar esta plantilla para tu nuevo Add-in.

## Requisitos Previos
* Revit 2026 (o modificar el `.csproj` para tu versión objetivo).
* .NET 8.0 SDK.
* Visual Studio 2022 o VS Code.
