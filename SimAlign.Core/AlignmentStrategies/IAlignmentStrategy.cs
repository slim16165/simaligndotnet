using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.AlignmentStrategies
{
    public interface IAlignmentStrategy
    {
        string Name { get; }
        Matrix<double> Align(Matrix<double> simMatrix, int maxCount = 2);
    }
}