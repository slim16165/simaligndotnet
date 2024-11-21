using System;
using Microsoft.Extensions.Configuration;
using Python.Runtime;

public static class PythonManager
{
    public static void Initialize()
    {
        Console.WriteLine("Initializing PythonManager...");

        try
        {
            // Carica configurazione da file JSON
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            string pythonDll = configuration["Python:PythonDLL"] ?? @"C:\Python311\python311.dll";
            string pythonHome = configuration["Python:PythonHome"] ?? @"C:\Python311";
            string pythonPath = configuration["Python:PythonPath"] ?? @"C:\Python311\Lib;C:\Python311\Lib\site-packages";

            // Configura il runtime di Python
            ConfigurePythonRuntime(pythonDll, pythonHome, pythonPath);

            // Inizializza Python.NET
            PythonEngine.Initialize();
            Console.WriteLine("Python.NET Initialized successfully.");

            // Esegui un test minimale
            TestPythonEnvironment();
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
            Console.WriteLine("PythonManager initialization completed.");
        }
    }

    private static void ConfigurePythonRuntime(string pythonDll, string pythonHome, string pythonPath)
    {
        Console.WriteLine($"Setting PythonDLL to: {pythonDll}");
        Runtime.PythonDLL = pythonDll;

        Console.WriteLine($"Setting PYTHONHOME to: {pythonHome}");
        Environment.SetEnvironmentVariable("PYTHONHOME", pythonHome);

        Console.WriteLine($"Setting PYTHONPATH to: {pythonPath}");
        Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath);

        // Aggiungi il percorso delle librerie native al PATH
        string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string newPath = $"{currentPath};{pythonHome}\\Lib;{pythonHome}\\DLLs";
        Environment.SetEnvironmentVariable("PATH", newPath);
        Console.WriteLine("Environment variables configured successfully.");
    }

    private static void TestPythonEnvironment()
    {
        Console.WriteLine("Testing Python environment...");

        using (Py.GIL())
        {
            dynamic torch = Py.Import("torch");
            string torchVersion = torch.__version__.ToString();
            Console.WriteLine($"PyTorch Version: {torchVersion}");
        }
    }
}
