# CQIng - Add-in de Columnetas y Viguetas de Muros para Revit 2026

Este es un Add-in para Revit 2026 desarrollado en C# (.NET 8) que automatiza la creación de elementos de mampostería confinada sobre muros estructurales, específicamente **columnetas** y **viguetas** (superiores e inferiores).

## Características Principales

*   **Creación Automática de Columnetas**: Genera columnetas de esquina, uniones en T y columnetas intermedias distribuidas a lo largo del muro.
*   **Distribución Inteligente**: Calcula automáticamente las separaciones máximas y distribuye columnetas intermedias de manera uniforme en el tramo libre de los muros.
*   **Alineación y Centrado Automático**: 
    *   Si la columneta seleccionada tiene el mismo espesor que el muro, se alinea perfectamente con este.
    *   Si la columneta es más gruesa que el muro, el Add-in distribuye el espesor excedente de forma simétrica a ambos lados del eje del muro, conservando la alineación exacta en esquinas y tramos para garantizar la continuidad estructural.
*   **Creación de Viguetas (Vigas de Confinamiento)**: Genera automáticamente viguetas superiores (coronación) y viguetas inferiores (cimiento) a lo largo de los muros, conectando de manera precisa los extremos en las esquinas y uniones mediante análisis topológico de nodos.
*   **Adaptación a Nivel Base y Topografía**: Asegura que las columnetas inicien en la cota correcta e interactúa con las restricciones del muro base.
*   **Interfaz de Usuario Integrada**: Incluye un panel en el Ribbon de Revit para iniciar el comando con opciones de configuración amigables mediante WPF y MVVM.
*   **Validación de Geometría**: Advierte al usuario sobre el uso de familias con espesores mayores a los del muro o configuraciones poco usuales, permitiendo al profesional tomar decisiones informadas antes de la generación.

## Requisitos Previos

*   Autodesk Revit 2026
*   .NET 8.0 SDK
*   Windows 10/11 (x64)

## Arquitectura del Proyecto

El proyecto está diseñado bajo una arquitectura limpia (Clean Architecture) y utiliza MVVM para la interfaz de usuario:

*   **App / Commands**: Punto de entrada del Add-in, creación de la cinta de opciones (Ribbon) y enlace del comando principal (`IExternalCommand`).
*   **Core**: Contiene la lógica de dominio y casos de uso, incluyendo cálculos matemáticos complejos para la geometría analítica de muros, intersecciones (CornerNodeSolver), distribuciones y rotaciones (WallConfinementCalculator, WallGeometryHelper).
*   **Services**: Se encarga de las operaciones con la API de Revit, como la extracción de datos de muros y familias, generación de los elementos (`ElementGenerationService`), y manipulación de parámetros y bounding boxes.
*   **UI**: Interfaces gráficas (WPF) con `CommunityToolkit.Mvvm`, manejando los ViewModels y la interacción inicial con el usuario antes de invocar la generación en Revit.

## Instalación y Uso (Desarrollo)

1.  Clona el repositorio.
2.  Abre la solución `CQIng.Revit.ColumnasVigasMuros.sln` en Visual Studio 2022.
3.  Compila el proyecto. El archivo `.addin` y las dependencias se generarán y copiarán automáticamente a la carpeta de Add-ins de Revit (`%AppData%\Autodesk\Revit\Addins\2026`).
4.  Inicia Revit 2026.
5.  En la pestaña correspondiente a CQIng, busca el botón **Columnas y Vigas de Muros**.
6.  Selecciona los muros y configura las familias deseadas en la interfaz emergente.
7.  Presiona "Aceptar" para generar el confinamiento.

## Soporte y Mantenimiento

Desarrollado para la mejora y automatización de procesos BIM de confinamiento de mampostería estructural.
