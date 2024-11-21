namespace SimAlign.ConsoleApp.SampleUsage
{
    class AlignFiles
    {
        public static void ProcessFiles(string sourcePath, string targetPath, string outputPath)
        {
            var sourceLines = File.ReadAllLines(sourcePath);
            var targetLines = File.ReadAllLines(targetPath);

            if (sourceLines.Length != targetLines.Length)
                throw new InvalidOperationException("Source and target files must have the same number of lines.");

            var aligner = new SentenceAligner();
            using var writer = new StreamWriter(outputPath);

            for (int i = 0; i < sourceLines.Length; i++)
            {
                var sourceSentence = sourceLines[i].Split(' ').ToList();
                var targetSentence = targetLines[i].Split(' ').ToList();

                var alignments = aligner.GetWordAligns(sourceSentence, targetSentence);

                foreach (var method in alignments.Keys)
                {
                    writer.WriteLine($"Method: {method}, Sentence Pair {i + 1}:");
                    writer.WriteLine(string.Join(", ", alignments[method]));
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