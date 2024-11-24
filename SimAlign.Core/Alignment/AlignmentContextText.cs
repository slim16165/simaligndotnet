using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Alignment;

public class AlignmentContextText
{
    public List<string> Sentences { get; set; }
    public List<List<string>> Tokens { get; set; }

    // Nuove proprietà per BPE
    public List<string> TokenList { get; set; }
    public List<int> TokenToWordMap { get; set; }
}