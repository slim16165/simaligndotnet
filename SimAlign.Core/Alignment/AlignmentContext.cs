﻿using MathNet.Numerics.LinearAlgebra;
using SimAlign.Core.Config;

namespace SimAlign.Core.Alignment
{
    public class AlignmentContext
    {
        public AlignmentContextText Source { get; set; }
        public AlignmentContextText Target { get; set; }
        public Matrix<double> SimilarityMatrix { get; set; }
        public Dictionary<MatchingMethod, List<(int, int)>> Alignments { get; set; }
    }
}
