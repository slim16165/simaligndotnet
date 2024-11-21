namespace SimAlign.Tests
{
    public class FileUtility
    {
        /// <summary>
        /// Cerca un file risalendo la gerarchia delle cartelle e considerando sottodirectory rilevanti.
        /// </summary>
        /// <param name="fileName">Nome del file da cercare</param>
        /// <returns>Percorso completo del file, se trovato; altrimenti null</returns>
        public static string FindFileSmart(string fileName)
        {
            var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

            // Risali la gerarchia delle cartelle
            while (currentDirectory != null)
            {
                // Escludi cartelle come bin, obj, ecc.
                if (IsIgnoredDirectory(currentDirectory))
                {
                    currentDirectory = currentDirectory.Parent;
                    continue;
                }

                // Controlla direttamente nella directory corrente
                var filePath = Path.Combine(currentDirectory.FullName, fileName);
                if (File.Exists(filePath))
                    return filePath;

                // Cerca nelle sottodirectory (es. modelli in cartelle specifiche)
                var foundInSubDir = Directory.GetFiles(currentDirectory.FullName, fileName, SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(foundInSubDir))
                    return foundInSubDir;

                // Risali di una cartella
                currentDirectory = currentDirectory.Parent;
            }

            // File non trovato
            return null;
        }

        /// <summary>
        /// Determina se una directory dovrebbe essere ignorata nella ricerca.
        /// </summary>
        /// <param name="directory">Directory da valutare</param>
        /// <returns>True se deve essere ignorata, altrimenti false</returns>
        private static bool IsIgnoredDirectory(DirectoryInfo directory)
        {
            var ignoredDirectories = new[] { "bin", "debug", "release", "obj" };
            return ignoredDirectories.Any(ignored =>
                directory.Name.Equals(ignored, StringComparison.OrdinalIgnoreCase));
        }
    }
}
