using System;
using System.Collections.Generic;
using System.Text;
using MathNet.Numerics.LinearRegression;
using MathNet.Numerics.LinearAlgebra.Double;


namespace SolveEquations
{
    public static class SolveSimultaneous
    {
        /// <summary>
        /// 連立方程式を計算する(未実装)
        /// </summary>
        /// <param name="formulas"></param>
        /// <returns></returns>
        public static double[,] SolveSimultanous(double[,] formulas)
        {
            var M = DenseMatrix.OfArray(formulas);

            return formulas;
        }
    }
}
