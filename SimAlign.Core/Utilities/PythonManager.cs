using Microsoft.Extensions.Configuration;
using Python.Runtime;

namespace SimAlign.Core.Utilities;

public static class PythonManager
{
    private static bool _initialized = false;
    private static readonly object _lock = new object();

    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized)
                return;

            Console.WriteLine("Initializing PythonManager...");

            try
            {
                // Configurazione del runtime Python
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();

                string pythonDll = configuration["Python:PythonDLL"] ?? @"C:\Python311\python311.dll";
                string pythonHome = configuration["Python:PythonHome"] ?? @"C:\Python311";
                string pythonPath = configuration["Python:PythonPath"] ?? @"C:\Python311\Lib;C:\Python311\Lib\site-packages";

                Runtime.PythonDLL = pythonDll;
                Environment.SetEnvironmentVariable("PYTHONHOME", pythonHome);
                Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath);

                // Inizializzazione del motore Python
                PythonEngine.Initialize();
                _initialized = true;
                Console.WriteLine("Python.NET initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize Python.NET: {ex.Message}");
                throw;
            }
        }
    }
}