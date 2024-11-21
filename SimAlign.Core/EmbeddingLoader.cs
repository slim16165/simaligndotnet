using Python.Runtime;
using MathNet.Numerics.LinearAlgebra;
using TorchSharp;
using System.Runtime.InteropServices;

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
            LogEnvironmentVariables();

            using (Py.GIL())
            {
                Environment.SetEnvironmentVariable("PYTORCH_DEBUG", "1");
                dynamic transformers = Py.Import("transformers");
                Console.WriteLine("Transformers imported successfully");
                _embModel = transformers.AutoModel.from_pretrained(_model, output_hidden_states: true).to(_device.ToString());
                _embModel.eval();
                _tokenizer = new Tokenizer(_model);
            }
        }

        // Metodo per loggare variabili d'ambiente e percorsi rilevanti
        private static void LogEnvironmentVariables()
        {
            Console.WriteLine($"PythonHome: {PythonEngine.PythonHome}");
            Console.WriteLine($"PythonPath: {PythonEngine.PythonPath}");

            string dllPath = @"C:\Python311\Lib\site-packages\torch\lib\torch_cpu.dll";
            Console.WriteLine($"DLL Path Exists: {System.IO.File.Exists(dllPath)}");
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

                    // Exclude [CLS] and [SEP] tokens: slice from 1 to -1
                    dynamic slicedOutputs = layerOutputs.slice(1, -1, 0); // Assuming batch dimension is first

                    // Conversione del tensore Python a Matrix<double>
                    dynamic np = Py.Import("numpy");
                    dynamic np_array = slicedOutputs.detach().cpu().numpy();

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
