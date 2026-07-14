# Auditoría Técnica — Prevención de Duplicados (ExistingElementsValidator)

## 1. Dónde se ejecutará la validación
La validación se conectará en `MainViewModel.cs`, dentro del método `Generate()`, justo **antes** de cambiar `IsBusy = true` y de ejecutar `RevitEventExecutor.Execute(...)` para la generación real. 
El flujo será:
1. Usuario presiona "Generar".
2. Se ejecuta `ExistingElementsValidator.Validate(doc, SelectedWalls)`.
3. Si la validación detecta elementos, se cancela la ejecución, se muestra un `MessageBox` con el informe detallado y se hace `return`.
4. Si la validación es limpia, el flujo continúa hacia el `GenerationPlanner` y `ExecutionService` como lo hace actualmente.

*Nota: La validación no se ejecuta durante la selección de muros ni durante la vista previa, ya que el usuario puede querer visualizar cómo quedarían los muros, pero el bloqueo estricto se aplica solo al intentar modificar el modelo (Generar).*

## 2. Cómo se identificarán los elementos creados por el Addin
Actualmente el Addin no marca los elementos que crea (solo asigna parámetros nativos de Revit como `Z_OFFSET_VALUE`).
Implementaremos el marcado utilizando **Extensible Storage (Almacenamiento Extensible)** directamente en las columnetas y viguetas creadas.

Se creará un `Schema` llamado `CQIng_GeneratedElement` con dos campos:
- `ParentWallId` (`ElementId`): El ID del muro que originó este elemento.
- `ElementType` (`string`): Identificador del tipo de elemento ("Columneta", "Vigueta Superior", "Vigueta Inferior").

Durante el `StructuralExecutionService`, cada vez que se crea un `FamilyInstance` (Columneta o Vigueta), se le adjuntará este `Schema` con los datos correspondientes.

Para la validación, `ExistingElementsValidator` utilizará un `ExtensibleStorageFilter` (muy rápido) o leerá las entidades de todas las columnetas y viguetas del proyecto, cruzando los `ParentWallId` con los muros seleccionados actualmente.

## 3. Por qué esta solución evita duplicados sin afectar el comportamiento actual
- **Cero dependencia geométrica:** No dependemos de buscar elementos que se solapen geométricamente (lo cual es lento y propenso a errores por tolerancias). El marcado por `Extensible Storage` es exacto a nivel de base de datos de Revit.
- **Resiliencia ante eliminaciones:** Si marcáramos el *muro* como "procesado", el usuario no podría regenerar si elimina manualmente las columnetas. Al marcar las *columnetas y viguetas*, si el usuario las borra, el muro vuelve a estar "limpio" automáticamente.
- **Aislamiento arquitectónico:** La lógica de generación y planificación no cambia. Solo agregamos una estampa (tag) al final de la creación del elemento. La validación es una lectura pura de datos en memoria antes de la transacción.

## 4. Qué clases serán modificadas y por qué
1. **`CQIngExtensibleStorageMarker.cs` (NUEVO):** Clase helper estática para definir el `Schema` y proveer métodos para leer/escribir los datos en los `FamilyInstance`.
2. **`ExistingElementsValidator.cs` (NUEVO):** Servicio estático que recibe la lista de muros seleccionados, busca elementos con nuestro `Schema` y devuelve un informe detallado agrupado por muro.
3. **`StructuralExecutionService.cs` (MODIFICADO):** Se agregará la llamada a `CQIngExtensibleStorageMarker.MarkElement(...)` inmediatamente después de crear cada `FamilyInstance` (columneta, vigueta superior, vigueta inferior).
4. **`MainViewModel.cs` (MODIFICADO):** Se inyectará la llamada a `ExistingElementsValidator` al inicio del método `Generate()`. Si hay un error, se mostrará el mensaje formateado y se abortará la operación.
