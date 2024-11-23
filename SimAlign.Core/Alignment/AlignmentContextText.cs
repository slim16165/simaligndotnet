using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Alignment;

public class AlignmentContextText
{
    public List<string> Sentences { get; set; }
    public List<List<string>> Tokens { get; set; }
    public Matrix<double> Embeddings { get; set; }

    // Nuove proprietà per BPE
    public List<string> BpeList { get; set; }
    public List<int> BpeToWordMap { get; set; }
}