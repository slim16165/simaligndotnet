using SimAlign.Core.Alignment;
using SimAlign.Core.Config;

namespace SimAlign.ConsoleApp.SampleUsage
{
    public static class AlignExample
    {
        public static void RunExample()
        {
            Console.WriteLine("==== Esempio di Allineamento ====");

            // Frasi di esempio da allineare
            var sourceSentence = "Sir Nils Olav III. was knighted by the norwegian king .";
            var targetSentence = "Nils Olav der Dritte wurde vom norwegischen König zum Ritter geschlagen .";

            // Configurazione per l'allineamento
            var config = new AlignmentConfig
            {
                Model = ModelType.BertBaseMultilingualCased,
                TokenType = TokenType.BPE,
                Distortion = 0.5f,
                MatchingMethods = new List<MatchingMethod> { MatchingMethod.MaxWeightMatch, MatchingMethod.IterativeMax },
                Device = DeviceType.CPU,
                Layer = 8
            };

            try
            {
                // Inizializza l'orchestratore SentenceAligner
                var aligner = new SentenceAligner(config);

                // Esegui l'allineamento
                var alignments = aligner.AlignSentences(
                    new List<string> { sourceSentence },
                    new List<string> { targetSentence }
                );

                // Stampa i risultati
                foreach (var method in alignments.Keys)
                {
                    Console.WriteLine($"Metodo di allineamento: {method}");
                    foreach (var alignment in alignments[method])
                    {
                        Console.WriteLine($"Sorgente: {alignment.Item1} -> Bersaglio: {alignment.Item2}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Si è verificato un errore durante l'allineamento:");
                Console.WriteLine(ex.Message);
            }
        }
    }
}