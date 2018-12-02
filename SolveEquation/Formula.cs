using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using Util;
using System.Threading.Tasks;
using FactMemory.Funcs;

namespace SolveEquations
{
    /// <summary>
    /// 多項式を表すクラス
    /// </summary>
    public class Formula
    {
        private List<Monomial> _monof;
        private List<Monomial> _denomimonof;

        /// <summary>
        /// 多項式
        /// </summary>
        /// <param name="mono">分子の多項式</param>
        /// <param name="denomimono">分母の多項式</param>
        public Formula(List<Monomial> mono, List<Monomial> denomimono = null)
        {
            if (denomimono == null) denomimono = new List<Monomial> { 1 };
            //monofに代入するだけ
            NumMonof = mono;
            DenMonof = denomimono;

            if (mono.Count() == 0) return;

            Initialise();
        }
        /// <summary>
        /// 多項式(単項式から変換)
        /// </summary>
        /// <param name="mono">単項式</param>
        public Formula(Monomial mono)
        {
            NumMonof = new List<Monomial> { mono };
            DenMonof = new List<Monomial> { 1 };

            Initialise();
        }

        /// <summary>
        /// 多項式(ExpandFormula.ExpandCharFormulaしてから代入)
        /// </summary>
        /// <param name="f">式</param>
        public async static Task<Formula> CreateByStr(string f)
        {
            return await ExpandFormula.ExpandCharFormula(f);
        }

        /// <summary>
        /// 多項式が整理できる場合は整理する
        /// ・分母分子ごとに同類項をまとめる
        /// ・共通因数があれば求める
        /// ・共通因数を掛けて約分し、それを分母分子に分けて掛ける
        /// </summary>
        private void Initialise()
        {
            if (NumMonof.IsZero())
            {
                DenMonof = new List<Monomial> { 1 };
                return;
            }
            if (NumMonof.IsOne() && DenMonof.IsOne()) return;

            //0の項を削除
            NumMonof = NumMonof.Where(x => !x.IsZero()).ToList();
            DenMonof = DenMonof.Where(x => !x.IsZero()).ToList();
            
            //同類項をまとめる
            var SortedNumerator = NumMonof.Grouping(IsNumberKey: false).Select(x =>
            {
                var res = x[0];

                res.Number = x.Select(y => y.Number).Aggregate((now, next) => now + next);

                return res;
            }).ToList();
            var SortedDenominator = DenMonof.Grouping(IsNumberKey: false).Select(x =>
            {
                var res = x[0];

                res.Number = x.Select(y => y.Number).Aggregate((now, next) => now + next);

                return res;
            }).ToList();

            if (SortedNumerator.IsOne() || SortedDenominator.IsOne())
            {
                NumMonof = SortedNumerator;
                DenMonof = SortedDenominator;
                return;
            }

            var NumFactor = Factorizations.FactorOut(SortedNumerator);
            var DenFactor = Factorizations.FactorOut(SortedDenominator);

            Monomial Factor = NumFactor.Item1 / DenFactor.Item1;

            NumMonof = NumFactor.Item2.Select(x => x * Factor.GetNumerator()).ToList();
            DenMonof = DenFactor.Item2.Select(x => x * Factor.GetDenominator()).ToList();

            Reduction();

            Trace.WriteLine("Formula Initialized:" + this.GetString());
        }

        /// <summary>
        /// 約分する
        /// </summary>
        private void Reduction()
        {
            var factors = Factorizations.FactorOut(this);

            NumMonof = (factors.Item1.GetNumerator() * factors.Item2.GetNumerator()).NumMonof;
            DenMonof = (factors.Item1.GetDenominator() * factors.Item2.GetDenominator()).NumMonof;

            if(DenMonof.Count() == 1)//分母が一つだけであれば数字は上につける
            {
                NumMonof = NumMonof.Select(x => x / DenMonof[0].Number.ToDouble()).ToList();
                DenMonof[0].Number = 1;
                return;
            }
            else//上下の式がどちらかの因数である場合は、割り切る
            {
                var chars = this.GetCharDics().Keys.ToList();
                char target = '0';
                if (chars.Count() == 0) return;
                if (chars.Count() == 1) target = chars[0];
                else
                {
                    var chardic = chars.ToDictionary(x => x, x => NumMonof.Concat(DenMonof).Max(y => y.Character.ContainsKey(x) ? y.Character[x] : 0));
                    target = chardic.First().Key;
                    foreach (var dic in chardic)
                    {
                        if (dic.Key != target)
                        {
                            if (dic.Value > chardic[target])
                            {
                                target = dic.Key;
                            }
                            else if (dic.Value == chardic[target])
                            {
                                if (dic.Key.CompareTo(target) < 0) target = dic.Key;
                            }
                        }
                    }
                }

                var IsNumBigger = NumMonof.Max(x => x.Character.ContainsKey(target) ? x.Character[target] : 0)
                                > DenMonof.Max(x => x.Character.ContainsKey(target) ? x.Character[target] : 0);


                var Bigger = new Dictionary<int, List<Monomial>> { };
                var Smaller = new Dictionary<int, List<Monomial>> { };
                var BiggerF = new List<Monomial> { };
                var SmallerF = new List<Monomial> { };


                if (IsNumBigger)
                {
                    BiggerF = NumMonof;
                    SmallerF = DenMonof;

                    Bigger = NumMonof.GroupBy(x => x.Character.ContainsKey(target) ? x.Character[target] : 0).ToDictionary(x => x.Key, x => x.Select(y => y).ToList());
                    Smaller = DenMonof.GroupBy(x => x.Character.ContainsKey(target) ? x.Character[target] : 0).ToDictionary(x => x.Key, x => x.Select(y => y).ToList());
                }
                else
                {
                    BiggerF = DenMonof;
                    SmallerF = NumMonof;

                    Bigger = DenMonof.GroupBy(x => x.Character.ContainsKey(target) ? x.Character[target] : 0).ToDictionary(x => x.Key, x => x.Select(y => y).ToList());
                    Smaller = NumMonof.GroupBy(x => x.Character.ContainsKey(target) ? x.Character[target] : 0).ToDictionary(x => x.Key, x => x.Select(y => y).ToList());
                }

                if (Smaller.Any(x => x.Value.Count() != 1)) return;

                var SUMfactor = new List<Monomial> { };


                var BiggerMax = Bigger.Keys.Max();
                var SmallerMax = Smaller.Keys.Max();

                while (true)
                {

                    var factor = Bigger[BiggerMax].Select(x => x / Smaller[SmallerMax][0]).ToList();
                    SUMfactor = SUMfactor.Concat(factor).ToList();

                    BiggerF = (new Formula(BiggerF) - new Formula(SmallerF) * new Formula(factor)).NumMonof;
                    Bigger = BiggerF.GroupBy(x => x.Character.ContainsKey(target) ? x.Character[target] : 0).ToDictionary(x => x.Key, x => x.Select(y => y).ToList());
                    BiggerMax = Bigger.Keys.Max();

                    if (BiggerF.Count() == 0)
                    {
                        var res = new Formula(SUMfactor);

                        NumMonof = res.NumMonof;
                        DenMonof = res.DenMonof;

                        return;
                    }
                    else if (BiggerMax < SmallerMax)
                    {
                        return;
                    }
                }
            }
        }


        public List<Monomial> NumMonof//分子
        {
            get { return _monof; }
            set { _monof = value; }
        }
        public List<Monomial> DenMonof//分母
        {
            get { return _denomimonof; }
            set { _denomimonof = value; }
        }

        public static Formula operator +(Formula a, Formula b)
        {
            if (a.IsZero()) return b;
            if (b.IsZero()) return a;

            if(a.DenMonof.IsOne() && b.DenMonof.IsOne())
            {
                return new Formula(a.NumMonof.Concat(b.NumMonof).ToList());
            }

            return new Formula(
                MultiMonomialList(a.NumMonof, b.DenMonof).Concat(MultiMonomialList(b.NumMonof, a.DenMonof)).ToList(),
                MultiMonomialList(a.DenMonof, b.DenMonof)
                );
        }

        /// <summary>
        /// Monomialのリスト同士の乗算
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private static List<Monomial> MultiMonomialList(List<Monomial> a, List<Monomial> b)
        {
            if(a.IsZero() || b.IsZero())
            {
                return new List<Monomial> { };
            }

            if (a.IsOne()) return b;
            if (b.IsOne()) return a;

            return a.Select(x => b.Select(y => x * y)).Aggregate((now, next) => now.Concat(next)).ToList();
        }

        public static Formula operator -(Formula a, Formula b)
        {
            if (a.IsZero()) return -b;
            if (b.IsZero()) return a;

            return new Formula(
                MultiMonomialList(a.NumMonof, b.DenMonof).Concat(MultiMonomialList(b.NumMonof, a.DenMonof).Select(x => -x)).ToList(),
                MultiMonomialList(a.DenMonof, b.DenMonof)
                );
        }

        public static Formula operator *(Formula a, Formula b)
        {
            if (a.IsZero() || b.IsZero()) return (Formula)0;
            if (a.IsOne()) return b;
            if (b.IsOne()) return a;

            return new Formula(
                MultiMonomialList(a.NumMonof, b.NumMonof),
                MultiMonomialList(a.DenMonof, b.DenMonof)
                );
        }

        public static Formula operator /(Formula x, Formula y)
        {
            if (x.IsZero()) return (Formula)0;
            if (y.IsZero()) throw new DivideByZeroException();
            if (x.IsOne()) return y.Invert();
            if (y.IsOne()) return x;

            return new Formula(
                MultiMonomialList(x.NumMonof, y.DenMonof),
                MultiMonomialList(x.DenMonof, y.NumMonof)
                );
        }

        public static Formula operator ^(Formula x, int r)
        {
            if (r == 0) return (Formula)1;
            else if (r == 1) return x;

            if(x.DenMonof.IsOne() && x.NumMonof.Count() == 1)
            {
                return x.NumMonof.First() ^ r;
            }
            else if(x.NumMonof.IsOne() && x.DenMonof.Count() == 1)
            {
                return (x.DenMonof.First() ^ r).Invert();
            }

            Formula res = x;

            if (r == 0)
            {
                return (Formula)1;
            }
            else if(r > 0)
            {
                for (int i = 0; i < r - 1; i++)
                {
                    res *= x;
                }

                return res;
            }
            else
            {
                for (int i = 0; i < -r - 1; i++)
                {
                    res *= x;
                }

                return res.Invert();
            }
        }

        public static Formula operator -(Formula a)
        {
            return new Formula(a.NumMonof.Select(x => -x).ToList(), a.DenMonof);
        }

        public static bool operator ==(Formula a, Formula b)
        {
            return a.GetHashCode() == b.GetHashCode();
        }

        public static bool operator !=(Formula a, Formula b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            return this.GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode()
        {
            var hash = 1;

            if (NumMonof.Count() != 0) hash ^= NumMonof.Select(x => x.GetHashCode()).Aggregate((now, next) => now ^ next);
            if (DenMonof.Count() != 0) hash ^= DenMonof.Select(x => -x.GetHashCode()).Aggregate((now, next) => now ^ next);

            return hash;
                
        }

        /// <summary>
        /// 逆数
        /// </summary>
        /// <returns></returns>
        public Formula Invert()
        {
            return new Formula(DenMonof, NumMonof);
        }

        /// <summary>
        /// double型→Formula型へのキャスト
        /// </summary>
        /// <param name="i"></param>
        public static explicit operator Formula(double i)
        {
            return new Formula(i);
        }


        public static implicit operator Formula(Monomial m)
        {
            return new Formula(m);
        }

        public async Task<string> GetString(bool ReturnFraction = false, bool ForDisplay = false)
        {
            string NumStr = "";
            if(NumMonof.Count() == 0)
            {
                return "0";
            }
            else
            {
                NumStr = (await NumMonof.Select(async x =>
                {
                    var str = await x.GetString(ReturnFraction, ForDisplay);
                    if (str == "") return "";
                    else if (str.First() == '-') return str;
                    else return "+" + str;

                }).WhenAll()).Aggregate((now, next) => now + next);

                if (NumStr != "" && NumStr.First() == '+') NumStr = NumStr.Substring(1);
            }

            if (DenMonof.Count() == 1 && DenMonof[0].IsOne())
            {
                return NumStr;
            }
            else
            {
                var DenStr = (await DenMonof.Select(async x =>
                {
                    var str = await x.GetString(ReturnFraction, ForDisplay);
                    if (str == "") return "";
                    else if (str.First() == '-') return str;
                    else return "+" + str;

                }).WhenAll()).Aggregate((now, next) => now + next);

                if (DenStr != "" && DenStr.First() == '+') DenStr = DenStr.Substring(1);

                if (DenStr == "1" || DenStr == "")
                {
                    return NumStr;
                }

                if (NumStr.Contains("+") || NumStr.Substring(1).Contains("-")) NumStr = "(" + NumStr + ")";
                if (DenStr.Contains("+") || DenStr.Substring(1).Contains("-")) DenStr = "(" + DenStr + ")";

                return NumStr + "/" + DenStr;
                
            }
        }

        /// <summary>
        /// Substituteのための辞書を得る
        /// </summary>
        /// <returns></returns>
        public Dictionary<char, double> GetCharDics()
        {
            return NumMonof.Concat(DenMonof).Select(x => x.GetCharDics().Keys.ToList())
                .Aggregate((now, next) => now.Union(next).ToList()).Distinct().ToDictionary(x => x, x => double.NaN);
        }

        /// <summary>
        /// 文字に代入して数値を得る
        /// </summary>
        /// <param name="dics"></param>
        /// <returns></returns>
        public async Task<Formula> Substitute(Dictionary<char, double> dics)
        {
            return new Formula ((await NumMonof.Select(async x => await x.Substitute(dics)).WhenAll()).ToList(),
                                (await DenMonof.Select(async x => await x.Substitute(dics)).WhenAll()).ToList());
        }

        public async Task<Formula> SubstituteX(char target, double value)
        {
            return new Formula((await NumMonof.Select(async x => await x.SubstituteX(target, value)).WhenAll()).ToList(),
                               (await DenMonof.Select(async x => await x.SubstituteX(target, value)).WhenAll()).ToList());
        }

        public Formula GetTargetCharInfo(char Target)
        {
            return new Formula(
                NumMonof.Select(x => x.GetTargetCharInfo(Target)).ToList(),
                DenMonof.Select(x => x.GetTargetCharInfo(Target)).ToList()
                );
        }

        /// <summary>
        /// 分子部分をFormulaで取得
        /// </summary>
        /// <returns></returns>
        public Formula GetNumerator()
        {
            return new Formula(NumMonof);
        }

        /// <summary>
        /// 分母部分をFormulaで取得
        /// </summary>
        /// <returns></returns>
        public Formula GetDenominator()
        {
            return new Formula(DenMonof);
        }

        public Formula RemoveX(char target)
        {
            return new Formula(NumMonof.Select(x => x.RemoveX(target)).ToList(), DenMonof.Select(x => x.RemoveX(target)).ToList());
        }

        /// <summary>
        /// Formulaの累乗根を計算する
        /// </summary>
        /// <param name="f">数式</param>
        /// <param name="power">N乗根</param>
        /// <returns></returns>
        public static Formula CalcRoot(Formula f,int power)
        {
            if(power >= 2)
            {
                return new Monomial(
                1.0, "",
                nthroots: new Dictionary<int, Formula> { { power, f } });
            }
            else if(power == 1)
            {
                return f;
            }
            else if(power == 0)
            {
                throw new DivideByZeroException("0乗根は存在しません。");
            }
            else if(power == -1)
            {
                return f.Invert();
            }
            else
            {
                return new Monomial(
                1.0, "",
                nthroots: new Dictionary<int, Formula> { { power, f.Invert() } });
            }
        }

        public bool IsOne()
        {
            return NumMonof.Count() == 1 && NumMonof[0].IsOne() && DenMonof.Count() == 1 && DenMonof[0].IsOne();
        }

        public bool IsZero()
        {
            if (NumMonof.Count() == 0 || DenMonof.Count() == 0) return true;

            return NumMonof.All(x => x.IsZero()) && DenMonof.All(x => x.IsZero());
        }

        public bool IsDouble()
        {
            return NumMonof.All(x => x.IsDouble()) && DenMonof.All(x => x.IsDouble());
        }

        public async Task<double> ToDouble()
        {
            return (await NumMonof.Select(async x => await x.ToDouble()).WhenAll()).Aggregate((now, next) => now + next)
                 / (await DenMonof.Select(async x => await x.ToDouble()).WhenAll()).Aggregate((now, next) => now + next);
        }
    }

    public static class FormulaExtensions
    {
        public static bool IsOne(this List<Monomial> m)
        {
            return m.Count() == 1 && m[0].IsOne();
        }

        public static bool IsZero(this List<Monomial> m)
        {
            return m.Count() == 0 || m.All(x => x.IsZero());
        }
    }
}
