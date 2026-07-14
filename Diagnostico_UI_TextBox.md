# Diagnóstico Ponytail — Comportamiento del TextBox y Altura de Ventana

## 1. Causa raíz del comportamiento del TextBox
El problema donde el valor del TextBox no puede ser borrado y se auto-restaura tiene su origen en la configuración del binding en XAML:
`Text="{Binding TopBeamVerticalOffset, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, StringFormat=N2}"`

**Explicación técnica:**
1. **`UpdateSourceTrigger=PropertyChanged`**: WPF intenta actualizar la propiedad `TopBeamVerticalOffset` del `ViewModel` con **cada tecla** que el usuario presiona.
2. **Propiedad numérica (`double`)**: Cuando el usuario selecciona todo el texto y presiona "Borrar", el TextBox queda temporalmente vacío (`""`) o con un signo menos (`"-"`). 
3. **Fallo de conversión**: El motor de Binding de WPF no puede convertir un string vacío o `"-"` a un tipo `double`. Al fallar la conversión en tiempo real, WPF aborta la actualización y el Binding automáticamente revierte el contenido visual del TextBox al último valor numérico válido conocido por el `ViewModel`.
4. **`StringFormat=N2`**: Además, al forzar un formato numérico en cada pulsación, entorpece la escritura libre.

**Solución elegida:**
Se cambia el `UpdateSourceTrigger` a `LostFocus` (que es el valor por defecto para los TextBox). De esta manera:
- El usuario puede borrar el texto completamente, escribir libremente y dejar el campo temporalmente vacío.
- WPF **solo** intentará convertir el texto a `double` y actualizar el `ViewModel` cuando el usuario termine de editar (presionando Tab o haciendo clic en otro control).
- Si al salir del control el valor ingresado no es numérico, WPF simplemente ignorará el cambio (manteniendo el valor numérico válido previo en memoria), pero ahora no interrumpirá la experiencia de escritura.

## 2. Causa raíz de la altura de la ventana
En la versión original, la ventana tenía un tamaño fijo de `Height="600"`.
Al añadir la nueva sección "Posición de la Vigueta Superior", la altura acumulada de todos los controles dentro del `StackPanel` izquierdo superó los 600 píxeles (considerando márgenes y la barra de título de Windows).
Como el botón "Generar Elementos" está al final del `StackPanel`, quedó empujado fuera de los límites visibles.

**Solución elegida:**
Para aplicar la solución "menos invasiva" sin arriesgar la estabilidad del `HelixViewport3D` (que a veces colapsa o crece infinitamente con `SizeToContent`), la solución más sólida y segura en WPF para diseños basados en Grid es simplemente incrementar la altura estática de la ventana a `Height="680"`.
- Esto provee el espacio adicional necesario (aprox. 80 píxeles extra) para los nuevos controles.
- Garantiza que el botón "Generar Elementos" sea completamente visible tanto si el CheckBox está activado como desactivado.
- Mantiene el comportamiento de la ventana y los controles 3D exactamente igual.
