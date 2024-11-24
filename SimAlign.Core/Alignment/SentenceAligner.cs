using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using SemanticTranscriptProcessor.Common._2_Tokenizers;
using SemanticTranscriptProcessor.Common.Interfaces;
using SemanticTranscriptProcessor.Common._1_TextRepresentation;
using SimAlign.Core.AlignmentStrategies;
using SimAlign.Core.Config;
using SimAlign.Core.Services;

namespace SimAlign.Core.Alignment
{
    public class SentenceAligner
    {
        private readonly AlignmentConfig _config;
        private readonly List<IAlignmentStrategy> _alignmentStrategies;
        private readonly ITokenizer _tokenizer;
        private readonly IEmbeddingProvider _embedder;
        private readonly IAggregator _aggregator;

        public SentenceAligner(AlignmentConfig config, ITokenizer tokenizer, IEmbeddingProvider embeddingProvider, IAggregator aggregator)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _embedder = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
            _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));

            // Inizializza le strategie di allineamento
            _alignmentStrategies = InitializeAlignmentStrategies(_config.MatchingMethods);
        }

        private static List<IAlignmentStrategy> InitializeAlignmentStrategies(List<MatchingMethod> matchingMethods)
        {
            var strategies = new List<IAlignmentStrategy>();

            foreach (var method in matchingMethods)
            {
                switch (method)
                {
                    case MatchingMethod.MaxWeightMatch:
                        strategies.Add(new MaxWeightMatchAlignment());
                        break;
                    case MatchingMethod.IterativeMax:
                        strategies.Add(new IterMaxAlignment());
                        break;
                    case MatchingMethod.ForwardOnly:
                        strategies.Add(new ForwardAlignment());
                        break;
                    case MatchingMethod.ReverseOnly:
                        strategies.Add(new ReverseAlignment());
                        break;
                    case MatchingMethod.Intersection:
                        strategies.Add(new IntersectionAlignment());
                        break;
                    default:
                        throw new ArgumentException($"Metodo di allineamento non riconosciuto: {method}");
                }
            }

            return strategies;
        }

        /// <summary>
        /// Allinea le frasi sorgente e target e restituisce gli allineamenti per ogni metodo specificato.
        /// </summary>
        /// <param name="srcSentences">Lista di frasi sorgente.</param>
        /// <param name="trgSentences">Lista di frasi target.</param>
        /// <returns>Dizionario di allineamenti per ogni metodo.</returns>
        public async Task<Dictionary<MatchingMethod, List<(int, int)>>> AlignSentencesAsync(List<string> srcSentences, List<string> trgSentences)
        {
            // Inizializza il contesto con le frasi sorgente e target
            var context = new AlignmentContext
            {
                Source = new AlignmentContextText { Sentences = srcSentences },
                Target = new AlignmentContextText { Sentences = trgSentences }
            };

            // 1. Tokenizzazione delle frasi con mapping
            var sourceCtx = context.Source;
            var targetCtx = context.Target;

            foreach (var sentence in sourceCtx.Sentences)
            {
                var (tokens, tokenToWordMap) = _tokenizer.TokenizeWithMapping(sentence);
                sourceCtx.Tokens.Add(tokens.ToList());
                sourceCtx.TokenToWordMap.AddRange(tokenToWordMap);
            }

            foreach (var sentence in targetCtx.Sentences)
            {
                var (tokens, tokenToWordMap) = _tokenizer.TokenizeWithMapping(sentence);
                targetCtx.Tokens.Add(tokens.ToList());
                targetCtx.TokenToWordMap.AddRange(tokenToWordMap);
            }

            // 2. Creazione delle liste di BPE e mappatura BPE a parole
            MapTokensToWords(context.Source);
            MapTokensToWords(context.Target);

            // 3. Calcolo degli embedding per ogni frase
            var sourceEmbeddingTasks = context.Source.Sentences.Select(sentence => _embedder.GetSentenceEmbedding(sentence)).ToList();
            var targetEmbeddingTasks = context.Target.Sentences.Select(sentence => _embedder.GetSentenceEmbedding(sentence)).ToList();

            var sourceEmbeddings = await Task.WhenAll(sourceEmbeddingTasks);
            var targetEmbeddings = await Task.WhenAll(targetEmbeddingTasks);

            // 4. Aggregazione degli embeddings a livello di frase
            var sourceAggregatedEmbeddings = sourceEmbeddings.Select(e => e.SentenceEmbedding).ToList();
            var targetAggregatedEmbeddings = targetEmbeddings.Select(e => e.SentenceEmbedding).ToList();

            // 5. Creazione delle matrici di embedding
            var sourceEmbeddingMatrix = CreateEmbeddingMatrix(sourceAggregatedEmbeddings);
            var targetEmbeddingMatrix = CreateEmbeddingMatrix(targetAggregatedEmbeddings);

            // 6. Calcolo della matrice di similarità tra le frasi
            context.SimilarityMatrix = SimilarityCalculator.CalculateCosineSimilarity(sourceEmbeddingMatrix, targetEmbeddingMatrix);
            context.SimilarityMatrix = SimilarityCalculator.ApplyDistortion(context.SimilarityMatrix, _config.Distortion);

            // 7. Applicazione delle strategie di allineamento per generare le matrici di allineamento
            var alignmentMatrices = _alignmentStrategies.ToDictionary(strategy => strategy.MethodName, strategy => strategy.Align(context.SimilarityMatrix));

            // 8. Generazione degli allineamenti finali a partire dalle matrici di allineamento
            context.Alignments = GenerateAlignments(alignmentMatrices, context.Source.TokenToWordMap, context.Target.TokenToWordMap, _config.TokenType);

            return context.Alignments;
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

    public class TokenMapper
    {
        private readonly TokenType _tokenType;
        private readonly List<int> _srcTokenMap;
        private readonly List<int> _trgTokenMap;

        public TokenMapper(TokenType tokenType, List<int> srcTokenMap, List<int> trgTokenMap)
        {
            _tokenType = tokenType;
            _srcTokenMap = srcTokenMap;
            _trgTokenMap = trgTokenMap;
        }

        public (int SrcIndex, int TrgIndex) MapIndices(int srcTokenIndex, int trgTokenIndex)
        {
            int srcIndex = _tokenType == TokenType.BPE ? _srcTokenMap[srcTokenIndex] : srcTokenIndex;
            int trgIndex = _tokenType == TokenType.BPE ? _trgTokenMap[trgTokenIndex] : trgTokenIndex;
            return (srcIndex, trgIndex);
        }
    }
}
