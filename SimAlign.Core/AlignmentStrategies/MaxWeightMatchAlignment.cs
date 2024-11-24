using Google.OrTools.Graph;
using MathNet.Numerics.LinearAlgebra;
using SemanticTranscriptProcessor.Common.Common.Model;
using SemanticTranscriptProcessor.Common.Interfaces;

namespace SimAlign.Core.AlignmentStrategies
{
    public class MaxWeightMatchAlignment : IAlignmentStrategy
    {
        public MatchingMethod MethodName => MatchingMethod.MaxWeightMatch;

        public Matrix<double> Align(Matrix<double> simMatrix, int maxCount = 2)
        {
            int m = simMatrix.RowCount;
            int n = simMatrix.ColumnCount;

            // Creazione dell'istanza di LinearSumAssignment
            LinearSumAssignment assignment = new LinearSumAssignment();

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    // OR-Tools risolve minimizzando il costo, quindi usiamo pesi negativi
                    int cost = -(int)(simMatrix[i, j] * 1000); // Scala i pesi se necessario
                    assignment.AddArcWithCost(i, j, cost);
                }
            }

            // Risolvi il problema di assegnazione
            assignment.Solve();

            // Crea la matrice di allineamento
            Matrix<double> alignmentMatrix = Matrix<double>.Build.Dense(m, n, 0.0);

            for (int i = 0; i < m; i++)
            {
                int assigned = assignment.RightMate(i);
                if (assigned != -1) // Verifica che un nodo sia stato assegnato
                {
                    alignmentMatrix[i, assigned] = 1.0;
                }
            }

            return alignmentMatrix;
        }
    }
}