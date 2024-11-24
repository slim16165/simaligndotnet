using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.AlignmentStrategies;

public class IntersectionAlignment : IAlignmentStrategy
{
    public string Name => "inter";

    public Matrix<double> Align(Matrix<double> simMatrix, int maxIterations = 1)
    {
        var forward = new ForwardAlignment().Align(simMatrix);
        var reverse = new ReverseAlignment().Align(simMatrix);
        return forward.PointwiseMultiply(reverse);
    }
}