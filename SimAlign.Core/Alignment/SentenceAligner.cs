using SimAlign.Core.AlignmentStrategies;
using SimAlign.Core.Config;
using SimAlign.Core.Services;
using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Alignment
{
    public class SentenceAligner
    {
        private readonly AlignmentConfig _config;
        private readonly Tokenizer _tokenizer;
        private readonly EmbeddingLoader _embeddingLoader;
        private readonly List<IAlignmentStrategy> _alignmentStrategies;

        public SentenceAligner(AlignmentConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Inizializza Tokenizer e EmbeddingLoader
            _tokenizer = new Tokenizer(config.Model);
            _embeddingLoader = new EmbeddingLoader(
                model: config.Model,
                device: config.Device,
                layer: config.Layer,
                tokenizer: _tokenizer
            );

            // Inizializza le strategie di allineamento
            _alignmentStrategies = InitializeAlignmentStrategies(config.MatchingMethods);
        }

        private static List<IAlignmentStrategy> InitializeAlignmentStrategies(List<string> matchingMethods)
        {
            var strategies = new List<IAlignmentStrategy>();

            foreach (var method in matchingMethods)
            {
                switch (method.ToLower())
                {
                    case "mwmf":
                        strategies.Add(new MaxWeightMatchAlignment());
                        break;
                    case "itermax":
                        strategies.Add(new IterMaxAlignment());
                        break;
                    // Aggiungi altre strategie se necessario
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
        public Dictionary<string, List<(int, int)>> AlignSentences(List<string> srcSentences, List<string> trgSentences)
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
            var batchEmbeddings = _embeddingLoader.ComputeEmbeddingsForBatch(new List<List<string>> { context.Source.BpeList, context.Target.BpeList });
            if (batchEmbeddings?.SourceEmbedding == null || batchEmbeddings.TargetEmbedding == null)
                throw new InvalidOperationException("Embeddings non ottenuti o incompleti.");


            // 4. Calcolo degli embedding a livello di parola, se necessario
            if (_config.TokenType == "word")
            {
                sourceCtx.Embeddings = ComputeWordEmbeddingsForSentence(sourceCtx.Embeddings, sourceCtx.Tokens);
                targetCtx.Embeddings = ComputeWordEmbeddingsForSentence(targetCtx.Embeddings, targetCtx.Tokens);
            }
            // Altrimenti, gli embedding BPE sono già ok (sono stati già assegnati sopra)

            // 5. Calcolo della matrice di similarità tra le frasi
            context.SimilarityMatrix = SimilarityCalculator.CalculateCosineSimilarity(sourceCtx.Embeddings, targetCtx.Embeddings);
            context.SimilarityMatrix = SimilarityCalculator.ApplyDistortion(context.SimilarityMatrix, _config.Distortion);

            // 6. Applicazione delle strategie di allineamento per generare le matrici di allineamento
            var alignmentMatrices = _alignmentStrategies.ToDictionary(strategy => strategy.Name, strategy => strategy.Align(context.SimilarityMatrix));

            // 7. Generazione degli allineamenti finali a partire dalle matrici di allineamento
            context.Alignments = GenerateAlignments(alignmentMatrices, context.Source.BpeToWordMap, context.Target.BpeToWordMap, _config.TokenType);

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

            var bpeList = new List<string>();
            var bpeToWordMap = new List<int>();

            for (int i = 0; i < contextText.Tokens.Count; i++)
            {
                foreach (var bpe in contextText.Tokens[i])
                {
                    bpeList.Add(bpe);
                    bpeToWordMap.Add(i); // Indica che questo token BPE appartiene alla parola i
                }
            }

            contextText.BpeList = bpeList;
            contextText.BpeToWordMap = bpeToWordMap;
        }


        /// <summary>
        /// Calcola l'embedding a livello di parola per una singola frase aggregando gli embedding dei token BPE.
        /// </summary>
        /// <param name="bpeVectors">Matrice di embedding BPE per la frase.</param>
        /// <param name="wordTokens">Lista di token BPE per ogni parola nella frase.</param>
        /// <returns>Matrice di embedding a livello di parola.</returns>
        private static Matrix<double> ComputeWordEmbeddingsForSentence(Matrix<double> bpeVectors, List<List<string>> wordTokens)
        {
            var wordVectors = new List<Vector<double>>();
            int bpeIndex = 0;

            foreach (var word in wordTokens)
            {
                int wordBpeCount = word.Count;

                // Verifica che non ci sia un disallineamento tra i token BPE e le parole
                if (bpeIndex + wordBpeCount > bpeVectors.RowCount)
                    throw new ArgumentException("Disallineamento tra BPE vectors e word tokens.");

                // Estrai gli embedding dei token BPE che appartengono alla parola corrente
                var wordBpeVectors = bpeVectors.SubMatrix(bpeIndex, wordBpeCount, 0, bpeVectors.ColumnCount);

                // Calcola la media degli embedding dei token BPE per ottenere un embedding rappresentativo per la parola
                var avgVector = wordBpeVectors.RowSums() / wordBpeCount;
                wordVectors.Add(avgVector);

                bpeIndex += wordBpeCount;
            }

            // Crea una matrice dove ogni riga è un embedding medio per una parola
            return Matrix<double>.Build.DenseOfRowVectors(wordVectors);
        }

        /// <summary>
        /// Genera gli allineamenti finali a partire dalle matrici di allineamento e dal mapping BPE-Parola.
        /// </summary>
        /// <param name="alignmentMatrices">Dizionario di matrici di allineamento per ogni metodo.</param>
        /// <param name="srcBpeMap">Mappatura da token BPE a parola per la frase sorgente.</param>
        /// <param name="trgBpeMap">Mappatura da token BPE a parola per la frase target.</param>
        /// <param name="tokenType">Tipo di tokenizzazione ("word" o "bpe").</param>
        /// <returns>Dizionario di allineamenti per ogni metodo.</returns>
        private static Dictionary<string, List<(int, int)>> GenerateAlignments(
            Dictionary<string, Matrix<double>> alignmentMatrices,
            List<int> srcBpeMap,
            List<int> trgBpeMap,
            string tokenType)
        {
            var aligns = new Dictionary<string, List<(int, int)>>();

            foreach (var method in alignmentMatrices.Keys)
            {
                aligns[method] = new List<(int, int)>();
                var matrix = alignmentMatrices[method];

                // Itera attraverso la matrice di allineamento per trovare corrispondenze
                for (int i = 0; i < matrix.RowCount; i++)
                {
                    for (int j = 0; j < matrix.ColumnCount; j++)
                    {
                        if (matrix[i, j] > 0)
                        {
                            // Mappa gli indici dei token BPE agli indici delle parole
                            int srcIndex = tokenType == "bpe" ? srcBpeMap[i] : i;
                            int trgIndex = tokenType == "bpe" ? trgBpeMap[j] : j;
                            aligns[method].Add((srcIndex, trgIndex));
                        }
                    }
                }

                // Rimuovere duplicati e ordinare gli allineamenti
                aligns[method] = aligns[method].Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
            }

            return aligns;
        }
    }

    public class BatchEmbeddings
    {
        public Matrix<double> SourceEmbedding { get; set; }
        public Matrix<double> TargetEmbedding { get; set; }
    }
}
