using Autodesk.Revit.DB;
using CQIng.Revit.ColumnasVigasMuros.Core.Domain.Entities;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

/// <summary>
/// Calcula la elevación final de la vigueta superior considerando el desfase vertical configurado por el usuario.
/// Cuando UseTopBeamOffset es false devuelve exactamente los mismos valores que el cálculo original.
/// </summary>
public static class TopBeamElevationCalculator
{
    /// <summary>
    /// Devuelve la elevación absoluta (Z) y el Z_OFFSET_VALUE que debe recibir la vigueta superior.
    /// </summary>
    /// <param name="zMax">Elevación absoluta de la coronación del muro (en pies).</param>
    /// <param name="zMin">Elevación absoluta de la base del muro (en pies).</param>
    /// <param name="refLevel">Nivel de referencia del muro (topLevel o baseLevel).</param>
    /// <param name="originalZOffset">El Z_OFFSET_VALUE calculado originalmente para la posición en coronación.</param>
    /// <param name="options">Opciones de generación que incluyen el desfase configurado por el usuario.</param>
    /// <returns>
    /// Tupla con:
    ///   levelAnchorElevation  : coordenada Z que se asigna al StartPoint/EndPoint (posición del nivel de referencia).
    ///   zOffsetValue          : valor para el parámetro Z_OFFSET_VALUE de la instancia.
    ///   level                 : nivel de referencia que debe usarse al crear la vigueta.
    ///   realAbsoluteElevation : posición Z real donde quedará la vigueta (para detección de duplicados).
    /// </returns>
    public static (double levelAnchorElevation, double zOffsetValue, Level level, double realAbsoluteElevation) Calculate(
        double zMax,
        double zMin,
        Level refLevel,
        double originalZOffset,
        Level baseLevel,
        GenerationOptions options)
    {
        // Comportamiento original: desfase desactivado o desfase cero.
        // levelAnchorElevation = refLevel.Elevation (sin sumar originalZOffset) porque
        // StructuralExecutionService aplica Z_OFFSET_VALUE por separado al crear la instancia.
        // Sumar aquí y en el ExecutionService produce un doble desfase que suspende la vigueta.
        if (!options.UseTopBeamOffset || options.TopBeamVerticalOffsetMeters <= 0.0)
        {
            // Posición real = refLevel.Elevation + originalZOffset = zMax (coronación del muro)
            return (refLevel.Elevation, originalZOffset, refLevel, refLevel.Elevation + originalZOffset);
        }

        // Convertir el desfase del usuario de metros a pies (unidad interna de Revit).
        double offsetFeet = options.TopBeamVerticalOffsetMeters / 0.3048;

        // Altura útil del muro en pies.
        double wallHeightFeet = zMax - zMin;

        // Validación: el desfase no puede ser mayor o igual a la altura del muro.
        if (offsetFeet >= wallHeightFeet)
        {
            double wallHeightMeters = wallHeightFeet * 0.3048;
            throw new InvalidOperationException(
                $"El desfase vertical ({options.TopBeamVerticalOffsetMeters:F2} m) " +
                $"es mayor o igual a la altura del muro ({wallHeightMeters:F2} m). " +
                "La vigueta quedaría por debajo de la base del muro.");
        }

        // Posición real deseada = coronación del muro − desfase del usuario.
        double targetAbsoluteZ = zMax - offsetFeet;

        // La vigueta se ancla al baseLevel. El Z_OFFSET_VALUE es la diferencia relativa.
        double newZOffset = targetAbsoluteZ - baseLevel.Elevation;

        // levelAnchorElevation = baseLevel.Elevation: el StartPoint.Z es el anclaje del nivel,
        // no la posición absoluta final. StructuralExecutionService aplica Z_OFFSET_VALUE encima.
        return (baseLevel.Elevation, newZOffset, baseLevel, targetAbsoluteZ);
    }
}
