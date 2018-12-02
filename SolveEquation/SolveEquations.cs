using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using MathNet.Numerics.LinearAlgebra.Double;
using System.Threading.Tasks;
using FactMemory.Resx;
using FactMemory.Funcs;

namespace SolveEquations
{
    //方程式を計算したい

    public static class SolveEquations
    {
        public async static Task<List<Complex>> SolveEquation(string f)
        {
            Formula formula = await ExpandFormula.ExpandCharFormula(f);

            //数式に分母がないことを確認
            if(!(formula.DenMonof.IsOne() || formula.IsZero()))
            {
                //Util.Trace.WriteLine(formula.DenMonof[0].GetString());
                throw new Exception(AppResources.TooComplicatedX);
            }

            var chars = formula.GetCharDics();
            //変数が一つのみであることを確認
            if (chars.Count() != 1)
            {
                throw new Exception(AppResources.ManyOrFewVariables);
            }

            var target = chars.First().Key;

            //変数が関数やルートの中にないことを確認
            if (formula.GetTargetCharInfo(target).NumMonof.Any(x => x.NumFuncs.Count() != 0 
                                                              || x.DenFuncs.Count() != 0 
                                                              || x.NthRoots.Count() != 0 
                                                              || x.Recipchara.Count() != 0))
            {
                try
                {
                    var resforms = await SolveAbout.SolveAboutX(formula, new Formula(new List<Monomial> { }), 'x');

                    if(resforms.Count() != 0 && resforms.All(x => x.IsDouble()))
                    {
                        return (await resforms.Select(async x => new Complex(await x.ToDouble(), 0.0)).WhenAll()).ToList();
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    throw new Exception(AppResources.TooComplicatedX);
                }
            }

            var monos = formula.NumMonof;

            var maxDegree = monos.Max(x => x.Character.ContainsKey(target) ? x.Character[target] : 0);

            var Coefficients = Enumerable.Repeat(0.0, maxDegree + 1).ToList();
            
            foreach(var m in monos)
            {
                if (!m.Character.ContainsKey(target))
                {
                    Coefficients[0] = m.Number.ToDouble();
                }
                else
                {
                    Coefficients[m.Character[target]] = m.Number.ToDouble();
                }
            }

            return SolveEquation(Coefficients);
        }

        /// <summary>
        /// 数式の2次方程式を解く
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public async static Task<List<Formula>> SolveEquation(Formula a,Formula b, Formula c)
        {
            if(a.IsDouble() && b.IsDouble() && b.IsDouble())
            {
                return SolveEquation(new List<double> { await c.ToDouble(), await b.ToDouble(), await a.ToDouble() })
                    .Where(x => x.Imaginary == 0)
                    .Select(x => (Formula)x.Real)
                    .ToList();
            }

            //２次方程式の解の公式に当てはめる

            return new List<Formula> {
                (Formula.CalcRoot((b ^ 2) - (Formula)4 * a * c, 2) - b) / ((Formula)2 * a),
                (-Formula.CalcRoot((b ^ 2) - (Formula)4 * a * c, 2) - b) / ((Formula)2 * a)
            };
        }

        /// <summary>
        /// 何次方程式でも解いてやるさ
        /// from: http://geisterchor.blogspot.com/2011/07/c.html?m=1
        /// </summary>
        /// <param name="a">次数の小さい順の係数</param>
        /// <returns></returns>
        public static List<Complex> SolveEquation(List<double> a)
        {
            if(a.Count() <= 1)
            {
                throw new Exception("方程式の次数が少なすぎます");
            }

            var MaxCoefficient = a.Last();

            a = a.Select(x => x / MaxCoefficient).ToList();

            a.RemoveAt(a.Count() - 1);

            a = a.Select(x => -x).ToList();

            var table = new double[a.Count(), a.Count()];

            for(int i = 0; i < a.Count() - 1; i++)
            {
                table[i, i + 1] = 1;
            }

            for(int i = 0; i < a.Count(); i++)
            {
                table[a.Count() - 1, i] = a[i];
            }

            var M = DenseMatrix.OfArray(table);

            var EVD = M.Evd();

            return EVD.EigenValues.Select(x => new Complex(x.Real, x.Imaginary)).ToList();
        }


        //以下,以前使っていたアルゴリズム

        /// <summary>
        /// ax+b=0の計算(現在は不使用)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Complex[] SolveEquation(double a, double b)
        {
            if (a != 0.0)
            {
                return new Complex[1] { -(b / a) };
            }
            else
            {
                throw new DivideByZeroException();
            }
        }

        /// <summary>
        /// ax^2+bx+c=0の計算(現在は不使用)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static Complex[] SolveEquation(double a, double b, double c)
        {
            if (a == 0)
            {
                return SolveEquation(b, c);
            }


            double D = Math.Pow(b, 2) - 4 * a * c;

            Complex[] results = new Complex[2];

            Complex SqrtD = Complex.Sqrt(D);

            results[0] = (-b + SqrtD) / (2 * a);
            results[1] = (-b - SqrtD) / (2 * a);

            return results;
        }

        /// <summary>
        /// ax^3+bx^2+cx+d=0の計算(現在は不使用)
        /// 参考:http://izmiz.hateblo.jp/entry/2014/11/03/214837
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public static Complex[] SolveEquation(double a, double b, double c, double d)
        {
            if (a == 0)
            {
                return SolveEquation(b, c, d);
            }

            Complex[] results = new Complex[3];

            if (d == 0)
            {
                results[0] = 0;

                var Qresult = SolveEquation(a, b, c);

                results[1] = Qresult[0];
                results[2] = Qresult[1];
            }
            else
            {
                results[0] = SolveCubicEqByNewton(a, b, c, d, 0.0001, 1000);

                double D1 = b * b - 3 * a * c;
                double D2 = -4 * a * c * c * c - 27 * a * a * d * d + b * b * c * c + 18 * a * b * c * d - 4 * b * b * b * d;

                if (D1 > 0 && D2 == 0)//2つの実数解(重解あり)
                {
                    double M = b / a + results[0].Real;
                    double N = -d / (a * results[0].Real);

                    if (Math.Pow(M, 2) - 4 * N < 0.001)
                    {
                        results[1] = -M / 2;
                        results[2] = results[1];
                    }
                    else
                    {
                        results[1] = results[0];
                        results[2] = results[1];
                        results[0] = -d / (a * results[1].Real);
                    }
                }
                else//1つの実数解と2つの複素数解or3つの実数解
                {
                    double M = b / a + results[0].Real;
                    double N = -d / (a * results[0].Real);

                    var Qresult = SolveEquation(1, M, N);

                    results[1] = Qresult[0];
                    results[2] = Qresult[1];
                }
            }

            return results;

        }

        /// <summary>
        /// ニュートン法で三次方程式の一つの解を求める(現在は不使用)
        /// </summary>
        /// <param name="a">三次の係数</param>
        /// <param name="b">二次の係数</param>
        /// <param name="c">一次の係数</param>
        /// <param name="d">零次の係数</param>
        /// <param name="errorrange">正確性-小数で誤差を示す</param>
        /// <param name="trial">試行回数</param>
        /// <returns>結果</returns>
        private static double SolveCubicEqByNewton(double a, double b, double c, double d, double errorrange, int trial)
        {
            //t = ニュートン法の初期値
            double t = 1;

            if (SubstituteCubicEq(a, b, c, d, t) == 0)
            {
                return t;
            }
            else
            {
                double t2 = 0.0;

                while (true)
                {
                    for (int i = trial; i > 0; i--)
                    {
                        t2 = t - SubstituteCubicEq(a, b, c, d, t) / (3 * a * t * t + 2 * b * t + c);

                        if (Math.Abs(t2 - t) < errorrange)
                        {
                            return t2;
                        }
                        t = t2;
                    }

                    t += 0.1;
                }
                

                
            }
        }

        private static double SubstituteCubicEq(double a, double b, double c, double d,double x)
        {
            return a * x * x * x + b * x * x + c * x + d;
        }
    }
    
}
