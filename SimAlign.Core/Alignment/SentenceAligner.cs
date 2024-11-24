using MathNet.Numerics.LinearAlgebra;
using SemanticTranscriptProcessor.Common.Interfaces;
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
        private readonly IEmbeddingProvider _embeddingProvider;

        public SentenceAligner(AlignmentConfig config, ITokenizer tokenizer, IEmbeddingProvider embeddingProvider)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
            _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));

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
        public Dictionary<MatchingMethod, List<(int, int)>> AlignSentences(List<string> srcSentences, List<string> trgSentences)
        {
            // Inizializza il contesto con le frasi sorgente e target
            var context = new AlignmentContext
            {
                Source = new AlignmentContextText { Sentences = srcSentences },
                Target = new AlignmentContextText { Sentences = trgSentences }
            };

            // 1. Tokenizzazione delle frasi
            var sourceCtx = context.Source;
            var targetCtx = context.Target;

            sourceCtx.Tokens = _tokenizer.TokenizeSentences(sourceCtx.Sentences);
            targetCtx.Tokens = _tokenizer.TokenizeSentences(targetCtx.Sentences);

            // 2. Creazione delle liste di BPE e mappatura BPE a parole
            MapTokensToWords(context.Source);
            MapTokensToWords(context.Target);

            // 3. Generazione degli embedding per i token BPE
            var batchEmbeddings = _embeddingLoader.ComputeEmbeddingsForBatch(new List<List<string>> { context.Source.TokenList, context.Target.TokenList });
            
            if (batchEmbeddings?.SourceEmbedding == null || batchEmbeddings.TargetEmbedding == null)
                throw new InvalidOperationException("Embeddings non ottenuti o incompleti.");

            // 4. Calcolo degli embedding a livello di parola, se necessario
            if (_config.TokenType == TokenType.Word)
            {
                sourceCtx.Embeddings = CondenseEmbeddingsFromTokenToWordLevel(sourceCtx.Embeddings, sourceCtx.Tokens);
                targetCtx.Embeddings = CondenseEmbeddingsFromTokenToWordLevel(targetCtx.Embeddings, targetCtx.Tokens);
            }
            // Altrimenti, gli embedding BPE sono già ok (sono stati già assegnati sopra)

            // 5. Calcolo della matrice di similarità tra le frasi
            context.SimilarityMatrix = SimilarityCalculator.CalculateCosineSimilarity(sourceCtx.Embeddings, targetCtx.Embeddings);
            context.SimilarityMatrix = SimilarityCalculator.ApplyDistortion(context.SimilarityMatrix, _config.Distortion);

            // 6. Applicazione delle strategie di allineamento per generare le matrici di allineamento
            var alignmentMatrices = _alignmentStrategies.ToDictionary(strategy => strategy.MethodName, strategy => strategy.Align(context.SimilarityMatrix));

            // 7. Generazione degli allineamenti finali a partire dalle matrici di allineamento
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
        /// Calcola l'embedding a livello di parola per una singola frase aggregando gli embedding dei token BPE.
        /// </summary>
        /// <param name="tokenVectors">Matrice di embedding BPE per la frase.</param>
        /// <param name="wordTokens">Lista di token BPE per ogni parola nella frase.</param>
        /// <returns>Matrice di embedding a livello di parola.</returns>
        private static Matrix<double> CondenseEmbeddingsFromTokenToWordLevel(Matrix<double> tokenVectors, List<List<string>> wordTokens)
        {
            var wordVectors = new List<Vector<double>>();
            int tokenIndex = 0;

            foreach (var word in wordTokens)
            {
                int wordTokenCount = word.Count;

                // Verifica che non ci sia un disallineamento tra i token BPE e le parole
                if (tokenIndex + wordTokenCount > tokenVectors.RowCount)
                    throw new ArgumentException("Disallineamento tra BPE vectors e word tokens.");

                // Estrai gli embedding dei token BPE che appartengono alla parola corrente
                var wordBpeVectors = tokenVectors.SubMatrix(tokenIndex, wordTokenCount, 0, tokenVectors.ColumnCount);

                // Calcola la media degli embedding dei token BPE per ottenere un embedding rappresentativo per la parola
                var avgVector = wordBpeVectors.RowSums() / wordTokenCount;
                wordVectors.Add(avgVector);

                tokenIndex += wordTokenCount;
            }

            // Crea una matrice dove ogni riga è un embedding medio per una parola
            return Matrix<double>.Build.DenseOfRowVectors(wordVectors);
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


    public class BatchEmbeddings
    {
        public Matrix<double> SourceEmbedding { get; set; }
        public Matrix<double> TargetEmbedding { get; set; }
    }
}
