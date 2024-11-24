using MathNet.Numerics.LinearAlgebra;
using SimAlign.Core.Utilities;

namespace SimAlign.Core.AlignmentStrategies
{
    public class IterMaxAlignment : IAlignmentStrategy
    {
        public string Name => "itermax";

        /// <summary>
        /// Calcola la matrice di allineamento utilizzando l'algoritmo iterativo itermax.
        /// </summary>
        /// <param name="simMatrix">Matrice di similarità tra due sequenze.</param>
        /// <param name="maxIterations">Numero massimo di iterazioni consentite.</param>
        /// <returns>Matrice di allineamento finale.</returns>
        public Matrix<double> Align(Matrix<double> simMatrix, int maxIterations = 2)
        {
            int rows = simMatrix.RowCount;
            int cols = simMatrix.ColumnCount;

            // Se le dimensioni sono troppo piccole, usa un approccio semplificato
            if (Math.Min(rows, cols) <= 2)
                return ComputeBasicAlignment(simMatrix);

            // Step 1: Calcola l'allineamento iniziale (forward-backward) come base
            Matrix<double> alignmentMatrix = InitializeAlignment(simMatrix);

            // Step 2: Migliora iterativamente l'allineamento
            for (int iteration = 1; iteration <= maxIterations; iteration++)
            {
                // Calcola la somma delle righe e delle colonne (per controllare le celle già assegnate)
                var (rowSums, colSums) = CalculateRowAndColumnSums(alignmentMatrix);

                // Crea maschere per penalizzare celle già assegnate
                var (mask, zeroMask) = CreatePenaltyMasks(rowSums, colSums, rows, cols);

                // Interrompi se tutte le righe o colonne sono completamente coperte
                if (AllConstraintsSatisfied(rowSums, colSums))
                    break;

                // Aggiorna la matrice di allineamento con le maschere
                alignmentMatrix = RefineAlignmentMatrix(simMatrix, alignmentMatrix, mask, zeroMask);
            }

            return alignmentMatrix;
        }

        /// <summary>
        /// Crea un allineamento iniziale semplice basato sulla similarità massima forward-backward.
        /// </summary>
        private static Matrix<double> ComputeBasicAlignment(Matrix<double> simMatrix)
        {
            var (forward, backward) = GetInitialAlignment(simMatrix);
            return forward.PointwiseMultiply(backward);
        }

        /// <summary>
        /// Calcola la matrice iniziale di allineamento forward-backward.
        /// </summary>
        private static Matrix<double> InitializeAlignment(Matrix<double> simMatrix)
        {
            var (forward, backward) = GetInitialAlignment(simMatrix);
            return forward.PointwiseMultiply(backward);
        }

        /// <summary>
        /// Calcola le somme di righe e colonne della matrice corrente di allineamento.
        /// </summary>
        private static (Vector<double>, Vector<double>) CalculateRowAndColumnSums(Matrix<double> alignmentMatrix)
        {
            return (alignmentMatrix.RowSums(), alignmentMatrix.ColumnSums());
        }

        /// <summary>
        /// Crea maschere per penalizzare celle già assegnate.
        /// </summary>
        private static (Matrix<double>, Matrix<double>) CreatePenaltyMasks(Vector<double> rowSums, Vector<double> colSums, int rows, int cols, double alpha = 0.9)
        {
            var maskX = (Matrix<double>.Build.Dense(rows, 1, 1.0) - rowSums.ToColumnMatrix()).PointwiseClamp(0.0, 1.0);
            var maskY = (Matrix<double>.Build.Dense(1, cols, 1.0) - colSums.ToRowMatrix()).PointwiseClamp(0.0, 1.0);
            var penaltyMask = ((alpha * maskX) + (alpha * maskY)).PointwiseClamp(0.0, 1.0);
            var zeroMask = 1.0 - ((1.0 - maskX) * (1.0 - maskY));
            return (penaltyMask, zeroMask);
        }

        /// <summary>
        /// Verifica se tutte le righe e colonne sono coperte dalle somme.
        /// </summary>
        private static bool AllConstraintsSatisfied(Vector<double> rowSums, Vector<double> colSums)
        {
            return rowSums.Sum() < 1.0 || colSums.Sum() < 1.0;
        }

        /// <summary>
        /// Aggiorna la matrice di allineamento usando le maschere per penalizzare celle già assegnate.
        /// </summary>
        private static Matrix<double> RefineAlignmentMatrix(Matrix<double> simMatrix, Matrix<double> currentAlignment, Matrix<double> mask, Matrix<double> zeroMask)
        {
            var maskedSim = simMatrix.PointwiseMultiply(mask);
            var (forward, backward) = GetInitialAlignment(maskedSim);

            forward = forward.PointwiseMultiply(zeroMask);
            backward = backward.PointwiseMultiply(zeroMask);

            var newAlignment = forward.PointwiseMultiply(backward);
            return currentAlignment + newAlignment - currentAlignment;
        }

        /// <summary>
        /// Calcola la matrice di allineamento forward e backward dalla matrice di similarità.
        /// </summary>
        private static (Matrix<double>, Matrix<double>) GetInitialAlignment(Matrix<double> simMatrix)
        {
            int rows = simMatrix.RowCount;
            int cols = simMatrix.ColumnCount;
            var forward = Matrix<double>.Build.Dense(rows, cols);
            var backward = Matrix<double>.Build.Dense(cols, rows);

            for (int i = 0; i < rows; i++)
            {
                int maxIndex = simMatrix.Row(i).MaximumIndex();
                forward[i, maxIndex] = 1.0;
            }

            for (int j = 0; j < cols; j++)
            {
                int maxIndex = simMatrix.Column(j).MaximumIndex();
                backward[j, maxIndex] = 1.0;
            }

            return (forward, backward.Transpose());
        }
    }
}
