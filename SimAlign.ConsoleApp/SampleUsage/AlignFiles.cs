using SimAlign.Core.Alignment;
using SimAlign.Core.Config;

namespace SimAlign.ConsoleApp.SampleUsage
{
    class AlignFiles
    {
        public static void ProcessFiles(string sourcePath, string targetPath, string outputPath)
        {
            var sourceLines = File.ReadAllLines(sourcePath);
            var targetLines = File.ReadAllLines(targetPath);

            if (sourceLines.Length != targetLines.Length)
                throw new InvalidOperationException("I file sorgente e target devono avere lo stesso numero di righe.");

            // Configurazione dell'allineamento
            var config = new AlignmentConfig
            {
                Model = "bert-base-multilingual-cased",
                TokenType = "bpe",
                Distortion = 0.5f,
                MatchingMethods = new List<string> { "mwmf", "itermax" },
                Device = "cpu",
                Layer = 8
            };

            // Inizializza il SentenceAligner
            var aligner = new SentenceAligner(config);

            using var writer = new StreamWriter(outputPath);

            for (int i = 0; i < sourceLines.Length; i++)
            {
                // Ottieni le frasi sorgente e target
                var sourceSentence = sourceLines[i];
                var targetSentence = targetLines[i];

                try
                {
                    // Esegui l'allineamento
                    var alignments = aligner.AlignSentences(
                        new List<string> { sourceSentence },
                        new List<string> { targetSentence }
                    );

                    // Scrivi i risultati nel file di output
                    foreach (var method in alignments.Keys)
                    {
                        writer.WriteLine($"Metodo: {method}, Coppia Frasi {i + 1}:");
                        foreach (var pair in alignments[method])
                        {
                            writer.WriteLine($"Sorgente: {pair.Item1}, Bersaglio: {pair.Item2}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Errore durante l'elaborazione della coppia di frasi {i + 1}: {ex.Message}");
                    writer.WriteLine($"Errore durante l'elaborazione della coppia di frasi {i + 1}: {ex.Message}");
                }
            }

            Console.WriteLine($"Allineamenti salvati in {outputPath}");
        }

        public static void ProcessFilesInteractive()
        {
            Console.Clear();
            Console.WriteLine("==== Calcola Allineamenti da File ====");

            Console.Write("Percorso file sorgente: ");
            var sourcePath = Console.ReadLine();

            Console.Write("Percorso file target: ");
            var targetPath = Console.ReadLine();

            Console.Write("Percorso file di output: ");
            var outputPath = Console.ReadLine();

            try
            {
                ProcessFiles(sourcePath, targetPath, outputPath);
                Console.WriteLine($"Allineamenti scritti con successo in {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore: {ex.Message}");
            }

            Console.WriteLine("\nPremi un tasto per tornare al menu...");
            Console.ReadKey();
        }
    }
}
