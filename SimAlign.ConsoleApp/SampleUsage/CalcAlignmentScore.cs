namespace SimAlign.ConsoleApp.SampleUsage
{
    public class CalcAlignmentScore
    {
        public static (double Precision, double Recall, double F1, double AER) CalculateScores(
            string goldFilePath, string generatedFilePath)
        {
            var goldAlignments = LoadAlignments(goldFilePath);
            var generatedAlignments = LoadAlignments(generatedFilePath);

            double totalHits = 0, precisionHits = 0, recallHits = 0;
            double totalGold = goldAlignments.Count;

            foreach (var sentenceId in generatedAlignments.Keys)
            {
                if (!goldAlignments.ContainsKey(sentenceId)) continue;

                var goldSet = goldAlignments[sentenceId];
                var generatedSet = generatedAlignments[sentenceId];

                precisionHits += generatedSet.Intersect(goldSet).Count();
                recallHits += goldSet.Intersect(generatedSet).Count();
                totalHits += generatedSet.Count;
            }

            double precision = precisionHits / totalHits;
            double recall = recallHits / totalGold;
            double f1 = 2 * precision * recall / (precision + recall);
            double aer = 1 - (precisionHits + recallHits) / (totalHits + totalGold);

            return (precision, recall, f1, aer);
        }

        private static Dictionary<string, HashSet<string>> LoadAlignments(string path)
        {
            var alignments = new Dictionary<string, HashSet<string>>();
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('\t');
                var sentenceId = parts[0];
                var alignmentPairs = parts[1].Split(' ');

                if (!alignments.ContainsKey(sentenceId))
                    alignments[sentenceId] = new HashSet<string>();

                foreach (var pair in alignmentPairs)
                    alignments[sentenceId].Add(pair);
            }

            return alignments;
        }

        public static void CalculateScoresInteractive()
        {
            Console.Clear();
            Console.WriteLine("==== Calcolo Metriche di Allineamento ====");

            Console.Write("Percorso file di riferimento (gold): ");
            var goldPath = Console.ReadLine();

            Console.Write("Percorso file generato: ");
            var generatedPath = Console.ReadLine();

            try
            {
                var (precision, recall, f1, aer) = CalculateScores(goldPath, generatedPath);
                Console.WriteLine($"Precisione: {precision:P2}");
                Console.WriteLine($"Richiamo: {recall:P2}");
                Console.WriteLine($"F1: {f1:P2}");
                Console.WriteLine($"AER: {aer:P2}");
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
