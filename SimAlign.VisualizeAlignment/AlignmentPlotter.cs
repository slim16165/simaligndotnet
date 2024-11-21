namespace SimAlign.VisualizeAlignment
{
    public static class AlignmentPlotter
    {
        public static double[,] CombineMatrices(double[,] sures, double[,] possibles)
        {
            int n = sures.GetLength(0);
            int m = sures.GetLength(1);
            double[,] combined = new double[n, m];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    combined[i, j] = 0.75 * sures[i, j] + 0.4 * possibles[i, j];
                }
            }

            return combined;
        }
    }
}