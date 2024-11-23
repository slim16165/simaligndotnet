using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Utilities;

public static class MatrixExtensions
{
    /// <summary>
    /// Clamp each element in the matrix between min and max values.
    /// </summary>
    /// <param name="matrix">The input matrix.</param>
    /// <param name="min">Minimum value.</param>
    /// <param name="max">Maximum value.</param>
    /// <returns>A new matrix with clamped values.</returns>
    public static Matrix<double> PointwiseClamp(this Matrix<double> matrix, double min, double max)
    {
        return matrix.Map(x => Math.Max(min, Math.Min(x, max)));
    }
}