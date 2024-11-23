using MathNet.Numerics.LinearAlgebra;
using Python.Runtime;
using SimAlign.Core.Utilities;
using TorchSharp;

namespace SimAlign.Core.Services
{
    public class EmbeddingLoader
    {
        private readonly string _model;
        private readonly torch.Device _device;
        private readonly int _layer;
        private dynamic _embModel;
        private readonly Tokenizer _tokenizer;

        public EmbeddingLoader(string model, string device, int layer, Tokenizer tokenizer)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _device = torch.device(device ?? "cpu");
            _layer = layer;
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));

            InitializeModel();
            Console.WriteLine($"Initialized EmbeddingLoader with model: {_model}");
        }

        private void InitializeModel()
        {
            using (Py.GIL())
            {
                PythonManager.Initialize();
                Environment.SetEnvironmentVariable("PYTORCH_DEBUG", "1");
                Console.WriteLine("Python environment configured.");

                dynamic transformers = Py.Import("transformers");
                Console.WriteLine("Transformers library imported successfully.");

                _embModel = transformers.AutoModel
                    .from_pretrained(_model, output_hidden_states: true)
                    .to(_device.ToString());
                _embModel.eval();
                Console.WriteLine($"Model {_model} loaded on {_device}.");
            }
        }

        public List<Matrix<double>> ComputeEmbeddingsForBatch(List<List<string>> sentencesBatch)
        {
            if (_embModel == null)
            {
                throw new InvalidOperationException("Embedding model is not initialized.");
            }

            using (Py.GIL())
            {
                // Prepara l'input usando il tokenizer
                dynamic inputs = _tokenizer.Encode(sentencesBatch, isSplitIntoWords: true);

                // Esegui il modello e ottieni gli stati nascosti
                dynamic outputs = _embModel.InvokeMethod("__call__", inputs);
                dynamic hiddenStates = outputs.hidden_states;

                // Verifica che il livello esista
                if (_layer >= hiddenStates.Length)
                {
                    throw new InvalidOperationException($"Specified layer {_layer} exceeds available layers ({hiddenStates.Length}).");
                }

                // Ottieni l'output del livello specifico, escludendo token speciali
                dynamic layerOutputs = hiddenStates[_layer];
                dynamic slicedOutputs = layerOutputs.slice(1, -1, 0);

                // Converti l'output in matrici
                return ConvertToMatrices(slicedOutputs);
            }
        }

        private static List<Matrix<double>> ConvertToMatrices(dynamic slicedOutputs)
        {
            dynamic npArray = slicedOutputs.detach().cpu().numpy();

            int numSentences = npArray.shape[0];
            var matrices = new List<Matrix<double>>();

            for (int sentenceIndex = 0; sentenceIndex < numSentences; sentenceIndex++)
            {
                int rows = npArray.shape[1];
                int cols = npArray.shape[2];
                Matrix<double> matrix = Matrix<double>.Build.Dense(rows, cols);

                for (int rowIndex = 0; rowIndex < rows; rowIndex++)
                {
                    for (int colIndex = 0; colIndex < cols; colIndex++)
                    {
                        matrix[rowIndex, colIndex] = (double)npArray[sentenceIndex, rowIndex, colIndex];
                    }
                }

                matrices.Add(matrix);
            }

            return matrices;
        }
    }
}
