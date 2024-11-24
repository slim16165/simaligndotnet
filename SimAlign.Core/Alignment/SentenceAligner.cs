﻿using MathNet.Numerics.LinearAlgebra;
using SemanticTranscriptProcessor.Common._1_TextRepresentation;
using SemanticTranscriptProcessor.Common.Common.Model;
using SemanticTranscriptProcessor.Common.Interfaces;
using SimAlign.Core.Services;

namespace SimAlign.Core.Alignment;

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

        // Tokenization and mapping if not already done
        await TokenizeAndMapAsync(sentencesA, context.Source);
        await TokenizeAndMapAsync(sentencesB, context.Target);

        // Calculate embeddings if not already present
        await EnsureEmbeddingsAsync(sentencesA);
        await EnsureEmbeddingsAsync(sentencesB);

        // Create embedding matrices
        var sourceEmbeddingMatrix = CreateEmbeddingMatrix(sentencesA.Select(s => s.SentenceEmbedding).ToList());
        var targetEmbeddingMatrix = CreateEmbeddingMatrix(sentencesB.Select(s => s.SentenceEmbedding).ToList());

        // Compute similarity matrix
        context.SimilarityMatrix = SimilarityCalculator.CalculateCosineSimilarity(sourceEmbeddingMatrix, targetEmbeddingMatrix);
        context.SimilarityMatrix = SimilarityCalculator.ApplyDistortion(context.SimilarityMatrix, _config.Distortion);

        // Apply the alignment strategy
        var alignmentMatrix = _alignmentStrategy.Align(context.SimilarityMatrix);

        // Generate aligned segments
        var alignedSegments = GenerateAlignedSegments(alignmentMatrix, sentencesA, sentencesB);

        return alignedSegments;
    }

    private async Task TokenizeAndMapAsync(List<SentenceRepresentation> sentences, AlignmentContextText contextText)
    {
        foreach (var sentence in sentences)
        {
            if (sentence.Tokens == null || sentence.Tokens.Length == 0)
            {
                var (tokens, _) = _tokenizer.TokenizeWithMapping(sentence.OriginalText);
                sentence.Tokens = tokens.ToArray();
            }
            contextText.Tokens.Add(sentence.Tokens.ToList());
        }

        MapTokensToWords(contextText);
    }

    private async Task EnsureEmbeddingsAsync(List<SentenceRepresentation> sentences)
    {
        var tasks = sentences.Select(async sentence =>
        {
            if (sentence.SentenceEmbedding == null)
            {
                var embedding = await _embedder.GetSentenceEmbedding(sentence.OriginalText);
                sentence.SentenceEmbedding = embedding.SentenceEmbedding;
            }
        }).ToList();

        await Task.WhenAll(tasks);
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
    /// Mappa i token BPE alle rispettive parole e aggiorna il contesto.
    /// </summary>
    /// <param name="contextText">Oggetto contenente le informazioni della frase.</param>
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
                tokenToWordMap.Add(i); // Indica che questo token BPE appartiene alla parola i
            }
        }

        contextText.TokenList = tokenList;
        contextText.TokenToWordMap = tokenToWordMap;
    }

    /// <summary>
    /// Crea una matrice di embedding a partire da una lista di array di float.
    /// </summary>
    /// <param name="embeddings">Lista di array di float rappresentanti gli embeddings.</param>
    /// <returns>Matrice di embedding.</returns>
    private static Matrix<double> CreateEmbeddingMatrix(List<float[]> embeddings)
    {
        if (embeddings == null || embeddings.Count == 0)
            throw new ArgumentException("La lista di embeddings non può essere nulla o vuota.");

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
    /// Genera gli allineamenti finali a partire dalle matrici di allineamento e dal mapping BPE-Parola.
    /// </summary>
    /// <param name="alignmentMatrices">Dizionario di matrici di allineamento per ogni metodo.</param>
    /// <param name="sourceTokenMap">Mappatura da token BPE a parola per la frase sorgente.</param>
    /// <param name="targetTokenMap">Mappatura da token BPE a parola per la frase target.</param>
    /// <param name="tokenType">Tipo di tokenizzazione ("word" o "bpe").</param>
    /// <returns>Dizionario di allineamenti per ogni metodo.</returns>
    private static Dictionary<MatchingMethod, List<(int, int)>> GenerateAlignments(
        Dictionary<MatchingMethod, Matrix<double>> alignmentMatrices,
        List<int> sourceTokenMap,
        List<int> targetTokenMap,
        TokenType tokenType)
    {
        // Inizializza il mapper una volta sola
        var mapper = new TokenMapper(tokenType, sourceTokenMap, targetTokenMap);

        // Per ogni metodo di allineamento, genera gli allineamenti a partire dalla matrice
        return alignmentMatrices.ToDictionary(
            method => method.Key,
            method => ProcessAlignmentMatrix(method.Value, mapper)
        );
    }

    /// <summary>
    /// Processa una singola matrice di allineamento per generare una lista ordinata di coppie di indici (sorgente -> bersaglio).
    /// </summary>
    /// <param name="matrix">Matrice di similarità o allineamento.</param>
    /// <param name="mapper">Oggetto responsabile della mappatura degli indici di token alle parole.</param>
    /// <returns>Lista ordinata e unica di coppie di indici.</returns>
    private static List<(int, int)> ProcessAlignmentMatrix(Matrix<double> matrix, TokenMapper mapper)
    {
        // Usa un HashSet per eliminare duplicati automaticamente
        var alignmentSet = new HashSet<(int, int)>();

        // Itera attraverso la matrice per trovare le corrispondenze
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

        // Ordina e restituisci la lista
        return alignmentSet
            .OrderBy(pair => pair.Item1)
            .ThenBy(pair => pair.Item2)
            .ToList();
    }
}