using SimAlign.Core.Alignment;
using SimAlign.Core.Config;
using SimAlign.Core.Utilities;

class Program
{
    static void Main()
    {
        try
        {
            // Inizializzazione del PythonManager
            InitializePythonEnvironment();

            // Configura l'allineamento
            var config = new AlignmentConfig
            {
                Model = "bert-base-multilingual-cased",
                TokenType = "bpe",
                Distortion = 0.5f,
                MatchingMethods = new List<string> { "inter", "mwmf", "itermax" },
                Device = "cpu",
                Layer = 8
            };

            // Inizializza il SentenceAligner
            var sentenceAligner = new SentenceAligner(config);

            // Frasi di esempio
            var srcSentences = new List<string> { "This is a test.", "How are you?" };
            var trgSentences = new List<string> { "Questo è un test.", "Come stai?" };

            // Esegui l'allineamento
            var alignments = sentenceAligner.AlignSentences(srcSentences, trgSentences);

            // Stampa i risultati
            PrintAlignments(alignments);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }

    private static void InitializePythonEnvironment()
    {
        try
        {
            PythonManager.Initialize();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Errore durante l'inizializzazione dell'ambiente Python.");
            Console.WriteLine($"Dettagli: {ex.Message}");
            throw;
        }
    }

    private static void PrintAlignments(Dictionary<string, List<(int, int)>> alignments)
    {
        Console.WriteLine("Risultati dell'allineamento:");

        foreach (var method in alignments.Keys)
        {
            Console.WriteLine($"\nMetodo: {method}");
            foreach (var align in alignments[method])
            {
                Console.WriteLine($"{align.Item1} -> {align.Item2}");
            }
        }
    }


}
