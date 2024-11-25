using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Model;

public class AlignmentContextText
{
    public List<string> Sentences { get; set; } = new List<string>();
    public List<List<string>> Tokens { get; set; } = new List<List<string>>();

    // Nuove proprietà per BPE
    public List<string> TokenList { get; set; } = new List<string>();
    public List<int> TokenToWordMap { get; set; } = new List<int>();
}