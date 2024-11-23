using MathNet.Numerics.LinearAlgebra;

namespace SimAlign.Core.Services
{
    public static class SimilarityCalculator
    {
        public static Matrix<double> CalculateCosineSimilarity(Matrix<double> X, Matrix<double> Y)
        {
            Matrix<double> dotProduct = X * Y.Transpose();
            Vector<double> XNormVector = X.RowNorms(2.0);
            Vector<double> YNormVector = Y.RowNorms(2.0);

            Vector<double> XNormSafe = XNormVector.Map(x => x == 0 ? 1e-10 : x);
            Vector<double> YNormSafe = YNormVector.Map(x => x == 0 ? 1e-10 : x);

            Matrix<double> norms = XNormSafe.ToColumnMatrix() * YNormSafe.ToRowMatrix();
            Matrix<double> similarity = (dotProduct.PointwiseDivide(norms) + 1.0) / 2.0;

            return similarity;
        }

        public static Matrix<double> ApplyDistortion(Matrix<double> simMatrix, double ratio = 0.5)
        {
            int m = simMatrix.RowCount;
            int n = simMatrix.ColumnCount;
            if (m < 2 || n < 2 || ratio == 0.0)
                return simMatrix;

            Matrix<double> posX = Matrix<double>.Build.Dense(m, n, (i, j) => j / (double)(n - 1));
            Matrix<double> posY = Matrix<double>.Build.Dense(m, n, (i, j) => i / (double)(m - 1));

            Matrix<double> distortionMask = (posX - posY).PointwisePower(2) * ratio;
            distortionMask = 1.0 - distortionMask;

            return simMatrix.PointwiseMultiply(distortionMask);
        }
    }
}