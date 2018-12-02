using System;
using System.Collections.Generic;
using System.Text;
using SolveEquations;
using System.Linq;

namespace SolveEquations
{
    /// <summary>
    /// 因数分解をするクラス
    /// </summary>
    class Factorizations
    {
        /// <summary>
        /// 【未実装】因数分解をする。
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static string Factorization(string f)
        {
            return f;
        }

        /// <summary>
        /// 共通因数でくくる
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static (Monomial,Formula) FactorOut(Formula f)
        {
            var NumFactor = FactorOut(f.NumMonof);
            var DenFactor = FactorOut(f.DenMonof);

            return (
                NumFactor.Item1 / DenFactor.Item1,
                new Formula(NumFactor.Item2, DenFactor.Item2)
                );
        }
        /// <summary>
        /// 共通因数でくくる
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static (Monomial,List<Monomial>) FactorOut(List<Monomial> f)
        {
            if(f.Count() == 0)
            {
                return (1, new List<Monomial> { });
            }
            else if(f.Count() == 1)
            {
                return (f[0], new List<Monomial> { 1 });
            }

            //共通因数を見つける
            var Numbers = GCD(f.Select(x => x.Number).ToList());
            var Charas = GCD(f.Select(x => x.Character.Count() > 0 ? x.Character.GetString() : "").ToList());
            var Recips = LCM(f.Select(x => x.Recipchara.Count() > 0 ? x.Recipchara.GetString() : "").ToList());

            var CommonFactor = new Monomial(
                num: Numbers,
                chara: Charas,
                recipchara: Recips
                );//共通因数

            return (
                CommonFactor,
                f.Select(x => x / CommonFactor).ToList()
                );
        }

        /// <summary>
        /// 最大公約数を求める関数
        /// from https://qiita.com/tawatawa/items/408b872a7092be0d7b3c
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static long GCD(long a,long b)
        {
            if (b == 0) return a;

            return GCD(b, a % b);
        }
        public static int GCD(int a, int b)
        {
            if (b == 0) return a;

            return GCD(b, a % b);
        }
        public static long GCD(List<long> numbers)
        {
            switch (numbers.Count())
            {
                case 0:
                    return 1;

                case 1:
                    return numbers[0];

                default:
                    return numbers.Aggregate((now, next) => GCD(now, next));
            }
        }
        public static int GCD(List<int> numbers)
        {
            switch (numbers.Count())
            {
                case 0:
                    return 1;

                case 1:
                    return numbers[0];

                default:
                    return numbers.Aggregate((now, next) => GCD(now, next));
            }
        }
        public static Fraction GCD(List<Fraction> numbers)
        {
            var denomiLCM = LCM(numbers.Select(x => (long)x.Denominator).ToList());
            var numeraGCD = GCD(numbers.Select(x => (long)x.Numerator).ToList());

            return new Fraction(numeraGCD, denomiLCM);
        }
        public static double GCD(List<double> numbers)
        {
            var resfraction = GCD(numbers.Select(x => new Fraction(x)).ToList());

            return resfraction.Numerator / (double)resfraction.Denominator;
        }
        public static string GCD(List<string> chars)
        {

            var chardics = chars.Select(x =>
            {
                var charlist = x.ToArray()
                                .Distinct()
                                .ToList();

                return charlist.ToDictionary(y => y, y => x.Count(z => z == y));
            });

            var chartypes = chardics.Select(x => x.Keys.ToList())
                                    .Aggregate((now, next) => now.Intersect(next).ToList());

            if (chartypes.Count() == 0) return "";

            var CommonChars = chartypes.Select(x => (character: x, minvalue: chardics.Select(y => y[x]).Min()));

            return CommonChars.Select(x => new string(x.character, x.minvalue))
                              .Aggregate((now, next) => now + next);
        }

        /// <summary>
        /// 最小公倍数を求める関数
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static long LCM(long a,long b)
        {
            return a * b / GCD(a, b);
        }
        public static int LCM(int a, int b)
        {
            return a * b / GCD(a, b);
        }
        public static long LCM(List<long> numbers)
        {
            switch (numbers.Count())
            {
                case 0:
                    return 1;

                case 1:
                    return numbers[0];

                default:
                    return numbers.Aggregate((now, next) => LCM(now, next));
            }
        }
        public static int LCM(List<int> numbers)
        {
            switch (numbers.Count())
            {
                case 0:
                    return 1;

                case 1:
                    return numbers[0];

                default:
                    return numbers.Aggregate((now, next) => LCM(now, next));
            }
        }
        public static string LCM(List<string> chars)
        {
            chars.RemoveAll(x => x == "1");
            if (chars.Count() == 0) return "";
            else
            {
                var chardics = chars.Select(x => x.GroupBy(y => y).ToDictionary(y => y.Key, y => y.Count()));

                var chartypes = chardics.Select(x => x.Keys.ToList())
                                        .Aggregate((now, next) => now.Union(next).ToList())
                                        .Distinct();

                if (chartypes.Count() == 0) return "";

                var CommonChars = chartypes.Select(x => (character: x, maxvalue: chardics.Where(y => y.ContainsKey(x)).Select(y => y[x]).Max()));

                return CommonChars.Select(x => new string(x.character, x.maxvalue))
                                  .Aggregate((now, next) => now + next);
            }
        }

    }
}
