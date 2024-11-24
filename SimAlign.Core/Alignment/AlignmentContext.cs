using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Alignment
{
    public class AlignmentContext
    {
        public AlignmentContextText Source { get; set; }
        public AlignmentContextText Target { get; set; }
        public Matrix<double> SimilarityMatrix { get; set; }
    }
}
