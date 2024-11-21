using ScottPlot.Plottables;
using System.Windows;

namespace SimAlign.VisualizeAlignment
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadAlignment_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Esempio di dati di input
                string alignmentString = "0-0 1p1 2-1";
                string[] sourceWords = { "Testing", "this", "." };
                string[] targetWords = { "Hier", "wird", "getestet", "." };

                // Converte la stringa di allineamento in matrici
                var (sures, possibles) = AlignmentUtils.LineToMatrix(alignmentString, sourceWords.Length, targetWords.Length);

                // Visualizza il grafico
                PlotAlignment(sures, possibles, sourceWords, targetWords);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Image|*.png",
                    Title = "Save Alignment Image"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    AlignmentPlot.Plot.SavePng(saveFileDialog.FileName, 800, 600);
                    MessageBox.Show($"Image saved to {saveFileDialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PlotAlignment(double[,] sures, double[,] possibles, string[] sourceWords, string[] targetWords)
        {
            var plt = AlignmentPlot.Plot;
            plt.Clear();

            // Heatmap
            double[,] heatmapData = AlignmentPlotter.CombineMatrices(sures, possibles);
            var heatmap = new Heatmap(heatmapData)
            {
                Colormap = new ScottPlot.Colormaps.Viridis()
            };
            plt.PlottableList.Add(heatmap);

            // Annotazioni per parole sorgenti
            for (int i = 0; i < sures.GetLength(0); i++)
            {
                plt.Add.Text(sourceWords[i], x: -0.5, y: i);
            }

            // Annotazioni per parole target
            for (int j = 0; j < sures.GetLength(1); j++)
            {
                plt.Add.Text(targetWords[j], x: j, y: sures.GetLength(0) + 0.5);
            }

            // Titolo e rendering
            plt.Title("Alignment Visualization");
            plt.RenderInMemory(800, 600);
        }
    }
}
