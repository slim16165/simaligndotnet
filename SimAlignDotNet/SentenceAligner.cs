using MathNet.Numerics.LinearAlgebra;
using TorchSharp;
using Google.OrTools.Graph;

namespace SimAlign
{
    public class SentenceAligner
    {
        private string _model;
        private string _tokenType;
        private float _distortion;
        private List<string> _matchingMethods;
        private torch.Device _device;
        private EmbeddingLoader _embedLoader;

        public SentenceAligner(string model = "bert", string tokenType = "bpe", float distortion = 0.0f, string matchingMethods = "mai", string device = "cpu", int layer = 8)
        {
            Dictionary<string, string> modelNames = new Dictionary<string, string>
            {
                {"bert", "bert-base-multilingual-cased"},
                {"xlmr", "xlm-roberta-base"}
            };

            Dictionary<char, string> allMatchingMethods = new Dictionary<char, string>
            {
                {'a', "inter"},
                {'m', "mwmf"},
                {'i', "itermax"},
                {'f', "fwd"},
                {'r', "rev"}
            };

            _model = modelNames.ContainsKey(model) ? modelNames[model] : model;
            _tokenType = tokenType;
            _distortion = distortion;
            _matchingMethods = matchingMethods.Select(m => allMatchingMethods[m]).ToList();
            _device = torch.device(device);

            _embedLoader = new EmbeddingLoader(model: _model, device: _device, layer: layer);
        }

        public static Matrix<double> GetMaxWeightMatch(Matrix<double> sim)
        {
            int m = sim.RowCount;
            int n = sim.ColumnCount;

            // Creazione dell'istanza di LinearSumAssignment
            LinearSumAssignment assignment = new LinearSumAssignment();

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    // OR-Tools risolve minimizzando il costo, quindi usiamo pesi negativi
                    int cost = -(int)(sim[i, j] * 1000); // Scala i pesi se necessario
                    assignment.AddArcWithCost(i, j, cost);
                }
            }

            // Risolvi il problema di assegnazione
            assignment.Solve();

            // Crea la matrice di allineamento
            Matrix<double> alignmentMatrix = Matrix<double>.Build.Dense(m, n, 0.0);

            for (int i = 0; i < m; i++)
            {
                int assigned = assignment.RightMate(i);
                if (assigned != -1) // Verifica che un nodo sia stato assegnato
                {
                    alignmentMatrix[i, assigned] = 1.0;
                }
            }

            return alignmentMatrix;
        }

        public static Matrix<double> GetSimilarity(Matrix<double> X, Matrix<double> Y)
        {
            // Compute cosine similarity between X and Y
            Matrix<double> dotProduct = X * Y.Transpose();
            Vector<double> XNormVector = X.RowNorms(2.0);
            Vector<double> YNormVector = Y.RowNorms(2.0);

            // Evita divisioni per zero
            Vector<double> XNormSafe = XNormVector.Map(x => x == 0 ? 1e-10 : x);
            Vector<double> YNormSafe = YNormVector.Map(x => x == 0 ? 1e-10 : x);

            Matrix<double> norms = XNormSafe.ToColumnMatrix() * YNormSafe.ToRowMatrix();
            Matrix<double> similarity = (dotProduct.PointwiseDivide(norms) + 1.0) / 2.0;

            return similarity;
        }

        public static List<Matrix<double>> AverageEmbedsOverWords(List<Matrix<double>> bpeVectors, List<List<List<string>>> wordTokensPair)
        {
            // Average BPE embeddings over words
            List<Matrix<double>> averagedMatrices = new List<Matrix<double>>();

            for (int l = 0; l < 2; l++)
            {
                List<Vector<double>> wVectors = new List<Vector<double>>();
                int bpeIndex = 0;

                foreach (List<string> word in wordTokensPair[l])
                {
                    int wordBpeCount = word.Count;
                    if (bpeIndex + wordBpeCount > bpeVectors[l].RowCount)
                    {
                        throw new ArgumentException("Mismatch between BPE vectors and word tokens.");
                    }

                    Matrix<double> wordBpeVectors = bpeVectors[l].SubMatrix(bpeIndex, wordBpeCount, 0, bpeVectors[l].ColumnCount);
                    Vector<double> avgVector = wordBpeVectors.RowSums() / wordBpeCount;
                    wVectors.Add(avgVector);
                    bpeIndex += wordBpeCount;
                }

                // Costruisci una matrice dove ogni riga è un vettore medio per una parola
                Matrix<double> averagedMatrix = Matrix<double>.Build.DenseOfRowVectors(wVectors);
                averagedMatrices.Add(averagedMatrix);
            }

            return averagedMatrices;
        }

        public static (Matrix<double>, Matrix<double>) GetAlignmentMatrix(Matrix<double> simMatrix)
        {
            int m = simMatrix.RowCount;
            int n = simMatrix.ColumnCount;
            Matrix<double> forward = Matrix<double>.Build.Dense(m, n);
            Matrix<double> backward = Matrix<double>.Build.Dense(n, m);

            for (int i = 0; i < m; i++)
            {
                int maxIndex = simMatrix.Row(i).MaximumIndex();
                forward[i, maxIndex] = 1.0;
            }

            for (int j = 0; j < n; j++)
            {
                int maxIndex = simMatrix.Column(j).MaximumIndex();
                backward[j, maxIndex] = 1.0;
            }

            return (forward, backward.Transpose());
        }

        public static Matrix<double> ApplyDistortion(Matrix<double> simMatrix, double ratio = 0.5)
        {
            int m = simMatrix.RowCount;
            int n = simMatrix.ColumnCount;
            if (m < 2 || n < 2 || ratio == 0.0)
                return simMatrix;

            // Costruisci posX (m x n) e posY (m x n)
            Matrix<double> posX = Matrix<double>.Build.Dense(m, n, (i, j) => j / (double)(n - 1));
            Matrix<double> posY = Matrix<double>.Build.Dense(m, n, (i, j) => i / (double)(m - 1)); // Corretto rispetto a prima

            // Calcola distortionMask
            Matrix<double> distortionMask = (posX - posY).PointwisePower(2) * ratio;
            distortionMask = 1.0 - distortionMask;

            // Applica il distortionMask alla matrice simMatrix
            return simMatrix.PointwiseMultiply(distortionMask);
        }

        public static Matrix<double> IterMax(Matrix<double> simMatrix, int maxCount = 2)
        {
            int m = simMatrix.RowCount;
            int n = simMatrix.ColumnCount;
            double alphaRatio = 0.9;

            (Matrix<double> forward, Matrix<double> backward) = GetAlignmentMatrix(simMatrix);
            Matrix<double> inter = forward.PointwiseMultiply(backward);

            if (Math.Min(m, n) <= 2)
                return inter;

            Matrix<double> newInter = Matrix<double>.Build.Dense(m, n);
            int count = 1;

            while (count < maxCount)
            {
                Vector<double> rowSums = inter.RowSums();
                Vector<double> colSums = inter.ColumnSums();

                Matrix<double> maskX = (Matrix<double>.Build.Dense(m, 1, 1.0) - rowSums.ToColumnMatrix()).PointwiseClamp(0.0, 1.0);
                Matrix<double> maskY = (Matrix<double>.Build.Dense(1, n, 1.0) - colSums.ToRowMatrix()).PointwiseClamp(0.0, 1.0);
                Matrix<double> mask = ((alphaRatio * maskX) + (alphaRatio * maskY)).PointwiseClamp(0.0, 1.0);
                Matrix<double> maskZeros = 1.0 - ((1.0 - maskX) * (1.0 - maskY));

                if (rowSums.Sum() < 1.0 || colSums.Sum() < 1.0)
                {
                    mask = Matrix<double>.Build.Dense(mask.RowCount, mask.ColumnCount, 0.0);
                    maskZeros = Matrix<double>.Build.Dense(maskZeros.RowCount, maskZeros.ColumnCount, 0.0);
                }

                Matrix<double> newSim = simMatrix.PointwiseMultiply(mask);
                (Matrix<double> fwd, Matrix<double> bac) = GetAlignmentMatrix(newSim);
                fwd = fwd.PointwiseMultiply(maskZeros);
                bac = bac.PointwiseMultiply(maskZeros);
                newInter = fwd.PointwiseMultiply(bac); // Corrected duplication

                if ((inter + newInter - inter).L1Norm() < 1e-6) // Tolleranza per uguaglianza tra matrici
                    break;

                inter += newInter;
                count += 1;
            }

            return inter;
        }

        public Dictionary<string, List<(int, int)>> GetWordAligns(List<string> srcSent, List<string> trgSent)
        {
            // Tokenizzazione
            List<List<string>> l1_tokens = new List<List<string>>();
            foreach (string word in srcSent)
            {
                l1_tokens.Add(_embedLoader.TokenizeWord(word));
            }

            List<List<string>> l2_tokens = new List<List<string>>();
            foreach (string word in trgSent)
            {
                l2_tokens.Add(_embedLoader.TokenizeWord(word));
            }

            // Creazione delle liste di BPE e mappatura BPE a parole
            List<List<string>> bpe_lists = new List<List<string>> { new List<string>(), new List<string>() };
            List<int> l1_b2w_map = new List<int>();
            List<int> l2_b2w_map = new List<int>();

            // Mappatura per la prima lingua
            for (int i = 0; i < l1_tokens.Count; i++)
            {
                foreach (string bpe in l1_tokens[i])
                {
                    bpe_lists[0].Add(bpe);
                    l1_b2w_map.Add(i);
                }
            }

            // Mappatura per la seconda lingua
            for (int i = 0; i < l2_tokens.Count; i++)
            {
                foreach (string bpe in l2_tokens[i])
                {
                    bpe_lists[1].Add(bpe);
                    l2_b2w_map.Add(i);
                }
            }

            // Ottieni gli embeddings
            List<Matrix<double>> vectors = _embedLoader.GetEmbedList(new List<List<string>> { srcSent, trgSent });

            if (vectors == null || vectors.Count != 2)
            {
                throw new InvalidOperationException("Embeddings not obtained or incomplete.");
            }

            Matrix<double> vector0 = vectors[0];
            Matrix<double> vector1 = vectors[1];

            // Media degli embeddings se necessario
            List<Matrix<double>> averagedMatrices = new List<Matrix<double>>();
            if (_tokenType == "word")
            {
                averagedMatrices = AverageEmbedsOverWords(new List<Matrix<double>> { vector0, vector1 }, new List<List<List<string>>> { l1_tokens, l2_tokens });
            }
            else
            {
                // Se non è "word", usiamo gli embeddings BPE direttamente
                averagedMatrices.Add(vector0);
                averagedMatrices.Add(vector1);
            }

            // Calcolo della similarità
            Matrix<double> sim = GetSimilarity(averagedMatrices[0], averagedMatrices[1]);
            sim = ApplyDistortion(sim, _distortion);

            // Calcolo delle matrici di allineamento
            Dictionary<string, Matrix<double>> all_mats = new Dictionary<string, Matrix<double>>
            {
                { "fwd", GetAlignmentMatrix(sim).Item1 },
                { "rev", GetAlignmentMatrix(sim).Item2 }
            };

            all_mats["inter"] = all_mats["fwd"].PointwiseMultiply(all_mats["rev"]);

            if (_matchingMethods.Contains("mwmf"))
            {
                all_mats["mwmf"] = GetMaxWeightMatch(sim);
            }

            if (_matchingMethods.Contains("itermax"))
            {
                all_mats["itermax"] = IterMax(sim);
            }

            // Genera gli allineamenti
            Dictionary<string, List<(int, int)>> aligns = new Dictionary<string, List<(int, int)>>();

            foreach (string method in _matchingMethods)
            {
                aligns[method] = new List<(int, int)>();
            }

            for (int i = 0; i < sim.RowCount; i++)
            {
                for (int j = 0; j < sim.ColumnCount; j++)
                {
                    foreach (string method in _matchingMethods)
                    {
                        if (all_mats[method][i, j] > 0)
                        {
                            int srcIndex = _tokenType == "bpe" ? l1_b2w_map[i] : i;
                            int trgIndex = _tokenType == "bpe" ? l2_b2w_map[j] : j;
                            aligns[method].Add((srcIndex, trgIndex));
                        }
                    }
                }
            }

            // Ordina gli allineamenti e rimuovi duplicati
            foreach (string method in aligns.Keys.ToList())
            {
                aligns[method] = aligns[method].Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
            }

            return aligns;
        }
    }
}
