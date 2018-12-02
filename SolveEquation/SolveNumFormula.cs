using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using FactMemory.Funcs;
using Util;
using FactMemory.Services;
using System.Threading.Tasks;
using FactMemory.Resx;

namespace SolveEquations
{
    class SolveNumFormulas
    {
        public static FuncItemService ItemService { get; set; }

        /// <summary>
        /// 数字のみの数式を計算する
        /// </summary>
        /// <param name="f">計算式</param>
        /// <returns>計算結果</returns>
        public static async Task<double> SolveNumFormula(string f)
        {
            f = LaTeXtoFormula(f);

            //monos:単項式ごとに分けられたもの
            var monos = OperateBrackets.FindMonomials(f).ToList();
            monos.Remove("");

            var resmonos = new double[0];

            resmonos = await monos.Select(async (mono) =>
            {

                if (double.TryParse(mono, out double num))//単項式が数字だけなら先に返してしまう
                {
                    return num;
                }
                else
                {
                    //括弧に隣接する加減算記号を数字として扱う
                    mono = Regex.Replace(mono, "(\\+|\\-)\\(", "${1}1(");
                    mono = Regex.Replace(mono, "(\\+|\\-)\\#", "${1}1*#");
                    
                    var bmap = OperateBrackets.MapingBrackets(mono);

                    //括弧が足りない場合は補完
                    if (bmap.Last() - bmap.First() > 0)
                    {
                        mono += new string(')', bmap.Last() - bmap.First());
                        bmap = bmap.ToList().Concat(Enumerable.Range(0, bmap.Last() - bmap.First()).Reverse()).ToArray();
                    }

                    //括弧が正常な時の処理
                    if (bmap.First() == bmap.Last())
                    {
                        //演算子で区切る(累乗以外)、括弧はそのまま残す
                        var splitted = OperateBrackets.SplitByOperators(mono);

                        //累乗を計算する
                        while(splitted.Any(x => x.First() == '^'))
                        {
                            var n = splitted.LastOrDefault(x => x.First() == '^');
                            int index = splitted.LastIndexOf(n);
                            if(index > 0)
                            {
                                string pastn = splitted[index - 1];

                                double pastfigure = 1;
                                double nowfigure = 1;
                                char opera = '*';

                                if(pastn.First() == '*' 
                                || pastn.First() == '/'
                                || pastn.First() == '^')
                                {
                                    opera = pastn.First();

                                    if(!double.TryParse(pastn.Substring(1),out pastfigure))
                                    {
                                        pastfigure = await SolveNumFormula(pastn.Substring(2, pastn.Length - 3));
                                    }

                                    if (!double.TryParse(n.Substring(1), out nowfigure))
                                    {
                                        nowfigure = await SolveNumFormula(n.Substring(2, n.Length - 3));
                                    }
                                }
                                else
                                {
                                    if (!double.TryParse(pastn, out pastfigure))
                                    {
                                        pastfigure = await SolveNumFormula(pastn.Substring(1, pastn.Length - 2));
                                    }

                                    if (!double.TryParse(n.Substring(1), out nowfigure))
                                    {
                                        nowfigure = await SolveNumFormula(n.Substring(2, n.Length - 3));
                                    }
                                }

                                splitted.RemoveAt(index);
                                splitted.RemoveAt(index - 1);
                                splitted.Insert(index - 1, opera + Math.Pow(pastfigure, nowfigure).ToString());
                            }
                            else
                            {
                                throw new Exception("累乗でのエラー");
                            }
                        }

                        List<double> Numres = new List<double> { };

                        //各項を処理する
                        foreach (string r in splitted)
                        {
                            string s = r;

                            //最初に"*"がついていたら消す
                            if(s.First() == '*')
                            {
                                s = s.Substring(1);
                            }

                            bool IsInverse = false;
                            if(s.First() == '/')
                            {
                                IsInverse = true;

                                //最初の"/"を消す
                                s = s.Substring(1);
                            }

                            if (s.First() == '#')//関数がある時
                            {
                                string funkname = Regex.Replace(s, "^#\\$(([a-z|A-Z]|π)*)\\$#.*", "$1");
                                string inBracket = Regex.Replace(s, "^#\\$([a-z|A-Z]|π)*\\$#\\((.*)\\)$", "$2");

                                int[] inbracketmap = OperateBrackets.MapingBrackets(inBracket);

                                string CommaedinBracket = inBracket;
                                int lug = 0;
                                for (int i = 0; i < inBracket.Length; i++)
                                {
                                    if (inBracket[i] == ',' && inbracketmap[i] == 0)
                                    {
                                        CommaedinBracket = CommaedinBracket.Insert(i + lug++, ",");
                                    }
                                }

                                List<double> inBracketValues = (await CommaedinBracket.Split(new string[1] { ",," }, StringSplitOptions.None)
                                                                               .Select(async (x) => await SolveNumFormula(x))
                                                                               .WhenAll()).ToList();

                                if (IsInverse) Numres.Add(1 / await FuncItemService.RunFunc(inBracketValues, funkname));
                                else Numres.Add(await FuncItemService.RunFunc(inBracketValues, funkname));
                            }
                            else if (s.First() == '(')//括弧に囲まれたものの時
                            {
                                if(IsInverse) Numres.Add(1 / await SolveNumFormula(s.Substring(1, s.Length - 2)));
                                else Numres.Add(await SolveNumFormula(s.Substring(1, s.Length - 2)));
                            }
                            else//数字のみの時
                            {
                                if(IsInverse) Numres.Add(1 / double.Parse(s));
                                else Numres.Add(double.Parse(s));
                            }
                        }
                        return Numres.Aggregate((now, next) => now * next);
                    }
                    else
                    {
                        throw new Exception(AppResources.BracketsWrong);
                    }
                }
            }).WhenAll();
  

            return resmonos.Sum();
        }

        /// <summary>
        /// 数式の指定された部分に指定された文字を追加する
        /// </summary>
        /// <param name="nowformula"></param>
        /// <param name="figure"></param>
        /// <param name="locate"></param>
        /// <returns></returns>
        public async static Task<(string,int)> AddfigureManipulation(string nowformula, string figure, int locate)
        {
            string NowFormula = nowformula;

            //一つ前と二つ前の文字を抽出する
            //存在しなかった場合は""となる
            string PastChar = "";
            string PastPastChar = "";
            try
            {
                PastChar = NowFormula.Last().ToString();
                PastPastChar = NowFormula.Substring(NowFormula.Length - 2, 1);
            }
            catch { }
            
            if(figure == "/")//割り算が押された場合は分数を挿入する
            {
                if(NowFormula =="" || PastChar == "{" || PastChar == "(")
                {
                    NowFormula += "~frac{}{}";
                    locate++;
                }
                else
                {
                    int back = 0;
                    for (int i = NowFormula.Length - 1; i >= 0; i--)
                    {
                        if (Regex.IsMatch(NowFormula[i].ToString(), "^(\\+|\\-|\\*|/|\\(|\\{)$"))
                        {
                            back = i + 1;
                            break;
                        }
                        else if (NowFormula[i] == ')')
                        {
                            var locates = OperateBrackets.LocationofInsideofBracket(NowFormula, i);
                            i = locates.Item1;

                            if (i != 0 && NowFormula[i - 1] == '#')
                            {
                                for (int j = i - 2; j >= 0; j--)
                                {
                                    if (NowFormula[j] == '#')
                                    {
                                        i = j;
                                        break;
                                    }
                                }
                            }
                        }
                        else if (NowFormula[i] == '}')
                        {
                            var locates = OperateBrackets.LocationofInsideofBracket(NowFormula, i, "{}");
                            i = locates.Item1;
                        }
                    }

                    var Numor = NowFormula.Substring(back);
                    if (NowFormula != "" && NowFormula.Length > back)
                    {
                        NowFormula = NowFormula.Remove(back);
                    }


                    NowFormula = NowFormula + "~frac{" + Numor + "}{}";

                    locate += 2;
                }
            }
            else if(figure == "root")
            {
                NowFormula += "~root{2}{}";

                locate += 3;
            }
            else if(figure == "^")
            {
                if(!Regex.IsMatch(PastChar, "^(\\+|\\-|\\*|/|\\^|\\(|\\{)$"))
                {
                    NowFormula += "^{}";

                    locate += 1;
                }
            }
            else if (PastChar == "-" //前が-
                && Regex.IsMatch(PastPastChar, "^(\\*|/|\\^|\\()$")//前の前が*,/,^,(
                && Regex.IsMatch(figure, "^(\\+|\\-|\\*|/|\\^|\\))$"))//今のが+,-,*,/,^,)
            {
                switch (figure)
                {
                    case "+":
                        (NowFormula,locate) = BackSpace(NowFormula,locate);
                        break;

                    case "-":
                        (NowFormula,locate) = BackSpace(NowFormula,locate);
                        NowFormula += figure;
                        break;

                    case "*":
                    case "/":
                    case "^":
                        (NowFormula,locate) = BackSpace(NowFormula,locate);
                        (NowFormula,locate) = BackSpace(NowFormula,locate);
                        NowFormula += figure;
                        break;

                    case ")":
                        (NowFormula,locate) = BackSpace(NowFormula,locate);
                        (NowFormula,locate) = BackSpace(NowFormula,locate);
                        break;

                    default:
                        Trace.WriteLine("内部エラー");
                        break;
                }

                locate++;
            }
            else if (Regex.IsMatch(PastChar, "^(\\+|\\-)$")//前が+,-
                && Regex.IsMatch(figure, "^(\\+|\\-|\\*|/|\\^|\\))$"))//今のが+,-,*,/,^,)
            {
                (NowFormula,locate) = BackSpace(NowFormula,locate);
                NowFormula += figure;

                locate++;
            }
            else if(Regex.IsMatch(PastChar, "^(\\*|/|\\^|\\()$")//前が*,/,^,(
                && Regex.IsMatch(figure, "^(\\+|\\*|/|\\^|\\))$"))//今のが+,*,/,^,)
            {
                (NowFormula,locate) = BackSpace(NowFormula,locate);
                NowFormula += figure;

                locate++;
            }
            else if (figure == "invert" && !Regex.IsMatch(PastChar, "^(\\+|\\-|\\*|/|\\^|\\()$"))//逆数ボタンを押した時
            {
                NowFormula += "^{-1}";

                locate += 4;
            }
            else if(figure == "invert")
            {

            }
            else if(figure.First() == '~')
            {
                if(await FuncItemService.IsExist(figure.Substring(1)))
                {
                    var count = (await FuncItemService.GetByName(figure.Substring(1))).CharCount;

                    if(count == 0)
                    {
                        NowFormula += figure + "{}";
                        locate += 1;
                    }
                    else
                    {
                        NowFormula += figure + (new StringBuilder()).Insert(0, "{}", count).ToString();
                    }
                }

                locate += 1;
            }
            else if (figure == "." && PastChar == ".")//小数点
            {

            }
            else
            {
                NowFormula += figure;

                locate++;
            }

            return (NowFormula, locate);
        }

        /// <summary>
        /// LaTeX的な数式に文字を追加したいときの関数
        /// </summary>
        /// <param name="NowFormula">LaTeX的な数式</param>
        /// <param name="figure">挿入したい文字列</param>
        /// <param name="locate">キャレットの位置</param>
        /// <returns>(挿入後の数式,挿入後のキャレットの位置)</returns>
        public async static Task<(string,int)> AddfigureManipulation_LaTeX(string NowFormula,string figure,int locate)
        {
            //キャレットの位置を計算
            int truelocate = CalcTrueLocate(NowFormula, locate);

            var res = await AddfigureManipulation(NowFormula.Substring(0, truelocate), figure, locate);

            return (res.Item1 + NowFormula.Substring(truelocate), res.Item2);
        }

        /// <summary>
        /// LaTex的な数式でBackSpaceしたいときの関数
        /// </summary>
        /// <param name="NowFormula">LaTeX的な関数</param>
        /// <param name="locate">キャレットの位置</param>
        /// <returns>(BackSpace後の数式,BackSpace後のキャレットの位置)</returns>
        public static (string,int) BackSpace_LaTeX(string NowFormula,int locate)
        {
            int truelocate = CalcTrueLocate(NowFormula, locate);

            string past = "";

            if(truelocate > 0)
            {
                past = NowFormula[truelocate - 1].ToString();
            }

            //関数を消そうと画策してきたときの処理
            if(past == "{")
            {
                var bracketlocations = new List<(int, int)> { };

                var currentlocate = OperateBrackets.LocationofInsideofBracket(NowFormula, truelocate,"{}");
                bracketlocations.Add(currentlocate);

                var inspectionlocate = truelocate - 2;
                var backcount = 0;
                while (true)
                {
                    if (NowFormula[inspectionlocate] == '}')
                    {
                        var nowlocate = OperateBrackets.LocationofInsideofBracket(NowFormula, inspectionlocate, "{}");
                        bracketlocations.Insert(0, nowlocate);

                        inspectionlocate = nowlocate.Item1 - 1;

                        backcount++;
                    }
                    else if(NowFormula[inspectionlocate]== '^')
                    {
                        bracketlocations.Insert(0, (inspectionlocate, inspectionlocate));

                        break;
                    }
                    else
                    {
                        bracketlocations.Insert(
                            0, 
                            OperateBrackets.LocationofInsideofBracket(
                                NowFormula, 
                                inspectionlocate, 
                                "~" + NowFormula[inspectionlocate]
                            )
                        );

                        break;
                    }
                }

                inspectionlocate = currentlocate.Item2 + 1;

                while (true)
                {
                    if (inspectionlocate >= NowFormula.Length) break;

                    if(NowFormula[inspectionlocate] == '{')
                    {
                        var nowlocate = OperateBrackets.LocationofInsideofBracket(NowFormula, inspectionlocate, "{}");
                        bracketlocations.Add(nowlocate);

                        inspectionlocate = nowlocate.Item2 + 1;
                    }
                    else
                    {
                        break;
                    }
                }
                
                var gap = bracketlocations[0].Item2 - bracketlocations[0].Item1 + 1;

                NowFormula = NowFormula.Remove(bracketlocations[0].Item1,gap);

                for (int i = 1; i < bracketlocations.Count(); i++)
                {
                    NowFormula = NowFormula.Remove(bracketlocations[i].Item1 - gap, 1);
                    gap++;
                    NowFormula = NowFormula.Remove(bracketlocations[i].Item2 - gap, 1);
                    gap++;
                }

                return (NowFormula, locate - backcount - 1);
            }
            else if(past == "}")
            {
                return (NowFormula, locate - 1);
            }
            else
            {
                var res = BackSpace(NowFormula.Substring(0, truelocate), locate);
                return (res.Item1 + NowFormula.Substring(truelocate), res.Item2);
            }

        }

        /// <summary>
        /// キャレットの位置から実際の文字列での位置を割り出す
        /// </summary>
        /// <param name="NowFormula">数式</param>
        /// <param name="locate">キャレットの位置</param>
        /// <returns>実際の文字列での位置</returns>
        private static int CalcTrueLocate(string NowFormula,int locate)
        {
            List<int> SeparateMetaChara = new List<int> { };

            string PartofF = NowFormula;

            //trueだったらメタ文字をくり抜く作業に入り、falseだったら数式をくり抜く。
            bool IsNextMetaChara = false;

            string separated = "";

            while (PartofF != "")
            {
                if (IsNextMetaChara)
                {
                    switch (PartofF.First())
                    {
                        case '~':
                            separated = Regex.Replace(PartofF, "^(~(\\w+){).*$", "$1");
                            break;

                        case '}':
                            separated = Regex.Replace(PartofF, "^(}{?).*$", "$1");
                            break;

                        case '^':
                            separated = Regex.Replace(PartofF, "^(\\^{).*$", "$1");
                            break;
                    }

                    SeparateMetaChara.Add(separated.Length);
                    PartofF = PartofF.Substring(separated.Length);

                    IsNextMetaChara = false;
                }
                else
                {
                    separated = Regex.Replace(PartofF, "^([\\w|\\.|\\+|\\-|\\*|/|\\(|\\)|=]*).*$", "$1");

                    SeparateMetaChara.Add(separated.Length);
                    PartofF = PartofF.Substring(separated.Length);

                    IsNextMetaChara = true;
                }

                if (PartofF == "") SeparateMetaChara.Add(0);
            }

            int sum = -1;
            int lasti = 0;
            for (int i = 0; i < SeparateMetaChara.Count(); i += 2)
            {
                sum += SeparateMetaChara[i] + 1;

                if (locate <= sum)
                {
                    lasti = i;
                    break;
                }
            }

            int sumMetaChara = 0;
            for (int i = 1; i < lasti; i += 2)
            {
                sumMetaChara += SeparateMetaChara[i] - 1;
            }

            return locate + sumMetaChara;
        }
        
        /// <summary>
        /// 数式でBackSpaceしたいときの関数
        /// </summary>
        /// <param name="s">数式</param>
        /// <returns>BackSpace後の数式</returns>
        public static (string,int) BackSpace(string s,int locate)
        {
            if (s != "")
            {
                s = s.Remove(s.Length - 1, 1);

                if (s != "" && s.Last() == '#')
                {
                    s = Regex.Replace(s, "#\\$(\\w)+\\$#$", "");
                }

                return (s,locate - 1);
            }
            else
            {
                return ("", 0);
            }
        }

        /// <summary>
        /// LaTeX的な数式を普通の数式に変換
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static string LaTeXtoFormula(string s)
        {
            if (s.Contains("{"))
            {
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '~')
                    {
                        var LaTeXFuncName = Regex.Replace(s.Substring(i), "^~(\\w+)\\{.*$", "$1");

                        switch (LaTeXFuncName)
                        {
                            case "frac":
                                var Numor = OperateBrackets.TakeoutInsideofBracket(s.Substring(i + LaTeXFuncName.Length + 1), 0, "{}");
                                var Denor = OperateBrackets.TakeoutInsideofBracket(s.Substring(i + LaTeXFuncName.Length + 1 + Numor.Length + 2), 0, "{}");

                                s = s.Remove(i, 9 + Numor.Length + Denor.Length);
                                s = s.Insert(i, "(" + LaTeXtoFormula(Numor) + ")/(" + LaTeXtoFormula(Denor) + ")");
                                break;

                            case "root":
                                var rootindx = OperateBrackets.TakeoutInsideofBracket(s.Substring(i + LaTeXFuncName.Length + 1), 0, "{}");
                                var radicant = OperateBrackets.TakeoutInsideofBracket(s.Substring(i + LaTeXFuncName.Length + 1 + rootindx.Length + 2), 0, "{}");

                                s = s.Remove(i, 9 + rootindx.Length + radicant.Length);
                                s = s.Insert(i, "#$root$#(" + LaTeXtoFormula(rootindx) + "," + LaTeXtoFormula(radicant) + ")");
                                break;

                            default:
                                var parameters = new List<string>();
                                var searchingindex = 0;

                                while (true)
                                {
                                    var inside = OperateBrackets.TakeoutInsideofBracket(s.Substring(LaTeXFuncName.Length + 1 + i), searchingindex, "{}");
                                    parameters.Add(inside);
                                    searchingindex += inside.Length + 2;

                                    if (s.Length <= searchingindex + LaTeXFuncName.Length + 1 ||
                                        (s.Length > searchingindex + LaTeXFuncName.Length + 1 && s[searchingindex + LaTeXFuncName.Length + 1] != '{')) break;
                                }

                                s = s.Remove(i, LaTeXFuncName.Length + 1 + searchingindex);
                                s = s.Insert(i, "#$" + LaTeXFuncName + "$#(" + string.Join(",", parameters) + ")");
                                break;
                        }
                    }
                    else if (s[i] == '^' && i + 1 < s.Length && s[i + 1] == '{')
                    {
                        var content = OperateBrackets.TakeoutInsideofBracket(s.Substring(i + 1), 0, "{}");

                        s = s.Remove(i, 3 + content.Length);
                        s = s.Insert(i, "^(" + LaTeXtoFormula(content) + ")");
                    }
                }
            }
            return s;
        }

        /// <summary>
        /// 素因数分解をする
        /// from: https://qiita.com/y_miyoshi/items/da814d96e8890224aad3
        /// </summary>
        /// <param name="n"></param>
        /// <returns></returns>
        public static IEnumerable<ulong> PrimeFactors(ulong n)
        {
            ulong i = 2;
            ulong tmp = n;

            while (i * i <= n) //※1
            {
                if (tmp % i == 0)
                {
                    tmp /= i;
                    yield return i;
                }
                else
                {
                    i++;
                }
            }
            if (tmp != 1) yield return tmp;//最後の素数も返す
        }
    }
}
