using System;
using Python.Runtime;

class Program
{
    static void Main()
    {
        try
        {
            // Imposta il percorso corretto di Python
            Runtime.PythonDLL = @"C:\Python311\python311.dll";
            Environment.SetEnvironmentVariable("PYTHONHOME", @"C:\Python311");
            Environment.SetEnvironmentVariable("PYTHONPATH", @"C:\Python311\Lib;C:\Python311\Lib\site-packages");

            // Aggiungi il percorso delle librerie native al PATH
            Environment.SetEnvironmentVariable(
                "PATH",
                Environment.GetEnvironmentVariable("PATH") + @";C:\Python311\Lib;C:\Python311\DLLs"
            );

            // Inizializza Python.NET
            PythonEngine.Initialize();

            using (Py.GIL())
            {
                Console.WriteLine("Python.NET Initialized");

                // Test minimale: importa torch
                dynamic torch = Py.Import("torch");
                Console.WriteLine($"PyTorch Version: {torch.__version__}");
            }
        }
        catch (PythonException ex)
        {
            Console.WriteLine($"Python Exception: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
        finally
        {
            PythonEngine.Shutdown();
        }
    }
}