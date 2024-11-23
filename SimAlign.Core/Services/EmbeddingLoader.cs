﻿using MathNet.Numerics.LinearAlgebra;
using Python.Runtime;
using SimAlign.Core.Utilities;
using TorchSharp;
using System;
using System.Collections.Generic;
using SimAlign.Core.Alignment;

namespace SimAlign.Core.Services
{
    public class EmbeddingLoader
    {
        private readonly string _model;
        private readonly string _device;
        private readonly int _layer;
        private readonly Tokenizer _tokenizer;
        private dynamic _embModel;

        public EmbeddingLoader(string model, string device, int layer, Tokenizer tokenizer)
        {
            _model = model;
            _device = device;
            _layer = layer;
            _tokenizer = tokenizer;

            InitializeModel();
        }

        private void InitializeModel()
        {
            using (Py.GIL())
            {
                dynamic transformers = Py.Import("transformers");

                // Carica il modello e lo invia al device specificato
                _embModel = transformers.AutoModel.from_pretrained(_model);
                _embModel.to(_device);
                _embModel.eval();
                Console.WriteLine($"Model {_model} loaded on {_device}.");
            }
        }

        /// <summary>
        /// Calcola gli embedding per un batch di frasi sorgente e target.
        /// </summary>
        /// <param name="sentencesBatch">Lista di liste di token per sorgente e target.</param>
        /// <returns>Oggetto BatchEmbeddings contenente gli embedding per sorgente e target.</returns>
        public BatchEmbeddings ComputeEmbeddingsForBatch(List<List<string>> sentencesBatch)
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
                dynamic slicedOutputs = layerOutputs.slice(1, -1, 0); // Esclude i token speciali

                // Converti l'output in matrici
                return ConvertToBatchEmbeddings(slicedOutputs);
            }
        }

        /// <summary>
        /// Converte gli output tagliati in un oggetto BatchEmbeddings.
        /// </summary>
        /// <param name="slicedOutputs">Output tagliato dal modello.</param>
        /// <returns>Oggetto BatchEmbeddings contenente gli embedding per sorgente e target.</returns>
        private static BatchEmbeddings ConvertToBatchEmbeddings(dynamic slicedOutputs)
        {
            dynamic npArray = slicedOutputs.detach().cpu().numpy();

            if (npArray.shape[0] != 2)
            {
                throw new ArgumentException($"Expected batch size of 2, but got {npArray.shape[0]}.");
            }

            int embeddingDimension = npArray.shape[2];
            int sourceSeqLength = slicedOutputs[0].shape[0];
            int targetSeqLength = slicedOutputs[1].shape[0];

            // Convert source embeddings
            var sourceMatrix = Matrix<double>.Build.Dense(sourceSeqLength, embeddingDimension);
            for (int i = 0; i < sourceSeqLength; i++)
            {
                for (int j = 0; j < embeddingDimension; j++)
                {
                    sourceMatrix[i, j] = (double)npArray[0, i, j];
                }
            }

            // Convert target embeddings
            var targetMatrix = Matrix<double>.Build.Dense(targetSeqLength, embeddingDimension);
            for (int i = 0; i < targetSeqLength; i++)
            {
                for (int j = 0; j < embeddingDimension; j++)
                {
                    targetMatrix[i, j] = (double)npArray[1, i, j];
                }
            }

            return new BatchEmbeddings
            {
                SourceEmbedding = sourceMatrix,
                TargetEmbedding = targetMatrix
            };
        }

        // Eventuali altri metodi e helper...

    }
}
