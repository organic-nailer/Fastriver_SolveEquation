using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using FactMemory.Resx;
using FactMemory.Funcs;

namespace SolveEquations
{
    public static class SolveAbout
    {
        /// <summary>
        /// ある文字で式を整理する(x = の形にする)
        /// </summary>
        /// <param name="f">式</param>
        /// <param name="target">文字</param>
        /// <returns></returns>
        public async static Task<List<string>> SolveAboutX(string f,char target)
        {
            if(f.Count(x => x == '=') != 1)
            {
                throw new Exception(AppResources.ManyOrFewEqual);
            }

            if (!f.Contains(target))
            {
                throw new Exception(AppResources.TargetNotExist);
            }

            var BothSide = f.Split('=');

            return (await (await SolveAboutX(
                await Formula.CreateByStr(BothSide[0]), 
                await Formula.CreateByStr(BothSide[1]), 
                target)).Select(async x => await x.GetString()).WhenAll()).ToList();
        }

        /// <summary>
        /// ある文字で式を整理する(x =の形にする)
        /// </summary>
        /// <param name="LeftSide">左辺</param>
        /// <param name="RightSide">右辺</param>
        /// <param name="target">文字</param>
        /// <returns></returns>
        public async static Task<List<Formula>> SolveAboutX(Formula LeftSide, Formula RightSide,char target)
        {
            //分母部分を無くす
            var L = LeftSide.GetNumerator() * RightSide.GetDenominator();
            var R = RightSide.GetNumerator() * LeftSide.GetDenominator();

            //単項式の分母部分をなくす
            var LcommonFactor = Factorizations.FactorOut(L);
            var RcommonFactor = Factorizations.FactorOut(R);

            L = LcommonFactor.Item2 * LcommonFactor.Item1.GetNumerator() * RcommonFactor.Item1.GetDenominator();
            R = RcommonFactor.Item2 * RcommonFactor.Item1.GetNumerator() * LcommonFactor.Item1.GetDenominator();

            //xを含む単項式と含まないものに分け、左辺に含むものを集める
            var Lx = L.NumMonof.Where(x => x.GetCharDics().ContainsKey(target));
            var Lexceptx = L.NumMonof.Where(x => !x.GetCharDics().ContainsKey(target));

            var Rx = R.NumMonof.Where(x => x.GetCharDics().ContainsKey(target));
            var Rexceptx = R.NumMonof.Where(x => !x.GetCharDics().ContainsKey(target));

            L = new Formula(Lx.Concat(Rx.Select(x => -x)).ToList());
            R = new Formula(Lexceptx.Select(x => -x).Concat(Rexceptx).ToList());

            //Xがどのようなものなのかを調べる
            var XInfo = L.GetTargetCharInfo(target);
            var XTypes = XInfo.NumMonof.Select(x =>
            {
                if (x.NthRoots.Count() > 0)
                {
                    return (x, -Factorizations.LCM(x.NthRoots.Keys.ToList()));
                }
                else
                {
                    return (x, x.Character[target]);
                }
            }).GroupBy(x => x.Item2).ToDictionary(x => x.Key, x => x.Select(y => y.Item1).ToList());

            bool IsRightZero = R.IsZero();

            if(XInfo.NumMonof.Any(x => x.NumFuncs.Count() != 0 || x.DenFuncs.Count() != 0))//関数部に文字列がある場合
            {
                throw new Exception(AppResources.VariablesInFunction);
            }
            else if(XTypes.Count() > 4 || (IsRightZero && XTypes.Count() >= 3))//三次方程式以上の場合は解けない
            {
                throw new Exception(AppResources.TooComplicatedX);
            }
            else if(XTypes.Count() == 1)//xの種類が1の場合
            {
                if (IsRightZero && XTypes.All(x => x.Key > 0)) return new List<Formula> { (Formula)0 };

                if(!XTypes.Any(x => x.Key < 0))
                {
                    R = R / L * new Monomial(1.0, new Dictionary<char, int> { { target, XTypes.Keys.First() } });
                    return new List<Formula> { Formula.CalcRoot(R, XTypes.First().Key) };
                }
                else
                {
                    if(XTypes.First().Value.Count() >= 3)//3つ以上
                    {
                        throw new Exception(AppResources.TooComplicatedX);
                    }
                    else if(XTypes.First().Value.Count() == 1)//1つ
                    {
                        if(XTypes.First().Key > 0)
                        {
                            L = Formula.CalcRoot(L, Math.Abs(XTypes.First().Key));
                            R = Formula.CalcRoot(R, Math.Abs(XTypes.First().Key));
                        }
                        else
                        {
                            L = L ^ Math.Abs(XTypes.First().Key);
                            R = R ^ Math.Abs(XTypes.First().Key);
                        }

                        return await SolveAboutX(L, R, target);
                    }
                    else if(XTypes.Keys.First() <= -3)//3√以上かつ2つ
                    {
                        throw new Exception(AppResources.TooComplicatedX);
                    }
                    else//2√かつ2つ
                    {
                        L = L ^ 2;
                        R = R ^ 2;

                        return await SolveAboutX(L, R, target);
                    }
                }
            }
            else if(!XTypes.Any(x => x.Key < 0))//ルートがない場合
            {
                var LXMin = XTypes.Keys.Min();
                if (IsRightZero)
                {
                    if (XTypes.Count() == 3)
                    {
                        var factoredx = XTypes.Keys.Select(x => x - LXMin).ToList();
                        var max = factoredx.Max();
                        factoredx.Remove(0);
                        var min = factoredx.Min();

                        if (max == min * 2)
                        {
                            var ordered = L.NumMonof.GroupBy(x => x.Character[target])
                                   .ToDictionary(
                                x => x.Key,
                                x => new Formula(x.Select(y =>
                                      {
                                          var z = y;
                                          z.Character.Remove(target);
                                          return z;
                                      }).ToList()));

                            return (await SolveEquations.SolveEquation(ordered[max + LXMin], ordered[min + LXMin], ordered[LXMin]))
                                .Concat(new List<Formula> { (Formula)0 }).ToList();
                        }
                        else
                        {
                            throw new Exception(AppResources.TooComplicatedX);
                        }
                    }
                    else
                    {
                        var factoredx = XTypes.Keys.Select(x => x - LXMin).ToList();
                        var max = factoredx.Max();

                        var ordered = L.NumMonof.GroupBy(x => x.Character[target])
                                             .ToDictionary(
                                        x => x.Key,
                                        x => new Formula(x.Select(y =>
                                                          {
                                                              var z = y;
                                                              z.Character.Remove(target);
                                                              return z;
                                                          })
                                                          .ToList()));

                        return new List<Formula>
                        {
                            (Formula)0,
                            Formula.CalcRoot((Formula)(-1) * ordered[LXMin] / ordered[max + LXMin],max)
                        };
                    }
                }
                else
                {
                    if (XTypes.Keys.Max() == LXMin * 2)
                    {
                        var ordered = L.NumMonof.GroupBy(x => x.Character[target])
                               .ToDictionary(
                            x => x.Key,
                            x => new Formula(x.Select(y =>
                            {
                                var z = y;
                                z.Character.Remove(target);
                                return z;
                            }).ToList()));

                        return (await SolveEquations.SolveEquation(ordered[XTypes.Keys.Max()], ordered[LXMin], -R)).ToList();
                    }
                    else
                    {
                        throw new Exception(AppResources.TooComplicatedX);
                    }
                }
            }
            else if(XTypes.Count(x => x.Key < 0) == 1 && XTypes.Keys.Min() == -2)
            {
                var RootX = new Formula(XTypes[-2]);
                R = R - L + RootX;

                return await SolveAboutX(RootX ^ 2, R ^ 2, target);
            }
            else if(XTypes.Count(x => x.Key < 0) == 1 && XTypes.Where(x => x.Key < 0).First().Value.Count() == 1)
            {
                var RootX = new Formula(XTypes[XTypes.Keys.Min()]);

                R = R - L + RootX;

                return await SolveAboutX(RootX ^ XTypes.Keys.Min(), R ^ XTypes.Keys.Min(), target);
            }
            else if(XTypes.Count(x => x.Key < 0) == 2 && !XTypes.Any(x => x.Key > 0) && XTypes.Keys.Max() * 2 == XTypes.Keys.Min())
            {
                var max = XTypes.Keys.Max();
                var min = XTypes.Keys.Min();

                var MaxXs = XTypes[max].Select(x => x.GetTargetCharInfo(target) ^ max);
                var MinXs = XTypes[min].Select(x => x.GetTargetCharInfo(target) ^ min);

                if (MaxXs.Concat(MinXs).Distinct().Count() == 1)
                {
                    var formX = MaxXs.First();

                    var MaxExceptX = XTypes[max].Select(x => x.RemoveX(target)).ToList();
                    var MinExceptX = XTypes[min].Select(x => x.RemoveX(target)).ToList();

                    return await SolveAboutX((Formula)formX, (await SolveEquations.SolveEquation(new Formula(MaxExceptX), new Formula(MinExceptX), R))[0], target);
                }
                else
                {
                    throw new Exception(AppResources.TooComplicatedX);
                }
            }
            else
            {
                throw new Exception(AppResources.TooComplicatedX);
            }
        }
    }
}
