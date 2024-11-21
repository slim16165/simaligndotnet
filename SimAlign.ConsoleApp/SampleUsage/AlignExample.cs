namespace SimAlign.ConsoleApp.SampleUsage;

public static class AlignExample
{
    public static void RunExample()
    {
        Console.WriteLine("==== Esempio di Allineamento ====");

        var sourceSentence = "Sir Nils Olav III. was knighted by the norwegian king .";
        var targetSentence = "Nils Olav der Dritte wurde vom norwegischen König zum Ritter geschlagen .";

        var aligner = new SentenceAligner();
        var alignments = aligner.GetWordAligns(
            sourceSentence.Split(' ').ToList(),
            targetSentence.Split(' ').ToList()
        );

        foreach (var method in alignments.Keys)
        {
            Console.WriteLine($"Metodo: {method}");
            foreach (var alignment in alignments[method])
            {
                Console.WriteLine($"{alignment.Item1} -> {alignment.Item2}");
            }
        }
    }
}