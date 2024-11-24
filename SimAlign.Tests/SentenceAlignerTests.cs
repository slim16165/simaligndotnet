using System.Text.RegularExpressions;
using SimAlign.Core.Alignment;
using SimAlign.Core.Config;
using SimAlign.Core.Utilities;

namespace SimAlign.Tests
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class SentenceAlignerTests
    {
        [SetUp]
        public void Setup()
        {
            // Inizializza Python una sola volta
            PythonManager.Initialize();
        }

        [Test]
        public void TestAlignmentWithSampleFiles()
        {
            // Percorsi ai file di esempio
            string engFile = FileUtility.FindFileSmart("sample_eng.txt");
            string deuFile = FileUtility.FindFileSmart("sample_deu.txt");
            string goldFile = FileUtility.FindFileSmart("sample_eng_deu.gold");

            // Leggi i file
            var engSentences = File.ReadAllLines(engFile).ToList();
            var deuSentences = File.ReadAllLines(deuFile).ToList();

            if (engSentences.Count != deuSentences.Count)
                throw new InvalidOperationException("I file sorgente e target devono avere lo stesso numero di frasi.");

            // Leggi gli allineamenti attesi, filtrando i formati non validi
            var expectedAlignments = File.ReadAllLines(goldFile)
                .SelectMany(line => line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(pair => Regex.IsMatch(pair, @"^\d+-\d+$")) // Solo coppie nel formato x-y
                .Select(pair =>
                {
                    var parts = pair.Split('-');
                    return (int.Parse(parts[0]), int.Parse(parts[1]));
                })
                .ToList();

            // Configurazione per il SentenceAligner
            var config = new AlignmentConfig
            {
                Model = ModelType.BertBaseMultilingualCased,
                TokenType = TokenType.BPE,
                Distortion = 0.5f,
                MatchingMethods = new List<MatchingMethod> { MatchingMethod.Intersection },
                Device = DeviceType.CPU,
                Layer = 8
            };

            // Inizializza l'allineatore
            var aligner = new SentenceAligner(config);

            // Verifica ogni coppia di frasi
            for (int i = 0; i < engSentences.Count; i++)
            {
                var engSentence = engSentences[i];
                var deuSentence = deuSentences[i];

                try
                {
                    // Esegui l'allineamento
                    var alignments = aligner.AlignSentences(
                        [engSentence],
                        [deuSentence]
                    );

                    // Estrai gli allineamenti con il metodo "inter"
                    var alignmentMethod = MatchingMethod.Intersection;
                    Assert.That(alignments.ContainsKey(alignmentMethod), $"Metodo di allineamento {alignmentMethod} mancante per la frase {i + 1}.");
                    var actualAlignments = alignments[alignmentMethod];


                    // Confronta il numero di allineamenti
                    Assert.That(actualAlignments.Count, Is.EqualTo(expectedAlignments.Count), $"Il numero di allineamenti non corrisponde per la frase {i + 1}.");

                    // Verifica ogni allineamento atteso
                    foreach (var alignment in expectedAlignments)
                    {
                        Assert.That(actualAlignments, Does.Contain(alignment), $"Allineamento mancante: {alignment.Item1} -> {alignment.Item2}");
                    }
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Errore durante l'allineamento della frase {i + 1}: {ex.Message}");
                }
            }
        }
    }
}
