namespace SimAlign.VisualizeAlignment
{
    public static class AlignmentUtils
    {
        public static (double[,], double[,]) LineToMatrix(string line, int n, int m)
        {
            double[,] sures = new double[n, m];
            double[,] possibles = new double[n, m];

            string[] elements = line.Split(" ");
            foreach (string elem in elements)
            {
                if (elem.Contains("p"))
                {
                    var parts = elem.Split('p');
                    int i = int.Parse(parts[0]);
                    int j = int.Parse(parts[1]);

                    if (i >= n || j >= m)
                        throw new ArgumentException("Error in Gold Standard alignment!");

                    possibles[i, j] = 1;
                }
                else if (elem.Contains("-"))
                {
                    var parts = elem.Split('-');
                    int i = int.Parse(parts[0]);
                    int j = int.Parse(parts[1]);

                    if (i >= n || j >= m)
                        throw new ArgumentException("Error in Gold Standard alignment!");

                    possibles[i, j] = 1;
                    sures[i, j] = 1;
                }
            }

            return (sures, possibles);
        }
    }
}
