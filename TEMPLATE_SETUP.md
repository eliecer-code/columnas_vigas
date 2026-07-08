# Configuración de Plantilla Add-in Revit

Sigue estos pasos para iniciar un nuevo proyecto a partir de esta plantilla:

## 1. Renombrar el Proyecto
1. Clona o copia esta carpeta en la ubicación de tu nuevo proyecto.
2. Cambia el nombre de la carpeta raíz al nombre de tu nuevo proyecto.
3. Cambia el nombre del archivo `AddinName.sln` a `TuNuevoProyecto.sln`.
4. Cambia el nombre del archivo `AddinName.csproj` a `TuNuevoProyecto.csproj`.
5. Abre `TuNuevoProyecto.sln` en tu editor (Visual Studio o VS Code).

## 2. Actualizar Namespaces y Títulos
1. Realiza un *Buscar y Reemplazar* en todo el proyecto:
   - Busca: `CQIng.Revit.ColumnasVigasMuros`
   - Reemplaza por: `TuEmpresa.TuProyecto`
2. En `TuNuevoProyecto.csproj`, modifica las siguientes etiquetas si es necesario:
   - `<AssemblyName>TuProyecto</AssemblyName>`
   - `<RootNamespace>TuEmpresa.TuProyecto</RootNamespace>`

## 3. Configurar Credenciales y APIs
1. En la raíz del proyecto, crea un archivo `.env`.
2. Añade tus credenciales base:
```env
API_URL=https://api.tudominio.com/
API_KEY=tu_api_key_secreta
```
3. Verifica el archivo `Services/ApiClient.cs` para ajustar los endpoints según la arquitectura de tu nuevo backend.

## 4. Configurar el Manifiesto (.addin)
El archivo `.csproj` está configurado para generar automáticamente el archivo `.addin` y copiarlo a `%APPDATA%\Autodesk\Revit\Addins\2026` al compilar.
Si necesitas cambiar la versión de Revit (por ejemplo, 2024), abre `TuNuevoProyecto.csproj` y cambia la ruta en las propiedades `<RevitInstallDir>` y `<RevitAddinsDir>`.
También puedes ajustar el `AddInId`, `<VendorId>`, y `<VendorDescription>` dentro de la tarea `<Target Name="GenerateAddinManifest">`.

## 5. Compilar
1. Cierra Revit completamente.
2. Ejecuta en la consola:
```bash
dotnet build
```
3. Abre Revit. Tu nuevo Add-in debería aparecer en la cinta de opciones (Ribbon).

## 6. Personalizar
- **Iconos:** Reemplaza los archivos en la carpeta `Resources/` con tus propios iconos.
- **Vistas:** Modifica `UI/Views/MainWindow.xaml` para adaptar la interfaz.
- **Modelos:** Ajusta las clases en `Core/Models/` para que coincidan con la lógica de tu nuevo negocio.
