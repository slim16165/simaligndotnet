using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Alignment
{
    public class AlignmentContext
    {
        public AlignmentContextText Source { get; set; }
        public AlignmentContextText Target { get; set; }
        public Matrix<double> SimilarityMatrix { get; set; }
        public Dictionary<string, List<(int, int)>> Alignments { get; set; }
    }

    public class AlignmentContextText
    {
        public List<string> Sentences { get; set; }
        public List<List<string>> Tokens { get; set; }
        public Matrix<double> Embeddings { get; set; }
    }
}
