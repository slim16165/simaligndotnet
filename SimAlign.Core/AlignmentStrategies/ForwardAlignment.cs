using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.AlignmentStrategies;

public class ForwardAlignment : IAlignmentStrategy
{
    public string Name => "fwd";

    public Matrix<double> Align(Matrix<double> simMatrix, int maxIterations = 1)
    {
        int m = simMatrix.RowCount;
        int n = simMatrix.ColumnCount;
        Matrix<double> forward = Matrix<double>.Build.Dense(m, n, 0.0);

        for (int i = 0; i < m; i++)
        {
            int maxIndex = simMatrix.Row(i).MaximumIndex();
            forward[i, maxIndex] = 1.0;
        }

        return forward;
    }
}