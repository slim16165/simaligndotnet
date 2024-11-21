using System.Text.RegularExpressions;

namespace SimAlign.Tests
{
    [TestFixture]
    public class SentenceAlignerTests
    {
        [Test]
        public void TestAlignmentWithSampleFiles()
        {
            // Percorsi ai file di esempio
            string engFile = FileUtility.FindFileSmart("sample_eng.txt");
            string deuFile = FileUtility.FindFileSmart("sample_deu.txt");
            string goldFile = FileUtility.FindFileSmart("sample_eng_deu.gold");

            // Leggi i file
            string engText = File.ReadAllText(engFile);
            string deuText = File.ReadAllText(deuFile);

            // Dividi il testo in liste di parole
            var engWords = engText.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var deuWords = deuText.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();

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

            // Inizializza l'allineatore
            var aligner = new SentenceAligner();
            var alignments = aligner.GetWordAligns(engWords, deuWords);

            // Estrai una chiave di allineamento (ad esempio "inter")
            string alignmentMethod = "inter";
            Assert.That(alignments.ContainsKey(alignmentMethod), $"Metodo di allineamento {alignmentMethod} mancante.");
            var actualAlignments = alignments[alignmentMethod];

            // Confronta con i risultati attesi
            Assert.That(actualAlignments.Count, Is.EqualTo(expectedAlignments.Count), "Il numero di allineamenti non corrisponde.");
            foreach (var alignment in expectedAlignments)
            {
                Assert.That(actualAlignments, Does.Contain(alignment), $"Allineamento mancante: {alignment.Item1} -> {alignment.Item2}");
            }
        }
    }
}
