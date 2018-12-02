using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using ImTools;

using System.Text.RegularExpressions;
using Util;
using FactMemory.Services;
using System.Threading.Tasks;
using FactMemory.Funcs;
using FactMemory.Resx;

namespace SolveEquations
{
    /// <summary>
    /// 括弧のある計算式を括弧のない多項式に変換する
    /// </summary>
    public static class ExpandFormula
    {
        /// <summary>
        /// 式を展開する
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public async static Task<Formula> ExpandCharFormula(string f)
        {
            f = SolveNumFormulas.LaTeXtoFormula(f);

            var ret = await Expand(f);

            Trace.WriteLine("Expanded:" + ret.GetString());
            return ret;
        }

        /// <summary>
        /// 括弧のある式を展開
        /// </summary>
        /// <param name="f">括弧のある式</param>
        /// <returns>括弧のない多項式</returns>
        private async static Task<Formula> Expand(string f)
        {
            //-{文字列}のときは -1{文字列}に直す
            f = Regex.Replace(f, "-([a-z|A-Z]|\\()", "-1$1");

            //単項式に分割
            var fs = OperateBrackets.FindMonomials(f).ToList();

            //単項式ごとに処理
            List<Formula> mf = (await fs.Select(async (m) =>
            {
                if (m.First() == '*') m.Substring(1);

                if (Monomial.IsMonomial(m))
                {
                    return new Formula(await Monomial.CreateByStr(m));
                }
                else
                {
                    //括弧に隣接する加減算記号を数字として扱う
                    m = Regex.Replace(m, "(\\+|\\-)\\(", "${1}1(");

                    var bmap = OperateBrackets.MapingBrackets(m);

                    if(bmap.First() - bmap.Last() > 0)
                    {
                        throw new Exception(AppResources.BracketsWrong);
                    }

                    //括弧が足りない場合は補完
                    if (bmap.Last() - bmap.First() > 0)
                    {
                        m += new string(')', bmap.Last() - bmap.First());
                        bmap = bmap.ToList().Concat(
                            Enumerable.Range(0, bmap.Last() - bmap.First()).Reverse()
                            ).ToArray();
                    }

                    //中身のない括弧を消す
                    m = Regex.Replace(m, "\\(\\)", "");

                    if (m.Length == 0) return (Formula)0;

                    //括弧が一致したとき
                    if (bmap.First() == bmap.Last())
                    {
                        //前に演算子がついていないときは乗算記号をつける
                        if (!Regex.IsMatch(m.First().ToString(), "^(\\*|/|\\^|\\()$"))
                        {
                            m = m.Insert(0, "*");
                        }

                        //累乗を処理
                        if (m.Contains("^"))
                        {
                            m = await ExpandPowAsync(m);
                        }

                        //扱いやすい単位まで分割
                        var splitteds = OperateBrackets.SplitByOperators(m);

                        var Numerator = new List<Formula> { };
                        var Denominator = new List<Formula> { };

                        foreach(string splitted in splitteds)
                        {
                            if (splitted.Length == 0) continue;

                            var splt = splitted;

                            if (splt.First() == '*') splt = splt.Substring(1);

                            var IsDenom = splt.First() == '/';
                            if (IsDenom) splt = splt.Substring(1);

                            switch (splt.First())
                            {
                                case '(':
                                    if (IsDenom)
                                    {
                                        Denominator.Add(await Expand(splt.Substring(1, splt.Length - 2)));
                                    }
                                    else
                                    {
                                        Numerator.Add(await Expand(splt.Substring(1, splt.Length - 2)));
                                    }
                                    break;
                                default:
                                    if (IsDenom)
                                    {
                                        Denominator.Add(await Expand(splt));
                                    }
                                    else
                                    {
                                        Numerator.Add(await Expand(splt));
                                    }
                                    break;
                            }
                        }

                        if (Numerator.Count() == 0) Numerator.Add((Formula)1);
                        if (Denominator.Count() == 0) Denominator.Add((Formula)1);

                        return Numerator.Aggregate((now, next) => now * next) / Denominator.Aggregate((now, next) => now * next);
                    }
                    else
                    {
                        throw new Exception(AppResources.BracketsDontMatch);
                    }
                }
            }).WhenAll()).ToList();

            return mf.Aggregate((now, next) => now + next);
        }

        /// <summary>
        /// 文字式(未整理)の累乗を処理
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static async Task<string> ExpandPowAsync(string f)
        {
            if (f.Contains("^"))
            {
                f = Regex.Replace(f, "^\\^(\\+|-)?(\\d|\\.)*", "");//頭の累乗記号＋数字は消す
                f = Regex.Replace(f, "\\^$", "");//末尾の累乗記号も消す
                f = Regex.Replace(f, "\\^([a-z|A-Z])", "$1");//文字で累乗されている場合は累乗をなかったことにする

                f = Regex.Replace(f, "[a-z|A-Z]\\^(\\d)+", Match =>
                {
                    return new string(Match.Value.First(), int.Parse(Regex.Replace(Match.Value, "(.*)(\\d+)(.*)", "$2")));
                });

                while (f.Contains("^"))
                {
                    int PowIndex = f.IndexOf('^');

                    if (PowIndex == 0) throw new Exception(AppResources.PowerMustTop);

                    string Powed = "1";
                    if (f[PowIndex - 1] == ')')
                    {
                        //Xamarin.Forms.DependencyService.Get<IToastService>()
                        //.Show("括弧に'^'は付けられません");

                        Powed = "(" + OperateBrackets.TakeoutInsideofBracket(f, PowIndex - 2) + ")";

                        throw new Exception(AppResources.CantAddPowerAfterParenthes);
                    }
                    else if (Regex.IsMatch(f[PowIndex - 1].ToString(), "(\\+|-)?(\\d|\\.)"))
                    {
                        Powed = Regex.Match(f.Substring(0, PowIndex), "(\\+|-)?(\\d+|\\.)$").Value;
                    }
                    else
                    {
                        Powed = f[PowIndex - 1].ToString();
                    }

                    Double Powist = 1.0;
                    if (f[PowIndex + 1] == '(')
                    {
                        var content = OperateBrackets.TakeoutInsideofBracket(f, PowIndex + 1);

                        if (Regex.IsMatch(content, "[a-z]")) throw new Exception(AppResources.CantAddPowerAfterChar);

                        Powist = await SolveNumFormulas.SolveNumFormula(content);

                        f = f.Remove(PowIndex - 1, content.Length + 4);//^の分(1)+カッコの分(2)+
                    }
                    else
                    {
                        var content = Regex.Match(f.Substring(PowIndex), "^(\\+|-)?(\\d|\\.)+").Value;

                        Powist = double.Parse(content);

                        f = f.Remove(PowIndex - 1, content.Length + 2);
                    }

                    var repeated = "";

                    if (double.TryParse(Powed, out double Poweddouble))
                    {
                        repeated = Math.Pow(Poweddouble, Powist).ToString();
                    }
                    else
                    {
                        repeated = string.Concat(Enumerable.Repeat(Powed, (int)Powist));
                    }

                    f = f.Insert(PowIndex - 1, repeated);
                }
            }

            return f;
        }
    }









    public static class OperateBrackets
    {
        /// <summary>
        /// 括弧のある多項式を単項式に分割
        /// </summary>
        /// <param name="f">括弧のある式</param>
        /// <returns>単項式の配列</returns>
        public static string[] FindMonomials(string f)
        {
            int[] bracketdepth = MapingBrackets(f);
            string commaedf = f;
            string PastChar = "";
            int count = 0;
            for (int i = 0; i < f.Length; i++)
            {
                string NowChar = f.Substring(i, 1);

                if ((NowChar == "+" || NowChar == "-") 
                    && !(PastChar == "*" || PastChar == "/" || PastChar == "^") && bracketdepth[i] == 0)
                {
                    commaedf = commaedf.Insert(i + count++, ":");
                }

                PastChar = NowChar;
            }

            Trace.WriteLine(commaedf);
            return commaedf.Split(':').Remove("+").Remove("-").Remove("");
        }

        /// <summary>
        /// 加減算のない数式を、数字・関数・括弧に分ける
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static List<string> SplitByOperators(string f)
        {
            int[] bracketdepth = MapingBrackets(f);
            string commaedf = f;
            string PastChar = "";
            int count = 0;
            for (int i = 0; i < f.Length; i++)
            {
                string NowChar = f.Substring(i, 1);

                if ((NowChar == "*" || NowChar == "/" || NowChar == "^" || NowChar == "(" || NowChar == "#") && bracketdepth[i] == 0)
                {
                    commaedf = commaedf.Insert(i + count, ":");
                    count++;
                }
                else if(NowChar == ")" && bracketdepth[i+1] == 0)
                {
                    commaedf = commaedf.Insert(i + count + 1, ":");
                    count++;
                }

                PastChar = NowChar;
            }

            commaedf = Regex.Replace(commaedf, "(\\*|/|\\^):(\\(|#)", "$1$2");
            commaedf = Regex.Replace(commaedf, "::", ":");
            commaedf = Regex.Replace(commaedf, ":\\+", ":");
            commaedf = Regex.Replace(commaedf, "^\\+", "");

            Trace.WriteLine(commaedf);
            var commaedf_splitted = commaedf.Split(':').ToList();
            commaedf_splitted.RemoveAll(x => x == "*" || x == "/" || x == "^" || x == "");
            return commaedf_splitted;
        }

        /// <summary>
        /// 括弧のある単項式を括弧で分割(未使用)
        /// </summary>
        /// <param name="m">括弧のある単項式</param>
        /// <returns>カッコ内の式の配列</returns>
        public static string[] FindBrackets(string m)
        {
            string[] result = null;

            //掛ける記号を削除
            m = Regex.Replace(m, "\\*", "");

            if (!m.Contains("(") && !m.Contains(")"))
            {
                return new string[1] { m };
            }

            int[] bracketdepth = MapingBrackets(m);

            if (bracketdepth.First() == bracketdepth.Last())
            {
                int[] bracketlengths = null;

                //括弧の外側にあるものと括弧の内側にあるものの境界をマーク
                int prevalue = 0;
                int startpoint = 0;
                for (int i = 0; i < bracketdepth.Length; i++)
                {
                    if (bracketdepth[i] == 0)
                    {
                        if ((i + 1 < bracketdepth.Length && bracketdepth[i + 1] == 1) || prevalue == 1)
                        {
                            bracketlengths = bracketlengths.Append(i - startpoint);
                            startpoint = i;
                        }
                    }

                    prevalue = bracketdepth[i];
                }

                //長さ0のものを削除
                List<int> list = new List<int>(bracketlengths);
                list.Remove(0);
                bracketlengths = list.ToArray();

                //マークしたもので分割
                int index = 0;
                foreach (int l in bracketlengths)
                {

                    result = result.Append(m.Substring(index, l));

                    index += l;
                }

                //括弧で囲まれているものは括弧を外す
                string[] _result = null;
                foreach (string r in result)
                {
                    if (r.Contains("("))
                    {
                        _result = _result.Append(r.Substring(1, r.Length - 2));
                    }
                    else
                    {
                        _result = _result.Append(r);
                    }
                }
                Trace.WriteLine(string.Join(",", _result));
                return _result;
            }
            else
            {
                throw new Exception(AppResources.BracketsDontMatch);
            }
        }

        /// <summary>
        /// 括弧の入れ子の構造を整数の配列で表す
        /// </summary>
        /// <param name="f">括弧のある式</param>
        /// <returns>入れ子の構造</returns>
        public static int[] MapingBrackets(string f,string brackettype = "()")
        {
            int[] bracketdepth = new int[f.Length + 1];
            char[] charm = f.ToCharArray();

            bracketdepth[0] = 0;

            for (int i = 0; i < charm.Length; i++)
            {
                if ((charm[i] == brackettype.First() && ((i >= 1 && charm[i-1] != '#') || i == 0)) || (charm[i] == '#' && charm[i + 1] == '$'))
                {
                    bracketdepth[i + 1] = bracketdepth[i] + 1;
                }
                else if (charm[i] == brackettype.Last())
                {
                    bracketdepth[i + 1] = bracketdepth[i] - 1;
                }
                else
                {
                    bracketdepth[i + 1] = bracketdepth[i];
                }
            }

            Trace.WriteLine(string.Join(",", bracketdepth));
            return bracketdepth;
        }

        /// <summary>
        /// カッコ内の式の範囲を調べる
        /// </summary>
        /// <param name="f"></param>
        /// <param name="containIndex"></param>
        /// <param name="brackettype"></param>
        /// <returns></returns>
        public static (int,int) LocationofInsideofBracket(string f,int containIndex,string brackettype = "()")
        {
            var map = MapingBrackets(f, brackettype);

            var Height = Math.Max(map[containIndex], map[containIndex + 1]);

            int uplimit = map.Length - 1;
            int downlimit = 0;

            for (int i = containIndex + 1; i < map.Length; i++)
            {
                if (map[i] < Height)
                {
                    uplimit = i - 1;
                    break;
                }
            }
            for (int i = containIndex; i >= 0; i--)
            {
                if (map[i] < Height)
                {
                    downlimit = i;
                    break;
                }
            }

            return (downlimit, uplimit);
        }

        /// <summary>
        /// 指定した場所の高さの括弧の中身を取得する
        /// </summary>
        /// <param name="f"></param>
        /// <param name="containIndex"></param>
        /// <param name="brackettype"></param>
        /// <returns></returns>
        public static string TakeoutInsideofBracket(string f,int containIndex,string brackettype = "()")
        {
            (int, int) limits = LocationofInsideofBracket(f, containIndex, brackettype);

            return f.Substring(limits.Item1 + 1, limits.Item2 - limits.Item1 - 1);
        }
    }
}
