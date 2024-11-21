using Python.Runtime;

namespace SimAlign;

public static class PythonManager
{
    public static void Initialize()
    {
        // Percorso alla libreria Python
        Runtime.PythonDLL = @"C:\Python311\python311.dll"; 
        PythonEngine.PythonHome = @"C:\Python311"; 

        // Inizializzazione
        PythonEngine.Initialize();
    }
}