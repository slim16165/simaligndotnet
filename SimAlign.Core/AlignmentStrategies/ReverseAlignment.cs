using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.AlignmentStrategies;

public class ReverseAlignment : IAlignmentStrategy
{
    public string Name => "rev";

    public Matrix<double> Align(Matrix<double> simMatrix, int maxIterations = 1)
    {
        int m = simMatrix.RowCount;
        int n = simMatrix.ColumnCount;
        Matrix<double> reverse = Matrix<double>.Build.Dense(n, m, 0.0);

        for (int j = 0; j < n; j++)
        {
            int maxIndex = simMatrix.Column(j).MaximumIndex();
            reverse[j, maxIndex] = 1.0;
        }

        return reverse.Transpose();
    }
}