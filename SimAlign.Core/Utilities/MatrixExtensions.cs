using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Utilities;

public static class MatrixExtensions
{
    /// <summary>
    /// Limits (clamps) each value in the matrix to stay within a specific range.
    /// </summary>
    /// <param name="matrix">The input matrix to process.</param>
    /// <param name="min">The minimum value allowed for each element.</param>
    /// <param name="max">The maximum value allowed for each element.</param>
    /// <returns>A new matrix where all values are within the specified range.</returns>
    /// <example>
    /// Input matrix:
    /// | 0.5  1.5  3.0 |
    /// | 4.0  0.1  5.0 |
    /// 
    /// After calling ClampToRange(1.0, 3.0):
    /// | 1.0  1.5  3.0 |
    /// | 3.0  1.0  3.0 |
    /// </example>
    public static Matrix<double> ClampToRange(this Matrix<double> matrix, double min, double max)
    {
        return matrix.Map(value => Math.Max(min, Math.Min(value, max)));
    }
}