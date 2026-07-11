using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace CQIng.Revit.ColumnasVigasMuros.Core.Application.Helpers;

public static class NodeTopologyAnalyzer
{
    public static void AnalyzeNodeTopology(CornerNode node)
    {
        // Clasificar el nodo basado en sus muros conectados
        int totalConnections = 0;
        foreach (var nw in node.ConnectedWalls)
        {
            if (nw.IsContinuous)
                totalConnections += 2; // Un muro continuo cuenta como 2 conexiones topológicas
            else
                totalConnections += 1;
        }

        if (totalConnections == 1)
        {
            node.Type = CornerNode.NodeType.End;
        }
        else if (totalConnections == 2)
        {
            // 2 muros terminating
            node.Type = CornerNode.NodeType.L;
        }
        else if (totalConnections == 3)
        {
            // 1 continuo + 1 terminating, o 3 terminating
            node.Type = CornerNode.NodeType.T;
        }
        else if (totalConnections >= 4)
        {
            node.Type = CornerNode.NodeType.Cross;
        }
        else
        {
            node.Type = CornerNode.NodeType.Unknown;
        }

        // Seleccionar el Muro Principal (PrimaryWall) basado en la geometría:
        // 1. Si hay un muro continuo, ese es el principal (mantiene continuidad).
        // 2. Si no, el más grueso.
        // 3. Si no, el más largo.
        var continuousWall = node.ConnectedWalls.FirstOrDefault(w => w.IsContinuous);
        if (continuousWall != null)
        {
            node.PrimaryWall = continuousWall;
        }
        else
        {
            node.PrimaryWall = node.ConnectedWalls
                .OrderByDescending(w => w.Thickness)
                .ThenByDescending(w => (w.Wall.Location as LocationCurve).Curve.Length)
                .First();
        }

        // Definir qué muros deben reducirse
        // La regla de negocio exige que los muros continuos (muro principal de una T) NUNCA se recorten.
        // Solo los muros 'Terminating' (secundarios) se recortan.
        foreach (var nw in node.ConnectedWalls)
        {
            if (nw.IsContinuous)
            {
                // Muro continuo: nunca se reduce.
                node.WallCutLengths[nw.Wall.Id] = 0.0;
            }
            else
            {
                // Si es L, todos se reducen. Si es T, el principal (continuo) no se reduce, pero el secundario sí.
                // En un cruce (+), los continuos no se reducen.
                // Si un terminating NO es el principal (o si es una L donde ambos se reducen), se calcula su reducción.
                
                // NOTA: El valor numérico de la reducción se calcula luego en SolveNodeGeometry. 
                // Aquí solo pre-marcamos con un flag o los inicializamos.
            }
        }
    }
}
