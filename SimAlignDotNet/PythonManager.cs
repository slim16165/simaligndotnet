using Microsoft.Extensions.Configuration;
using Python.Runtime;

public static class PythonManager
{
    public static void Initialize()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        string pythonDll = configuration["Python:PythonDLL"];
        string pythonHome = configuration["Python:PythonHome"];

        Runtime.PythonDLL = pythonDll;
        PythonEngine.PythonHome = pythonHome;

        PythonEngine.Initialize();
    }
}