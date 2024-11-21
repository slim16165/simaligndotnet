SimAlign: Similarity Based Word Aligner
==============

<p align="center">
    <br>
    <img alt="Alignment Example" src="https://raw.githubusercontent.com/cisnlp/simalign/master/assets/example.png" width="300"/>
    <br>
<p>

SimAlign is a high-quality word alignment tool that uses static and contextualized embeddings and **does not require parallel training data**.

The following table shows how it compares to popular statistical alignment models:

|            | ENG-CES | ENG-DEU | ENG-FAS | ENG-FRA | ENG-HIN | ENG-RON |
| ---------- | ------- | ------- | ------- | ------- | ------- | ------- |
| fast-align | .78     | .71     | .46     | .84     | .38     | .68     |
| eflomal    | .85     | .77     | .63     | .93     | .52     | .72     |
| mBERT-Argmax | .87     | .81     | .67     | .94     | .55     | .65     |

Shown is F1, maximum across subword and word level. For more details see the [Paper](https://arxiv.org/pdf/2004.08728.pdf).


Installation and Usage
--------

Tested with Python 3.7, Transformers 3.1.0, Torch 1.5.0. Networkx 2.4 is optional (only required for Match algorithm). 
For full list of dependencies see `setup.py`.
For installation of transformers see [their repo](https://github.com/huggingface/transformers#installation).

Download the repo for use or alternatively install with PyPi

`pip install simalign`

or directly with pip from GitHub

`pip install --upgrade git+https://github.com/cisnlp/simalign.git#egg=simalign`


An example for using our code:
```python
from simalign import SentenceAligner

# making an instance of our model.
# You can specify the embedding model and all alignment settings in the constructor.
myaligner = SentenceAligner(model="bert", token_type="bpe", matching_methods="mai")

# The source and target sentences should be tokenized to words.
src_sentence = ["This", "is", "a", "test", "."]
trg_sentence = ["Das", "ist", "ein", "Test", "."]

# The output is a dictionary with different matching methods.
# Each method has a list of pairs indicating the indexes of aligned words (The alignments are zero-indexed).
alignments = myaligner.get_word_aligns(src_sentence, trg_sentence)

for matching_method in alignments:
    print(matching_method, ":", alignments[matching_method])

# Expected output:
# mwmf (Match): [(0, 0), (1, 1), (2, 2), (3, 3), (4, 4)]
# inter (ArgMax): [(0, 0), (1, 1), (2, 2), (3, 3), (4, 4)]
# itermax (IterMax): [(0, 0), (1, 1), (2, 2), (3, 3), (4, 4)]
```
For more examples of how to use our code see `scripts/align_example.py`.

Demo
--------

An online demo is available [here](https://simalign.cis.lmu.de/).


Gold Standards
--------
Links to the gold standars used in the paper are here: 


| Language Pair  | Citation | Type |Link |
| ------------- | ------------- | ------------- | ------------- |
| ENG-CES | Marecek et al. 2008 | Gold Alignment | http://ufal.mff.cuni.cz/czech-english-manual-word-alignment |
| ENG-DEU | EuroParl-based | Gold Alignment | www-i6.informatik.rwth-aachen.de/goldAlignment/ |
| ENG-FAS | Tvakoli et al. 2014 | Gold Alignment | http://eceold.ut.ac.ir/en/node/940 |
| ENG-FRA |  WPT2003, Och et al. 2000,| Gold Alignment | http://web.eecs.umich.edu/~mihalcea/wpt/ |
| ENG-HIN |   WPT2005 | Gold Alignment | http://web.eecs.umich.edu/~mihalcea/wpt05/ |
| ENG-RON |  WPT2005 Mihalcea et al. 2003 | Gold Alignment | http://web.eecs.umich.edu/~mihalcea/wpt05/ |
        
        
Evaluation Script
--------
For evaluating the output alignments use `scripts/calc_align_score.py`.

The gold alignment file should have the same format as SimAlign outputs.
Sure alignment edges in the gold standard have a '-' between the source and the target indices and the possible edges have a 'p' between indices.
For sample parallel sentences and their gold alignments from ENG-DEU, see `samples`.


Publication
--------

If you use the code, please cite 

```
@inproceedings{jalili-sabet-etal-2020-simalign,
    title = "{S}im{A}lign: High Quality Word Alignments without Parallel Training Data using Static and Contextualized Embeddings",
    author = {Jalili Sabet, Masoud  and
      Dufter, Philipp  and
      Yvon, Fran{\c{c}}ois  and
      Sch{\"u}tze, Hinrich},
    booktitle = "Proceedings of the 2020 Conference on Empirical Methods in Natural Language Processing: Findings",
    month = nov,
    year = "2020",
    address = "Online",
    publisher = "Association for Computational Linguistics",
    url = "https://www.aclweb.org/anthology/2020.findings-emnlp.147",
    pages = "1627--1643",
}
```

Feedback
--------

Feedback and Contributions more than welcome! Just reach out to @masoudjs or @pdufter. 


FAQ
--------

##### Do I need parallel data to train the system?

No, no parallel training data is required.

##### Which languages can be aligned?

This depends on the underlying pretrained multilingual language model used. For example, if mBERT is used, it covers 104 languages as listed [here](https://github.com/google-research/bert/blob/master/multilingual.md).

##### Do I need GPUs for running this?

Each alignment simply requires a single forward pass in the pretrained language model. While this is certainly 
faster on GPU, it runs fine on CPU. On one GPU (GeForce GTX 1080 Ti) it takes around 15-20 seconds to align 500 parallel sentences.



License
-------

Copyright (C) 2020, Masoud Jalili Sabet, Philipp Dufter

A full copy of the license can be found in LICENSE.






---



# SimAlignDotNet

SimAlignDotNet è una libreria C# basata su SimAlign, progettata per calcolare gli allineamenti tra parole di due frasi in lingue diverse. Questa implementazione utilizza modelli pre-addestrati come BERT o RoBERTa tramite TorchSharp e altre librerie per la gestione delle similarità tra embeddings.

## Struttura del Progetto

- **SimAlignDotNet**: Contiene la logica principale per il caricamento dei modelli, l'estrazione degli embeddings e l'allineamento delle parole.
- **SimAlign.ConsoleApp**: Un'applicazione console per eseguire il calcolo degli allineamenti su file o frasi.
- **SimAlign.VisualizeAlignment**: Applicazione WPF per la visualizzazione grafica degli allineamenti.

## Requisiti

- .NET 8.0 SDK
- TorchSharp (libreria C# per l'utilizzo di PyTorch)
- Python 3.7+ (per l'interfacciamento con modelli non direttamente supportati in C#)

## Installazione

1. **Clonare il repository**:
   ```bash
   git clone <URL_DEL_REPOSITORY>
   cd SimAlignDotNet
Configurare le dipendenze:
Installare TorchSharp e altre dipendenze utilizzando NuGet:

bash
Copia codice
dotnet add package TorchSharp
Configurare Python:
Verificare che Python sia installato e accessibile nel PATH.

Utilizzo
1. Console App
Per eseguire l'applicazione console:

bash
Copia codice
cd SimAlign.ConsoleApp
dotnet run
2. Visualizzazione
Per avviare l'applicazione WPF:

bash
Copia codice
cd SimAlign.VisualizeAlignment
dotnet run
3. Test di esempio
Il progetto include esempi nella cartella samples:

sample_eng.txt: Frase in inglese.
sample_deu.txt: Frase corrispondente in tedesco.
sample_eng_deu.gold: File di riferimento per l'allineamento.
Esegui il calcolo degli allineamenti con l'app console o usa la libreria per analisi personalizzate.

4. Programmazione
Esempio di utilizzo in codice C#:

csharp
Copia codice
using SimAlignDotNet;

var aligner = new SentenceAligner();
var srcSentence = "The cat sat on the mat.";
var trgSentence = "Die Katze saß auf der Matte.";

var alignments = aligner.GetWordAlignments(srcSentence, trgSentence);
foreach (var alignment in alignments)
{
    Console.WriteLine($"{alignment.Item1} -> {alignment.Item2}");
}
Licenza
Questo progetto è distribuito sotto la licenza MIT. Per maggiori dettagli, consulta il file LICENSE.



## Riferimenti

Jalili Sabet, Masoud, Philipp Dufter, François Yvon, and Hinrich Schütze.  
"**SimAlign: High Quality Word Alignments without Parallel Training Data using Static and Contextualized Embeddings**."  
Proceedings of the 2020 Conference on Empirical Methods in Natural Language Processing: Findings, Association for Computational Linguistics, 2020, pp. 1627–1643.  
[Link al paper](https://www.aclweb.org/anthology/2020.findings-emnlp.147)
