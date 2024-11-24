using MathNet.Numerics.LinearAlgebra;
using SimAlign.Core.Config;

namespace SimAlign.Core.AlignmentStrategies
{
    public interface IAlignmentStrategy
    {
        MatchingMethod MethodName { get; }
        Matrix<double> Align(Matrix<double> simMatrix, int maxCount = 2);
    }
}