using MathNet.Numerics.LinearAlgebra;
using SemanticTranscriptProcessor.Common._1_TextRepresentation;
using SemanticTranscriptProcessor.Common.Common.Model;
using SemanticTranscriptProcessor.Common.Interfaces;
using SemanticTranscriptProcessor.Common.Model;
using SimAlign.Core.Model;
using SimAlign.Core.Utilities;

namespace SimAlign.Core.Services;

public class SentenceAligner : ITranscriptAligner
{
    private readonly AlignmentConfig _config;
    private readonly IAlignmentStrategy _alignmentStrategy;
    private readonly ITokenizer _tokenizer;
    private readonly IEmbeddingProvider _embedder;

    public SentenceAligner(
        AlignmentConfig config,
        ITokenizer tokenizer,
        IEmbeddingProvider embeddingProvider,
        IAlignmentStrategy alignmentStrategy)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        _embedder = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _alignmentStrategy = alignmentStrategy ?? throw new ArgumentNullException(nameof(alignmentStrategy));
    }

    public async Task<List<AlignedSegment>> AlignTranscripts(
        List<SentenceRepresentation> sentencesA,
        List<SentenceRepresentation> sentencesB)
    {
        // Prepare the alignment context
        var context = new AlignmentContext
        {
            Source = new AlignmentContextText { Sentences = sentencesA.Select(s => s.OriginalText).ToList() },
            Target = new AlignmentContextText { Sentences = sentencesB.Select(s => s.OriginalText).ToList() }
        };

        // Tokenization and mapping
        context.Source = await TokenizeAndMapAsync(context.Source, sentencesA);
        context.Target = await TokenizeAndMapAsync(context.Target, sentencesB);

        // Ensure embeddings
        var embeddingsA = await EnsureEmbeddingsAsync(sentencesA);
        var embeddingsB = await EnsureEmbeddingsAsync(sentencesB);

        // Create embedding matrices
        var sourceEmbeddingMatrix = CreateEmbeddingMatrix(embeddingsA);
        var targetEmbeddingMatrix = CreateEmbeddingMatrix(embeddingsB);

        // Compute similarity matrix
        context.SimilarityMatrix = SimilarityCalculator.CalculateCosineSimilarity(sourceEmbeddingMatrix, targetEmbeddingMatrix);
        context.SimilarityMatrix = SimilarityCalculator.ApplyDistortion(context.SimilarityMatrix, _config.Distortion);

        // Apply the alignment strategy
        var alignmentMatrix = _alignmentStrategy.Align(context.SimilarityMatrix);

        // Generate aligned segments
        var alignedSegments = GenerateAlignedSegments(alignmentMatrix, sentencesA, sentencesB);

        return alignedSegments;
    }

    /// <summary>
    /// Tokenizes the given sentences and updates the alignment context with token data.
    /// </summary>
    /// <param name="contextText">The alignment context to populate with token data.</param>
    /// <param name="sentences">The list of sentences to tokenize and process.</param>
    private async Task<AlignmentContextText> TokenizeAndMapAsync(
        AlignmentContextText contextText,
        List<SentenceRepresentation> sentences)
    {
        var updatedContextText = new AlignmentContextText
        {
            Sentences = contextText.Sentences,
            Tokens = new List<List<string>>(),
            TokenList = new List<string>(),
            TokenToWordMap = new List<int>()
        };

        foreach (var sentence in sentences)
        {
            if (sentence.Tokens == null || sentence.Tokens.Tokens.Count == 0)
            {
                sentence.Tokens = _tokenizer.TokenizeWithMapping(sentence.OriginalText);
            }

            updatedContextText.Tokens.Add(sentence.Tokens.Tokens);
        }

        MapTokensToWords(updatedContextText);

        return updatedContextText;
    }

    private async Task<List<float[]>> EnsureEmbeddingsAsync(List<SentenceRepresentation> sentences)
    {
        var embeddings = new List<float[]>();

        foreach (var sentence in sentences)
        {
            if (sentence.SentenceEmbedding == null)
            {
                var embedding = await _embedder.GetSentenceEmbedding(sentence.OriginalText);
                sentence.SentenceEmbedding = embedding.SentenceEmbedding;
            }

            embeddings.Add(sentence.SentenceEmbedding);
        }

        return embeddings;
    }

    private static List<AlignedSegment> GenerateAlignedSegments(
        Matrix<double> alignmentMatrix,
        List<SentenceRepresentation> sentencesA,
        List<SentenceRepresentation> sentencesB)
    {
        var alignedSegments = new List<AlignedSegment>();
        var alignedPairs = new HashSet<(int, int)>();

        for (int i = 0; i < alignmentMatrix.RowCount; i++)
        {
            for (int j = 0; j < alignmentMatrix.ColumnCount; j++)
            {
                if (alignmentMatrix[i, j] > 0)
                {
                    alignedPairs.Add((i, j));
                }
            }
        }

        foreach (var (i, j) in alignedPairs.OrderBy(p => p.Item1).ThenBy(p => p.Item2))
        {
            var segment = new AlignedSegment
            {
                VersionA = new List<SentenceRepresentation> { sentencesA[i] },
                VersionB = new List<SentenceRepresentation> { sentencesB[j] },
                SimilarityScore = alignmentMatrix[i, j]
            };
            alignedSegments.Add(segment);
        }

        return alignedSegments;
    }

    /// <summary>
    /// Maps BPE tokens to their respective words and updates the context.
    /// </summary>
    /// <param name="contextText">Object containing sentence information.</param>
    private static void MapTokensToWords(AlignmentContextText contextText)
    {
        if (contextText == null)
            throw new ArgumentNullException(nameof(contextText));
        if (contextText.Tokens == null)
            throw new ArgumentNullException(nameof(contextText.Tokens));

        var tokenList = new List<string>();
        var tokenToWordMap = new List<int>();

        for (int i = 0; i < contextText.Tokens.Count; i++)
        {
            foreach (var bpe in contextText.Tokens[i])
            {
                tokenList.Add(bpe);
                tokenToWordMap.Add(i); // Indicates that this BPE token belongs to word i
            }
        }

        contextText.TokenList = tokenList;
        contextText.TokenToWordMap = tokenToWordMap;
    }

    /// <summary>
    /// Creates an embedding matrix from a list of float arrays.
    /// </summary>
    /// <param name="embeddings">List of float arrays representing embeddings.</param>
    /// <returns>Embedding matrix.</returns>
    private static Matrix<double> CreateEmbeddingMatrix(List<float[]> embeddings)
    {
        if (embeddings == null || embeddings.Count == 0)
            throw new ArgumentException("The list of embeddings cannot be null or empty.");

        int numEmbeddings = embeddings.Count;
        int embeddingDim = embeddings[0].Length;

        var matrix = Matrix<double>.Build.Dense(numEmbeddings, embeddingDim);

        for (int i = 0; i < numEmbeddings; i++)
        {
            for (int j = 0; j < embeddingDim; j++)
            {
                matrix[i, j] = embeddings[i][j];
            }
        }

        return matrix;
    }

    /// <summary>
    /// Generates final alignments from alignment matrices and BPE-to-word mappings.
    /// </summary>
    /// <param name="alignmentMatrices">Dictionary of alignment matrices for each method.</param>
    /// <param name="sourceTokenMap">BPE-to-word mapping for the source sentence.</param>
    /// <param name="targetTokenMap">BPE-to-word mapping for the target sentence.</param>
    /// <param name="tokenType">Type of tokenization ("word" or "bpe").</param>
    /// <returns>Dictionary of alignments for each method.</returns>
    private static Dictionary<MatchingMethod, List<(int, int)>> GenerateAlignments(
        Dictionary<MatchingMethod, Matrix<double>> alignmentMatrices,
        List<int> sourceTokenMap,
        List<int> targetTokenMap,
        TokenType tokenType)
    {
        // Initialize the mapper once
        var mapper = new TokenMapper(tokenType, sourceTokenMap, targetTokenMap);

        // For each alignment method, generate alignments from the matrix
        return alignmentMatrices.ToDictionary(
            method => method.Key,
            method => ProcessAlignmentMatrix(method.Value, mapper)
        );
    }

    /// <summary>
    /// Processes a single alignment matrix to generate a sorted list of index pairs (source -> target).
    /// </summary>
    /// <param name="matrix">Similarity or alignment matrix.</param>
    /// <param name="mapper">Object responsible for mapping token indices to words.</param>
    /// <returns>Sorted and unique list of index pairs.</returns>
    private static List<(int, int)> ProcessAlignmentMatrix(Matrix<double> matrix, TokenMapper mapper)
    {
        // Use a HashSet to automatically eliminate duplicates
        var alignmentSet = new HashSet<(int, int)>();

        // Iterate through the matrix to find matches
        for (int i = 0; i < matrix.RowCount; i++)
        {
            for (int j = 0; j < matrix.ColumnCount; j++)
            {
                if (matrix[i, j] > 0)
                {
                    alignmentSet.Add(mapper.MapIndices(i, j));
                }
            }
        }

        // Sort and return the list
        return alignmentSet
            .OrderBy(pair => pair.Item1)
            .ThenBy(pair => pair.Item2)
            .ToList();
    }
}
