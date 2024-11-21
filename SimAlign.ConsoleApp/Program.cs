using SimAlign.ConsoleApp.SampleUsage;

namespace SimAlign.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            bool exit = false;

            while (!exit)
            {
                Console.Clear();
                Console.WriteLine("==== SimAlign Console Application ====");
                Console.WriteLine("1. Esempio di allineamento semplice");
                Console.WriteLine("2. Calcola allineamenti da file");
                Console.WriteLine("3. Calcola metriche di allineamento");
                Console.WriteLine("4. Visualizza gli allineamenti");
                Console.WriteLine("0. Esci");
                Console.WriteLine("======================================");
                Console.Write("Seleziona un'opzione: ");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        AlignExample.RunExample();
                        break;
                    case "2":
                        AlignFiles.ProcessFilesInteractive();
                        break;
                    case "3":
                        CalcAlignmentScore.CalculateScoresInteractive();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("Opzione non valida. Premi un tasto per continuare...");
                        Console.ReadKey();
                        break;
                }
            }

            Console.WriteLine("Grazie per aver usato SimAlign Console App!");
        }
    }
}