using Python.Runtime;
using MathNet.Numerics.LinearAlgebra;
using TorchSharp;
using System.Collections.Generic;

namespace SimAlign
{
    public class EmbeddingLoader
    {
        private string _model;
        private torch.Device _device;
        private int _layer;
        private dynamic _embModel;
        private Tokenizer _tokenizer;

        public EmbeddingLoader(string model = "bert-base-multilingual-cased", torch.Device device = null, int layer = 8)
        {
            _model = model;
            _device = device ?? torch.device("cpu");
            _layer = layer;

            InitializeModelAndTokenizer();
            Console.WriteLine($"Initialized the EmbeddingLoader with model: {_model}");
        }

        private void InitializeModelAndTokenizer()
        {
            PythonManager.Initialize();
            using (Py.GIL())
            {
                dynamic transformers = Py.Import("transformers");
                _embModel = transformers.AutoModel.from_pretrained(_model, output_hidden_states: true).to(_device.ToString());
                _embModel.eval();
                _tokenizer = new Tokenizer(_model);
            }
        }

        // Metodo pubblico per tokenizzare una parola
        public List<string> TokenizeWord(string word)
        {
            return _tokenizer.Tokenize(word);
        }

        public List<Matrix<double>> GetEmbedList(List<List<string>> sentBatch)
        {
            if (_embModel == null)
            {
                throw new InvalidOperationException("Embedding model is not initialized.");
            }

            using (Py.GIL())
            {
                // Ottieni gli inputs per il batch di frasi
                dynamic inputs = _tokenizer.Encode(sentBatch, isSplitIntoWords: true);

                // Creazione di un dizionario Python per gli argomenti di parola chiave
                using (PyDict kwargs = new PyDict())
                {
                    foreach (dynamic key in inputs.Keys)
                    {
                        kwargs[key] = inputs[key];
                    }

                    // Invoca il modello con gli argomenti di parola chiave
                    dynamic outputs = _embModel.InvokeMethod("__call__", kwargs);

                    dynamic hiddenStates = outputs.hidden_states;
                    if (_layer >= hiddenStates.Length)
                    {
                        throw new InvalidOperationException($"Specified to take embeddings from layer {_layer}, but model has only {hiddenStates.Length} layers.");
                    }
                    dynamic layerOutputs = hiddenStates[_layer];

                    // Conversione del tensore Python a Matrix<double>
                    dynamic np = Py.Import("numpy");
                    dynamic np_array = layerOutputs.detach().cpu().numpy();

                    // Converti l'array di float a Matrix<double>
                    int sentences = np_array.shape[0];
                    List<Matrix<double>> matrices = new List<Matrix<double>>();

                    for (int s = 0; s < sentences; s++)
                    {
                        int rows = np_array.shape[1];
                        int cols = np_array.shape[2];
                        Matrix<double> matrix = Matrix<double>.Build.Dense(rows, cols);

                        for (int i = 0; i < rows; i++)
                        {
                            for (int j = 0; j < cols; j++)
                            {
                                matrix[i, j] = (double)np_array[s, i, j];
                            }
                        }

                        matrices.Add(matrix);
                    }

                    return matrices;
                }
            }
        }

    }
}
