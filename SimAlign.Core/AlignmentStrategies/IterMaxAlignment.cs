using MathNet.Numerics.LinearAlgebra;
using SimAlign.Core.Utilities;

namespace SimAlign.Core.AlignmentStrategies
{
    public class IterMaxAlignment : IAlignmentStrategy
    {
        public string Name => "itermax";

        public Matrix<double> Align(Matrix<double> simMatrix, int maxCount = 2)
        {
            int m = simMatrix.RowCount;
            int n = simMatrix.ColumnCount;
            double alphaRatio = 0.9;

            (Matrix<double> forward, Matrix<double> backward) = GetAlignmentMatrix(simMatrix);
            Matrix<double> inter = forward.PointwiseMultiply(backward);

            if (Math.Min(m, n) <= 2)
                return inter;

            int count = 1;

            while (count < maxCount)
            {
                Vector<double> rowSums = inter.RowSums();
                Vector<double> colSums = inter.ColumnSums();

                Matrix<double> maskX = (Matrix<double>.Build.Dense(m, 1, 1.0) - rowSums.ToColumnMatrix()).PointwiseClamp(0.0, 1.0);
                Matrix<double> maskY = (Matrix<double>.Build.Dense(1, n, 1.0) - colSums.ToRowMatrix()).PointwiseClamp(0.0, 1.0);
                Matrix<double> mask = ((alphaRatio * maskX) + (alphaRatio * maskY)).PointwiseClamp(0.0, 1.0);
                Matrix<double> maskZeros = 1.0 - ((1.0 - maskX) * (1.0 - maskY));

                if (rowSums.Sum() < 1.0 || colSums.Sum() < 1.0)
                {
                    mask = Matrix<double>.Build.Dense(mask.RowCount, mask.ColumnCount, 0.0);
                    maskZeros = Matrix<double>.Build.Dense(maskZeros.RowCount, maskZeros.ColumnCount, 0.0);
                }

                Matrix<double> newSim = simMatrix.PointwiseMultiply(mask);
                (Matrix<double> fwd, Matrix<double> bac) = GetAlignmentMatrix(newSim);
                fwd = fwd.PointwiseMultiply(maskZeros);
                bac = bac.PointwiseMultiply(maskZeros);
                var newInter = fwd.PointwiseMultiply(bac);

                if ((inter + newInter - inter).L1Norm() < 1e-6) // Tolleranza per uguaglianza tra matrici
                    break;

                inter += newInter;
                count += 1;
            }

            return inter;
        }

        private static (Matrix<double>, Matrix<double>) GetAlignmentMatrix(Matrix<double> simMatrix)
        {
            int m = simMatrix.RowCount;
            int n = simMatrix.ColumnCount;
            Matrix<double> forward = Matrix<double>.Build.Dense(m, n);
            Matrix<double> backward = Matrix<double>.Build.Dense(n, m);

            for (int i = 0; i < m; i++)
            {
                int maxIndex = simMatrix.Row(i).MaximumIndex();
                forward[i, maxIndex] = 1.0;
            }

            for (int j = 0; j < n; j++)
            {
                int maxIndex = simMatrix.Column(j).MaximumIndex();
                backward[j, maxIndex] = 1.0;
            }

            return (forward, backward.Transpose());
        }
    }
}
