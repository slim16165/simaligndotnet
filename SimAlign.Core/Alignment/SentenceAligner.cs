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

        private List<IAlignmentStrategy> InitializeAlignmentStrategies(List<string> matchingMethods)
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

        public Dictionary<string, List<(int, int)>> AlignSentences(List<string> srcSentences, List<string> trgSentences)
        {
            // Inizializza il contesto con le frasi sorgente e target
            var context = new AlignmentContext
            {
                Source = new AlignmentContextText { Sentences = srcSentences },
                Target = new AlignmentContextText { Sentences = trgSentences }
            };

            // 1. Tokenizzazione delle frasi
            context.Source.Tokens = _tokenizer.TokenizeSentences(context.Source.Sentences);
            context.Target.Tokens = _tokenizer.TokenizeSentences(context.Target.Sentences);

            // 2. Creazione delle liste di BPE e mappatura BPE a parole
            var (bpeSrcList, srcBpeMap) = MapTokensToWords(context.Source.Tokens);
            var (bpeTrgList, trgBpeMap) = MapTokensToWords(context.Target.Tokens);

            // 3. Generazione degli embedding per i token BPE
            var embeddings = _embeddingLoader.ComputeEmbeddingsForBatch(new List<List<string>> { bpeSrcList, bpeTrgList });
            if (embeddings == null || embeddings.Count < 2)
                throw new InvalidOperationException("Embeddings non ottenuti o incompleti.");

            context.Source.Embeddings = embeddings[0];
            context.Target.Embeddings = embeddings[1];

            // 4. Calcolo degli embedding a livello di parola, se necessario
            var averagedMatrices = new List<Matrix<double>>();

            if (_config.TokenType == "word")
            {
                averagedMatrices = ComputeWordEmbeddings(new List<Matrix<double>> { context.Source.Embeddings, context.Target.Embeddings }, new List<List<List<string>>> { context.Source.Tokens, context.Target.Tokens });
            }
            else
            {
                // Se non è "word", usiamo gli embedding BPE direttamente
                averagedMatrices.Add(context.Source.Embeddings);
                averagedMatrices.Add(context.Target.Embeddings);
            }

            var wordEmbeddings = averagedMatrices;
            context.Source.Embeddings = wordEmbeddings[0];
            context.Target.Embeddings = wordEmbeddings[1];

            // 5. Calcolo della matrice di similarità tra le frasi
            context.SimilarityMatrix = SimilarityCalculator.CalculateCosineSimilarity(context.Source.Embeddings, context.Target.Embeddings);
            context.SimilarityMatrix = SimilarityCalculator.ApplyDistortion(context.SimilarityMatrix, _config.Distortion);

            // 6. Applicazione delle strategie di allineamento per generare le matrici di allineamento
            var alignmentMatrices = _alignmentStrategies.ToDictionary(strategy => strategy.Name, strategy => strategy.Align(context.SimilarityMatrix));

            // 7. Generazione degli allineamenti finali a partire dalle matrici di allineamento
            context.Alignments = GenerateAlignments(alignmentMatrices, srcBpeMap, trgBpeMap, _config.TokenType);

            return context.Alignments;
        }


        /// <summary>
        /// Mappa i token BPE alle rispettive parole e crea una lista di token aggregati.
        /// </summary>
        /// <param name="tokens">Lista di liste di token BPE per ogni frase.</param>
        /// <returns>Una tupla contenente la lista aggregata di token BPE e la mappatura da token a parola.</returns>
        private static (List<string>, List<int>) MapTokensToWords(List<List<string>> tokens)
        {
            var bpeList = new List<string>();
            var bpeToWordMap = new List<int>();

            for (int i = 0; i < tokens.Count; i++)
            {
                foreach (var bpe in tokens[i])
                {
                    bpeList.Add(bpe);
                    bpeToWordMap.Add(i); // Indica che questo token BPE appartiene alla parola i
                }
            }

            return (bpeList, bpeToWordMap);
        }

        /// <summary>
        /// Calcola gli embedding a livello di parola aggregando gli embedding dei token BPE.
        /// </summary>
        /// <param name="bpeVectors">Lista di matrici di embedding BPE per le frasi sorgente e target.</param>
        /// <param name="wordTokensPair">Lista di liste di liste di token BPE per le frasi sorgente e target.</param>
        /// <returns>Lista di matrici di embedding a livello di parola.</returns>
        private static List<Matrix<double>> ComputeWordEmbeddings(List<Matrix<double>> bpeVectors, List<List<List<string>>> wordTokensPair)
        {
            var averagedMatrices = new List<Matrix<double>>();

            for (int l = 0; l < 2; l++)
            {
                var wordVectors = new List<Vector<double>>();
                int bpeIndex = 0;

                foreach (var word in wordTokensPair[l])
                {
                    int wordBpeCount = word.Count;

                    // Verifica che non ci sia un disallineamento tra i token BPE e le parole
                    if (bpeIndex + wordBpeCount > bpeVectors[l].RowCount)
                    {
                        throw new ArgumentException("Disallineamento tra BPE vectors e word tokens.");
                    }

                    // Estrai gli embedding dei token BPE che appartengono alla parola corrente
                    var wordBpeVectors = bpeVectors[l].SubMatrix(bpeIndex, wordBpeCount, 0, bpeVectors[l].ColumnCount);

                    // Calcola la media degli embedding dei token BPE per ottenere un embedding rappresentativo per la parola
                    var avgVector = wordBpeVectors.RowSums() / wordBpeCount;
                    wordVectors.Add(avgVector);

                    bpeIndex += wordBpeCount;
                }

                // Crea una matrice dove ogni riga è un embedding medio per una parola
                var averagedMatrix = Matrix<double>.Build.DenseOfRowVectors(wordVectors);
                averagedMatrices.Add(averagedMatrix);
            }

            return averagedMatrices;
        }

        /// <summary>
        /// Genera gli allineamenti finali a partire dalle matrici di allineamento e dal mapping BPE-Parola.
        /// </summary>
        /// <param name="alignmentMatrices">Dizionario di matrici di allineamento per ogni metodo.</param>
        /// <param name="srcBpeMap">Mappatura da token BPE a parola per la frase sorgente.</param>
        /// <param name="trgBpeMap">Mappatura da token BPE a parola per la frase target.</param>
        /// <param name="tokenType">Tipo di tokenizzazione ("word" o "bpe").</param>
        /// <returns>Dizionario di allineamenti per ogni metodo.</returns>
        private static Dictionary<string, List<(int, int)>> GenerateAlignments(Dictionary<string, Matrix<double>> alignmentMatrices, List<int> srcBpeMap, List<int> trgBpeMap, string tokenType)
        {
            var aligns = new Dictionary<string, List<(int, int)>>();

            foreach (var method in alignmentMatrices.Keys)
            {
                aligns[method] = new List<(int, int)>();
                var matrix = alignmentMatrices[method];

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
}
