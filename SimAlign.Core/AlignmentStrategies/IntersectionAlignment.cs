using MathNet.Numerics.LinearAlgebra;
using SimAlign.Core.Config;

namespace SimAlign.Core.AlignmentStrategies;

public class IntersectionAlignment : IAlignmentStrategy
{
    public MatchingMethod MethodName => MatchingMethod.Intersection;

    public Matrix<double> Align(Matrix<double> simMatrix, int maxIterations = 1)
    {
        var forward = new ForwardAlignment().Align(simMatrix);
        var reverse = new ReverseAlignment().Align(simMatrix);
        return forward.PointwiseMultiply(reverse);
    }
}