using FactMemory.Funcs;
using ImTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Util;

namespace SolveEquations
{
    /// <summary>
    /// 単項式を表すクラス
    /// </summary>
    public class Monomial
    {
        private (
            Fraction num,
            Dictionary<char, int> chara,
            Dictionary<char, int> recipchara, 
            List<MonoFunc> numfuncs,
            List<MonoFunc> denfuncs,
            Dictionary<int,Formula> nthroots
            ) m;

        /// <summary>
        /// 単項式(分数で初期化・文字をDictionaryで)
        /// </summary>
        /// <param name="num"></param>
        /// <param name="chara"></param>
        /// <param name="recipchara"></param>
        /// <param name="numfuncs"></param>
        /// <param name="denfuncs"></param>
        /// <param name="nthroots"></param>
        public Monomial(
            Fraction num,
            Dictionary<char, int> chara = null,
            Dictionary<char, int> recipchara = null,
            List<MonoFunc> numfuncs = null,
            List<MonoFunc> denfuncs = null,
            Dictionary<int, Formula> nthroots = null)
        {
            if (chara == null) chara = new Dictionary<char, int> { };
            if (recipchara == null) recipchara = new Dictionary<char, int> { };
            if (numfuncs == null) numfuncs = new List<MonoFunc> { };
            if (denfuncs == null) denfuncs = new List<MonoFunc> { };
            if (nthroots == null) nthroots = new Dictionary<int, Formula> { };

            Trace.WriteLine("Created with A");

            Initialise(num, chara, recipchara, numfuncs, denfuncs, nthroots);
        }
        /// <summary>
        /// 単項式(分数で初期化・文字をstringで)
        /// </summary>
        /// <param name="num"></param>
        /// <param name="chara"></param>
        /// <param name="recipchara"></param>
        /// <param name="numfuncs"></param>
        /// <param name="denfuncs"></param>
        /// <param name="nthroots"></param>
        public Monomial(
            Fraction num,
            string chara = "",
            string recipchara = "",
            List<MonoFunc> numfuncs = null,
            List<MonoFunc> denfuncs = null,
            Dictionary<int, Formula> nthroots = null)
        {
            if (numfuncs == null) numfuncs = new List<MonoFunc> { };
            if (denfuncs == null) denfuncs = new List<MonoFunc> { };
            if (nthroots == null) nthroots = new Dictionary<int, Formula> { };

            var charalist = chara.ToList();
            var reciplist = recipchara.ToList();
            
            var charadic = charalist.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
            var recipdic = reciplist.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());

            Trace.WriteLine("Created with B");

            Initialise(num, charadic, recipdic, numfuncs, denfuncs, nthroots);
        }

        public async static Task<Monomial> CreateByStr(string f)
        {
            Trace.WriteLine("Created with C");

            f = await ExpandFormula.ExpandPowAsync(f);//累乗を消す

            //分割
            var monos = OperateBrackets.SplitByOperators(f);

            var NumFuncs = new List<MonoFunc> { };
            var DenFuncs = new List<MonoFunc> { };
            var NthRoots = new Dictionary<int, Formula> { };

            var res = (await monos.Select(async x =>
            {
                var mono = x;

                //先頭に*がある場合は消す
                if (mono.First() == '*') mono = mono.Substring(1);

                //先頭に/がある場合は消し、IsDenominatorをTrueにする
                var IsDenominator = mono.First() == '/';

                if (IsDenominator) mono = mono.Substring(1);



                if (Regex.IsMatch(mono, "^#\\$root\\$#"))//ルートの処理
                {
                    var inside = Regex.Replace(mono, "^#\\$([a-z|A-Z]|π|_)*\\$#\\((.*)\\)$", "$2").Split(',');
                    var Rootindex = int.Parse(inside[0]);

                    if (!NthRoots.ContainsKey(Rootindex)) NthRoots.Add(Rootindex, (Formula)1);

                    if (IsDenominator) NthRoots[int.Parse(inside[0])] *= (await Formula.CreateByStr(inside[1])).Invert();
                    else NthRoots[int.Parse(inside[0])] *= await Formula.CreateByStr(inside[1]);
                }
                else if (mono.First() == '#')//ルート以外の関数の処理
                {
                    var FuncName = Regex.Replace(mono, "^#\\$(([a-z|A-Z]|π)*)\\$#.*", "$1");
                    string inBracket = Regex.Replace(mono, "^#\\$([a-z|A-Z]|π)*\\$#\\((.*)\\)$", "$2");

                    var inside = inBracket.Split(',');

                    if (IsDenominator)
                    {
                        DenFuncs.Add(new MonoFunc
                        {
                            FuncName = FuncName,
                            Formulas = (await inside.Select(async y => await Formula.CreateByStr(y)).WhenAll()).ToList()
                        });
                    }
                    else
                    {
                        NumFuncs.Add(new MonoFunc
                        {
                            FuncName = FuncName,
                            Formulas = (await inside.Select(async y => await Formula.CreateByStr(y)).WhenAll()).ToList()
                        });
                    }

                }
                else if (mono.First() == '(')//括弧に囲まれたやつだったときの処理
                {
                    if (IsDenominator) return (await Monomial.CreateByStr(mono.Substring(1, mono.Length - 2))).Invert();
                    else return await Monomial.CreateByStr(mono.Substring(1, mono.Length - 2));
                }
                else//その他
                {
                    var nums = new List<double> { 1.0 };
                    var chars = "";

                    while (mono.Length != 0)
                    {

                        var lump = Regex.Match(mono, "^(\\+|-)?(\\d|\\.)+").Value;



                        if (lump != "")
                        {
                            nums.Add(double.Parse(lump));
                        }
                        else
                        {
                            lump = Regex.Match(mono, "^[a-z|A-Z]+").Value;

                            chars += lump;
                        }

                        mono = mono.Substring(lump.Length);
                    }

                    if (IsDenominator) return new Monomial(nums.Aggregate((now, next) => now * next), chars).Invert();
                    else return new Monomial(nums.Aggregate((now, next) => now * next), chars);
                }

                return 1;

            }).WhenAll()).Aggregate((now, next) => now * next);


            return res * new Monomial(
                num: 1.0, 
                chara: "",
                numfuncs: NumFuncs, 
                denfuncs: DenFuncs,
                nthroots: NthRoots);

        }

        /// <summary>
        /// 初期設定
        /// ・分子と分母の文字を整列
        /// ・文字部分を約分
        /// </summary>
        /// <param name="num"></param>
        /// <param name="chara"></param>
        /// <param name="recipchara"></param>
        /// <param name="nthroots"></param>
        private void Initialise(
            Fraction num, Dictionary<char,int> chara, Dictionary<char, int> recipchara,
            List<MonoFunc> numfuncs, List<MonoFunc> denfuncs, Dictionary<int, Formula> nthroots)
        {

            Number = num;
            Character = chara;
            Recipchara = recipchara;
            NumFuncs = numfuncs;
            DenFuncs = denfuncs;
            NthRoots = nthroots;

            if(Character.Count() == 0
                && recipchara.Count() == 0
                && numfuncs.Count() == 0
                && denfuncs.Count() == 0
                && nthroots.Count() == 0)
            {
                Trace.WriteLine("Monomial Initialized:" + Number.ToString());
                return;
            }

            if(NthRoots.Any(x => x.Value.IsZero()))
            {
                Number = 0;
                Character.Clear();
                Recipchara.Clear();
                NumFuncs.Clear();
                DenFuncs.Clear();
                NthRoots.Clear();

                return;
            }

            Character = Character.Where(x => x.Value != 0).ToDictionary(x => x.Key, x => x.Value);
            Recipchara = Recipchara.Where(x => x.Value != 0).ToDictionary(x => x.Key, x => x.Value);
            NthRoots = NthRoots.Where(x => !x.Value.IsOne()).ToDictionary(x => x.Key, x => x.Value);

            Reduction();

            Trace.WriteLine("Monomial Initialized:" + this.GetString());
        }

        /// <summary>
        /// 約分
        /// </summary>
        private void Reduction()
        {
            //TODO:ここはもっと改善の余地があると思う
            List<Monomial> Separateds = new List<Monomial> { };

            NthRoots = NthRoots.ToDictionary(
                x => x.Key,
                x =>
                {
                    //共通因数を捻り出す
                    var Factor = Factorizations.FactorOut(x.Value);
                    //ルートの外に出せる分を計算し、Separatedsに格納
                    var Separated = Monomial.SeparatePower(Factor.Item1, x.Key);
                    Separateds.Add(Separated.Item1);
                    //ルートの外に出した後の残りを戻す
                    return Factor.Item2 * Separated.Item2;
                });
                                      
            if(Separateds.Count() != 0)//ルートの外に出たものの処理
            {
                var Multied = new Monomial(Number, Character, Recipchara) * Separateds.Aggregate((now, next) => now * next);

                Number = Multied.Number;
                Character = Multied.Character;
                Recipchara = Multied.Recipchara;
            }

            if(NumFuncs.Count() != 0 && DenFuncs.Count() != 0)//関数の約分
            {
                var commonfunc = NumFuncs.Intersect(DenFuncs);

                NumFuncs = NumFuncs.Except(commonfunc).ToList();
                DenFuncs = DenFuncs.Except(commonfunc).ToList();
            }
            
            var intersect = Character.Keys.ToList().Intersect(Recipchara.Keys.ToList());//文字の約分
            if (intersect.Count() != 0)
            {
                foreach(var key in intersect)
                {
                    var difference = Character[key] - Recipchara[key];

                    if(difference > 0)
                    {
                        Character[key] = difference;
                        Recipchara.Remove(key);
                    }
                    else if(difference < 0)
                    {
                        Character.Remove(key);
                        Recipchara[key] = 0 - difference;
                    }
                    else
                    {
                        Character.Remove(key);
                        Recipchara.Remove(key);
                    }
                }
            }

        }

        /// <summary>
        /// 数字
        /// </summary>
        public Fraction Number
        {
            get { return m.num; }
            set { m.num = value; }
        }
        /// <summary>
        /// 分子の文字
        /// </summary>
        public Dictionary<char, int> Character
        {
            get { return m.chara; }
            set { m.chara = value; }
        }
        /// <summary>
        /// 分母の文字
        /// </summary>
        public Dictionary<char, int> Recipchara
        {
            get { return m.recipchara; }
            set { m.recipchara = value; }
        }
        /// <summary>
        /// 分子の関数
        /// </summary>
        public List<MonoFunc> NumFuncs
        {
            get { return m.numfuncs; }
            set { m.numfuncs = value; }
        }
        /// <summary>
        /// 分母の関数
        /// </summary>
        public List<MonoFunc> DenFuncs
        {
            get { return m.denfuncs; }
            set { m.denfuncs = value; }
        }
        /// <summary>
        /// 累乗
        /// </summary>
        public Dictionary<int,Formula> NthRoots
        {
            get { return m.nthroots; }
            set { m.nthroots = value; }
        }

        /// <summary>
        /// 掛け算演算子オーバーロード
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Monomial operator *(Monomial x, Monomial y)
        {
            if (x.IsZero() || y.IsZero()) return 0;
            if (x.IsOne()) return y;
            if (y.IsOne()) return x;

            //文字部分
            var Character = x.Character.Concat(y.Character)
                       .GroupBy(a => a.Key)
                       .ToDictionary(a => a.Key, a => a.Select(b => b.Value).Sum());
            var Recipchara = x.Recipchara.Concat(y.Recipchara)
                       .GroupBy(a => a.Key)
                       .ToDictionary(a => a.Key, a => a.Select(b => b.Value).Sum());

            //関数部分
            var NumFuncs = x.NumFuncs.Concat(y.NumFuncs).ToList();
            var DenFuncs = x.DenFuncs.Concat(y.DenFuncs).ToList();

            return new Monomial(x.Number * y.Number, Character, Recipchara, NumFuncs, DenFuncs, MultiRoots(x.NthRoots,y.NthRoots));
        }

        /// <summary>
        /// ルートの部分の掛け算
        /// </summary>
        /// <param name="multiplier">かける数</param>
        /// <param name="multiplicand">がけられる数</param>
        /// <returns></returns>
        private static Dictionary<int,Formula> MultiRoots(Dictionary<int,Formula> multiplier, Dictionary<int, Formula> multiplicand)
        {
            if (multiplier.Count() == 0 && multiplicand.Count() == 0) return new Dictionary<int, Formula> { };

            var commonkeys = multiplier.Keys.Intersect(multiplicand.Keys);

            var uncommoner = multiplier.Where(x => !commonkeys.Contains(x.Key));
            var uncommoncand = multiplicand.Where(x => !commonkeys.Contains(x.Key));

            var result = uncommoner.Concat(uncommoncand);

            return result.Concat(multiplier.Where(x => commonkeys.Contains(x.Key)).Select(x =>
            {
                return new KeyValuePair<int, Formula>(x.Key, x.Value * multiplicand[x.Key]);
            })).ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// 割り算演算子オーバーロード
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Monomial operator /(Monomial x, Monomial y)
        {
            return x * y.Invert();
        }

        /// <summary>
        /// 累乗演算子オーバーロード
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Formula operator ^(Monomial a, int b)
        {
            if (b == 0) return (Formula)1;
            if (b == 1) return a;

            var _Number = a.Number ^ b;
            var _NumFuncs = a.NumFuncs;
            var _DenFuncs = a.DenFuncs;

            for (int i = 0; i < b; i++)
            {
                _NumFuncs = _NumFuncs.Concat(_NumFuncs).ToList();
                _DenFuncs = _DenFuncs.Concat(_DenFuncs).ToList();
            }
            
            var PoweredRootFormula = a.NthRoots.Select(x => (x.Value ^ ((b - b % x.Key) / x.Key), new { key = x.Key, value = x.Value ^ (b % x.Key) }));

            var Formulas = new Formula(1);

            if(PoweredRootFormula.Count() != 0) Formulas = PoweredRootFormula.Select(x => x.Item1).Aggregate((now, next) => now * next);

            return Formulas * new Monomial(
                num: _Number,
                chara: a.Character.ToDictionary(x => x.Key, x => x.Value * b),
                recipchara: a.Recipchara.ToDictionary(x => x.Key, x => x.Value * b),
                numfuncs: _NumFuncs,
                denfuncs: _DenFuncs,
                nthroots: PoweredRootFormula.ToDictionary(x => x.Item2.key, x => x.Item2.value)
                );
        }

        /// <summary>
        /// 足し算演算子オーバーロード
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Monomial operator +(Monomial x, Monomial y)
        {
            if (x.IsZero()) return y;
            if (y.IsZero()) return x;

            var numx = x.Number;
            var numy = y.Number;

            x.Number = 1;
            y.Number = 1;

            if (x == y)
            {
                x.Number = numx + numy;
                return x;
            }
            else
            {
                throw new FormatException("内部エラー：この単項式は足し算できません。");
            }
        }

        /// <summary>
        /// 引き算演算子オーバーロード
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static Monomial operator -(Monomial x, Monomial y)
        {
            if (x.IsZero()) return -y;
            if (y.IsZero()) return x;

            var numx = x.Number;
            var numy = y.Number;

            x.Number = 1;
            y.Number = 1;

            if (x == y)
            {
                x.Number = numx - numy;
                return x;
            }
            else
            {
                throw new FormatException("内部エラー：この単項式は引き算できません。");
            }
        }

        public static Monomial operator -(Monomial a)
        {
            a.Number = a.Number.SignReverse();

            return a;
        }

        public static bool operator ==(Monomial a, Monomial b)
        {
            return a.GetHashCode() == b.GetHashCode();
        }

        public static bool operator !=(Monomial a, Monomial b)
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
            var hash = Number.GetHashCode();

            if (Character.Count() != 0)  hash ^= Character.Select(x => x.Key.GetHashCode() + x.Value.GetHashCode()).Aggregate((now, next) => now ^ next);
            if (Recipchara.Count() != 0) hash ^= Recipchara.Select(x => x.Key.GetHashCode() + x.Value.GetHashCode()).Aggregate((now, next) => now ^ next);
            if(NumFuncs.Count() != 0)    hash ^= NumFuncs.Select(x => x.GetHashCode()).Aggregate((now, next) => now ^ next);
            if(DenFuncs.Count() != 0)    hash ^= DenFuncs.Select(x => x.GetHashCode()).Aggregate((now, next) => now ^ next);
            if(NthRoots.Count() != 0)    hash ^= NthRoots.Select(x => x.Key.GetHashCode() + x.Value.GetHashCode()).Aggregate((now, next) => now ^ next);

            return hash;
        }

        /// <summary>
        /// 逆数
        /// </summary>
        /// <returns></returns>
        public Monomial Invert()
        {
            return new Monomial(
                new Fraction(Number.Sign, Number.Denominator, Number.Numerator),
                Recipchara,
                Character,
                DenFuncs,
                NumFuncs,
                NthRoots.ToDictionary(x => x.Key, x => x.Value.Invert())
                );
        }

        /// <summary>
        /// doubleからMonomialへの明示的な型変換
        /// </summary>
        /// <param name="i"></param>
        public static implicit operator Monomial(double i)
        {
            return new Monomial(i,"","");
        }

        /// <summary>
        /// 文字列の表示形式(文字の累乗を^で表現)で取得
        /// </summary>
        /// <param name="ReturnFraction"></param>
        /// <returns></returns>
        public async Task<string> GetString(bool ReturnFraction = false, bool ForDisplay = false)
        {
            if (Number.IsZero()) return "0";

            string Numerator = "";
            string Denominator = "";


            string NumNumStr = "";
            string DenNumStr = "";

            var numfuncs = NumFuncs;
            var denfuncs = DenFuncs;
            var nthroots = NthRoots;

            double calcednum = 1;

            if (numfuncs.Count() != 0)
            {
                var calced = numfuncs.Where(x => x.Formulas.All(y => y.GetCharDics().Count() == 0)).ToList();

                if (calced.Count() != 0)
                {
                    calcednum *= (await calced.Select(async x =>
                        await FuncItemService.RunFunc((await x.Formulas.Select(async y => await y.ToDouble()).WhenAll()).ToList(), x.FuncName)
                        ).WhenAll())
                        .Aggregate((now, next) => now * next);
                }

                numfuncs = numfuncs.Except(calced).ToList();
            }
            if (denfuncs.Count() != 0)
            {
                var calced = denfuncs.Where(x => x.Formulas.All(y => y.GetCharDics().Count() == 0)).ToList();

                if (calced.Count() != 0)
                {
                    calcednum /= (await calced.Select(async x =>
                        await FuncItemService.RunFunc((await x.Formulas.Select(async y => await y.ToDouble()).WhenAll()).ToList(), x.FuncName)
                        ).WhenAll())
                        .Aggregate((now, next) => now * next);
                }

                denfuncs = denfuncs.Except(calced).ToList();
            }

            //ルートが計算できるようなら計算する
            if (nthroots.Count() != 0)
            {

                var calced = nthroots.Where(x => x.Value.GetCharDics().Count() == 0).ToList();

                if (calced.Count() != 0)
                {
                    calcednum *= (await calced.Select(async x => Math.Pow(await x.Value.ToDouble(), x.Key)).WhenAll())
                        .Aggregate((now, next) => now * next);
                }

                nthroots = nthroots.Except(calced).ToDictionary(x => x.Key, x => x.Value);
            }



            if (ReturnFraction)
            {
                Number = Number * calcednum;

                NumNumStr = Number.GetNumerator().ToString();
                DenNumStr = Number.GetDenominator().ToString();
            }
            else
            {
                NumNumStr = Math.Round(Number.ToDouble() * calcednum, 4).ToString();
                DenNumStr = "1";
            }


            var CharStr = Character.GetString(ForDisplay);
            var RecipStr = Recipchara.GetString(ForDisplay);


            var NumFuncsStr = "";
            var DenFuncsStr = "";
            if (numfuncs.Count != 0) NumFuncsStr = numfuncs.Select(x => x.GetString(ForDisplay, ReturnFraction)).Aggregate((now, next) => now + next);
            if (denfuncs.Count != 0) DenFuncsStr = denfuncs.Select(x => x.GetString(ForDisplay, ReturnFraction)).Aggregate((now, next) => now + next);


            var RootsStr = "";
            if (nthroots.Count() != 0)
            {
                if (ForDisplay)
                {
                    RootsStr = nthroots.Select(x => "root(" + x.Key + "," + x.Value.GetString(ReturnFraction, ForDisplay) + ")")
                                     .Aggregate((now, next) => now + next);
                }
                else
                {
                    RootsStr = nthroots.Select(x => "#$root$#(" + x.Key + "," + x.Value.GetString(ReturnFraction, ForDisplay) + ")")
                                     .Aggregate((now, next) => now + next);
                }
            }

            if (DenNumStr == "" && CharStr == "" && RecipStr == "" && NumFuncsStr == "" && DenFuncsStr == "" && RootsStr == "")
            {
                return NumNumStr;
            }

            if ((NumNumStr == "1" || NumNumStr == "-1") && (CharStr != "" || NumFuncsStr != "" || RootsStr != ""))
            {
                Numerator = NumNumStr.Replace("1", "") + CharStr + NumFuncsStr + RootsStr;
            }
            else
            {
                Numerator = NumNumStr + CharStr + NumFuncsStr + RootsStr;
            }

            if ((DenNumStr == "1" || DenNumStr == "-1") && (RecipStr != "" || DenFuncsStr != ""))
            {
                Denominator = DenNumStr.Replace("1", "") + RecipStr + DenFuncsStr;
            }
            else
            {
                Denominator = DenNumStr + RecipStr + DenFuncsStr;
            }

            if (Numerator == "" || Denominator == "") return "0";
            else if (Denominator == "1")
            {
                return Numerator;
            }
            else if (Denominator == "-1")
            {
                return "-" + Numerator;
            }
            else
            {
                return Numerator + "/" + Denominator;
            }
        }



        /// <summary>
        /// Substituteのための文字の種類の辞書を渡す
        /// </summary>
        /// <returns></returns>
        public Dictionary<char, double> GetCharDics()
        {
            var chars = Character.Keys.Union(Recipchara.Keys);

            if(NumFuncs.Count() != 0)
            {
                chars = chars.Union(NumFuncs.Select(x => x.Formulas.Select(y => y.GetCharDics().Keys.ToList())
                                               .Aggregate((now, next) => now.Concat(next).ToList()))
                        .Aggregate((now, next) => now.Concat(next).ToList()));
            }

            if(DenFuncs.Count() != 0)
            {
                chars = chars.Union(NumFuncs.Select(x => x.Formulas.Select(y => y.GetCharDics().Keys.ToList())
                                               .Aggregate((now, next) => now.Concat(next).ToList()))
                        .Aggregate((now, next) => now.Concat(next).ToList()));
            }

            if(NthRoots.Count() != 0)
            {
                chars = chars.Union(NthRoots.Select(x => x.Value.GetCharDics().Keys.ToList())
                        .Aggregate((now, next) => now.Concat(next).ToList()));
            }

            return chars.Distinct().ToDictionary(x => x, x => Double.NaN);
        }

        /// <summary>
        /// 代入
        /// </summary>
        /// <param name="dics"></param>
        /// <returns></returns>
        public async Task<Monomial> Substitute(Dictionary<char, double> dics)
        {
            var nowmonomial = this;

            foreach(var dic in dics)
            {
                if(!dic.Value.Equals(double.NaN))
                {
                    nowmonomial = await nowmonomial.SubstituteX(dic.Key, dic.Value);
                }
            }

            return nowmonomial;
        }

        public async Task<Monomial> SubstituteX(char target, double value)
        {
            double num = Number.ToDouble();

            if (Character.ContainsKey(target))
            {
                num *= Math.Pow(value, Character[target]);

                Character.Remove(target);
            }
            if (Recipchara.ContainsKey(target))
            {
                num /= Math.Pow(value, Character[target]);

                Recipchara.Remove(target);
            }

            var numfuncs = new List<MonoFunc> { };
            var denfuncs = new List<MonoFunc> { };
            if (NumFuncs.Count() > 0)
            {
                numfuncs = (await NumFuncs.Select(async x => new MonoFunc
                {
                    FuncName = x.FuncName,
                    Formulas = (await x.Formulas.Select(async y => await y.SubstituteX(target, value)).WhenAll()).ToList()
                }).WhenAll()).ToList();

                var calced = numfuncs.Where(x => x.Formulas.All(y => y.GetCharDics().Count() == 0)).ToList();

                if(calced.Count() != 0)
                {
                    num *= (await calced.Select(async x =>
                        await FuncItemService.RunFunc(
                            (await x.Formulas.Select(async y => await y.ToDouble()).WhenAll()).ToList(), x.FuncName)
                        ).WhenAll())
                        .Aggregate((now, next) => now * next);
                }

                numfuncs = numfuncs.Except(calced).ToList();
            }
            if (DenFuncs.Count() > 0)
            {
                denfuncs = (await NumFuncs.Select(async x => new MonoFunc
                {
                    FuncName = x.FuncName,
                    Formulas = (await x.Formulas.Select(async y => await y.SubstituteX(target, value)).WhenAll()).ToList()
                }).WhenAll()).ToList();

                var calced = denfuncs.Where(x => x.Formulas.All(y => y.GetCharDics().Count() == 0)).ToList();

                if (calced.Count() != 0)
                {
                    num /= (await calced.Select(async x =>
                        await FuncItemService.RunFunc(
                            (await x.Formulas.Select(async y => await y.ToDouble()).WhenAll()).ToList(), x.FuncName)
                        ).WhenAll())
                        .Aggregate((now, next) => now * next);
                }

                denfuncs = denfuncs.Except(calced).ToList();
            }

            var nthroots = new Dictionary<int, Formula> { };
            if (NthRoots.Count() > 0)
            {
                nthroots = (await NthRoots.Select(async x => new { x.Key, Value = await x.Value.SubstituteX(target, value) }).WhenAll())
                                          .ToDictionary(x => x.Key, x => x.Value);

                var calced = nthroots.Where(x => x.Value.GetCharDics().Count() == 0);

                if (calced.Count() != 0)
                {
                    num *= (await calced.Select(async x => Math.Pow(x.Key, await x.Value.ToDouble())
                        ).WhenAll())
                        .Aggregate((now, next) => now * next);
                }

                foreach(var c in calced)
                {
                    NthRoots.Remove(c.Key);
                }
            }

            return new Monomial(
                num,
                Character,
                Recipchara,
                NumFuncs,
                DenFuncs,
                NthRoots
                );
        }

        /// <summary>
        /// ターゲットの文字が存在するところのみ抽出する
        /// </summary>
        /// <param name="Target"></param>
        /// <returns></returns>
        public Monomial GetTargetCharInfo (char Target)
        {
            return new Monomial(
                1.0,
                Character.Where(x => x.Key == Target).ToDictionary(x => x.Key, x => x.Value),
                Recipchara.Where(x => x.Key == Target).ToDictionary(x => x.Key, x => x.Value),
                NumFuncs.Select(x =>
                {
                    return new MonoFunc
                    {
                        FuncName = x.FuncName,
                        Formulas = x.Formulas.Select(y => y.GetTargetCharInfo(Target)).ToList()
                    };
                }).Where(x => x != new MonoFunc()).ToList(),
                DenFuncs.Select(x =>
                {
                    return new MonoFunc
                    {
                        FuncName = x.FuncName,
                        Formulas = x.Formulas.Select(y => y.GetTargetCharInfo(Target)).ToList()
                    };
                }).Where(x => x != new MonoFunc()).ToList(),
                NthRoots.Where(x => x.Value.GetCharDics().ContainsKey(Target))
                        .ToDictionary(x => x.Key, x => x.Value.GetTargetCharInfo(Target))
                );
        }



        /// <summary>
        /// 分子部分を取得する
        /// </summary>
        /// <returns></returns>
        public Monomial GetNumerator()
        {
            return new Monomial(
                Number.GetNumerator(),
                Character, 
                null,
                NumFuncs,
                null,
                NthRoots.ToDictionary(x => x.Key, x => new Formula(x.Value.NumMonof))
                );
        }

        /// <summary>
        /// 分母部分を取得する
        /// </summary>
        /// <returns></returns>
        public Monomial GetDenominator()
        {
            return new Monomial(
                Number.GetDenominator(),
                Recipchara,
                null,
                DenFuncs,
                null,
                NthRoots.ToDictionary(x => x.Key, x => new Formula(x.Value.DenMonof))
                );
        }

        /// <summary>
        /// 累乗で表せる部分を分離する
        /// </summary>
        /// <example>8x^2yを2乗で分離→2x</example>
        /// <param name="power">乗数(>=2)</param>
        /// <returns></returns>
        public static (Monomial,Monomial) SeparatePower(Monomial m, int power)
        {
            if (power < 2) return (1,m);

            var NumNumSeparates = SolveNumFormulas.PrimeFactors(m.Number.Numerator)
                            .GroupBy(x => x)
                            .ToDictionary(x => x.Key, x => x.Count())
                            .Select(x =>
                            {
                                var remain = x.Value % power;
                                return Math.Pow(x.Key, (x.Value - remain) / power);
                            });
            var NumNumSeparate = 1.0;
            if(NumNumSeparates.Count() > 0) NumNumSeparate = NumNumSeparates.Aggregate((now, next) => now * next);

            var NumDenSeparates = SolveNumFormulas.PrimeFactors(m.Number.Denominator)
                            .GroupBy(x => x)
                            .ToDictionary(x => x.Key, x => x.Count())
                            .Select(x =>
                            {
                                var powerx = power;
                                while (powerx < x.Value) powerx += power;
                                return Math.Pow(x.Key, powerx / power);
                            });
            var NumDenSeparate = 1.0;
            if (NumDenSeparates.Count() > 0) NumDenSeparate = NumDenSeparates.Aggregate((now, next) => now * next);

            var CharSeparate = m.Character.ToDictionary(x => x.Key, x => (x.Value - x.Value % power) / power);

            var RecipSeparate = m.Recipchara.ToDictionary(
                            x => x.Key,
                            x =>
                            {
                                var powerx = power;
                                while (powerx < x.Value) powerx += power;
                                return powerx / power;
                            });

            var NumFuncSeparates = m.NumFuncs.GroupBy(x => x)
                            .ToDictionary(x => x.Key, x => x.Count())
                            .Select(x =>
                            {
                                var remain = x.Value % power;
                                return Enumerable.Repeat(x.Key, (x.Value - remain) / power);
                            });
            var NumFuncSeparate = new List<MonoFunc> { };
            if (NumFuncSeparates.Count() > 0) NumFuncSeparate = NumFuncSeparates.Aggregate((now, next) => now.Concat(next)).ToList();

            var DenFuncSeparates = m.DenFuncs.GroupBy(x => x)
                            .ToDictionary(x => x.Key, x => x.Count())
                            .Select(x =>
                            {
                                var powerx = power;
                                while (powerx < x.Value) powerx += power;
                                return Enumerable.Repeat(x.Key, powerx / power);
                            });
            var DenFuncSeparate = new List<MonoFunc> { };
            if (DenFuncSeparates.Count() > 0) DenFuncSeparate = DenFuncSeparates.Aggregate((now, next) => now.Concat(next)).ToList();

            return (new Monomial(
                        NumNumSeparate / NumDenSeparate,
                        CharSeparate,
                        RecipSeparate,
                        NumFuncSeparate,
                        DenFuncSeparate
                        ),
                    new Monomial(
                        m.Number / Math.Pow(NumNumSeparate, power) * Math.Pow(NumDenSeparate, power),
                        m.Character.ToDictionary(x => x.Key, x => CharSeparate.ContainsKey(x.Key) ? x.Value - CharSeparate[x.Key] * power : x.Value),
                        m.Recipchara.ToDictionary(x => x.Key, x => RecipSeparate.ContainsKey(x.Key) ? x.Value - RecipSeparate[x.Key] * power : x.Value),
                        NumFuncSeparate.Count() > 0 ? m.NumFuncs.Concat(Enumerable.Repeat(NumFuncSeparate, 3).Aggregate((n, x) => n.Concat(x).ToList())).ToList() : m.NumFuncs,
                        DenFuncSeparate.Count() > 0 ? m.DenFuncs.Concat(Enumerable.Repeat(DenFuncSeparate, 3).Aggregate((n, x) => n.Concat(x).ToList())).ToList() : m.DenFuncs,
                        m.NthRoots
                        ));
        }

        /// <summary>
        /// xのみを消す
        /// </summary>
        /// <param name="target">消す文字</param>
        /// <returns></returns>
        public Monomial RemoveX(char target)
        {
            var RemovedCharacter = Character;
            RemovedCharacter.Remove(target);
            var RemovedRecipchara = Recipchara;
            RemovedRecipchara.Remove(target);

            var RemovedNumFuncs = NumFuncs.Select(x => new MonoFunc { FuncName = x.FuncName, Formulas = x.Formulas.Select(y => y.RemoveX(target)).ToList() });
            var RemovedDenFuncs = DenFuncs.Select(x => new MonoFunc { FuncName = x.FuncName, Formulas = x.Formulas.Select(y => y.RemoveX(target)).ToList() });

            var RemovedNthRoots = NthRoots.ToDictionary(x => x.Key, x => x.Value.RemoveX(target));

            return new Monomial(
                num: Number,
                chara: RemovedCharacter,
                recipchara: RemovedRecipchara,
                numfuncs: RemovedNumFuncs.ToList(),
                denfuncs: RemovedDenFuncs.ToList(),
                nthroots: RemovedNthRoots
                );
        }

        /// <summary>
        /// 文字列がMonomialにできるかを判断する
        /// </summary>
        /// <param name="f">判断する文字列</param>
        /// <param name="expand">True:展開してから判断,False:そのまま判断</param>
        /// <returns></returns>
        public static bool IsMonomial(string f,bool expand = false)
        {

            f = Regex.Replace(f, "^(\\+|-)", "");//行頭の符号を除外する
            f = Regex.Replace(f, "(\\(|\\*|/|\\+|\\^)-", "$1");//除算記号以外のマイナスを除外する

            while (f.Contains("#$"))//関数の内部を除外する
            {
                var rootIndex = Regex.Match(f, "#\\$([a-z|A-Z|0-9]|π)+\\$#");

                var rootBracketIndexes = OperateBrackets.LocationofInsideofBracket(f, rootIndex.Index + rootIndex.Length);

                f = f.Remove(rootIndex.Index, rootBracketIndexes.Item2 - rootIndex.Index + 1);
            }

            var map = OperateBrackets.MapingBrackets(f);
            if(map.First() != map.Last())
            {
                return false;
            }

            return !f.Contains("+") && !f.Contains("-");
        }

        public bool IsDouble()
        {
            return this.GetCharDics().Count() == 0;
        }

        /// <summary>
        /// できたら数字に変換
        /// </summary>
        /// <returns></returns>
        public async Task<double> ToDouble()
        {
            if(this.IsDouble())
            {
                var num = Number.ToDouble();

                if(NumFuncs.Count() != 0)
                {
                    num *= (await NumFuncs.Select(async x => 
                           await FuncItemService.RunFunc(
                               (await x.Formulas.Select(async y => await y.ToDouble()).WhenAll()).ToList(), 
                               x.FuncName))
                           .WhenAll())
                           .Aggregate((now, next) => now * next);
                }
                if(DenFuncs.Count() != 0)
                {
                    num /= (await NumFuncs.Select(async x =>
                           await FuncItemService.RunFunc(
                               (await x.Formulas.Select(async y => await y.ToDouble()).WhenAll()).ToList(), 
                               x.FuncName))
                           .WhenAll())
                           .Aggregate((now, next) => now * next);
                }
                if(NthRoots.Count() != 0)
                {
                    num *= (await NthRoots.Select(async x => Math.Pow(await x.Value.ToDouble(), x.Key)).WhenAll()).Aggregate((now, next) => now * next);
                }

                return num;
            }
            else
            {
                throw new FormatException();
            }
        }

        public bool IsOne()
        {
            return Number.ToDouble() == 1.0
                && Character.Count() == 0
                && Recipchara.Count() == 0
                && NumFuncs.Count() == 0
                && DenFuncs.Count() == 0
                && NthRoots.Count() == 0;
        }

        public bool IsZero()
        {
            return Number.IsZero()
                && Character.Count() == 0
                && Recipchara.Count() == 0
                && NumFuncs.Count() == 0
                && DenFuncs.Count() == 0
                && NthRoots.Count() == 0;
        }
    }

    public class MonoFunc
    {
        public string FuncName { get; set; }
        public List<Formula> Formulas { get; set; }

        public string GetString(bool ForDisplay = false, bool ReturnFraction = false)
        {
            if (ForDisplay)
            {
                return FuncName + "(" + string.Join(",", Formulas.Select(x => x.GetString(ReturnFraction))) + ")";
            }
            else
            {
                return "#$" + FuncName + "$#(" + string.Join(",", Formulas.Select(x => x.GetString(ReturnFraction))) + ")";
            }
        }

        public override bool Equals(object obj)
        {
            return this.GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode()
        {
            return FuncName.GetHashCode() 
                ^ (Formulas.Count() != 0 ? 
                        Formulas.Select(x => x.GetHashCode()).Aggregate((now, next) => now ^ next) 
                        : 1);
        }
    }

    
    public static class Extensions
    {
        public static string GetString(this Dictionary<char,int> character, bool ForDisplay = false)
        {
            if (character.Count() == 0) return "";

            if (ForDisplay)
            {
                return character.Select(x => x.Value != 1 ? x.Key + "^" + x.Value : x.Key.ToString())
                            .Aggregate((now, next) => now + next);
            }
            else
            {
                return character.Select(x => new string(x.Key, x.Value)).Aggregate((now, next) => now + next);
            }
            
        }

        public static bool IsEquals(this Dictionary<char,int> source, Dictionary<char,int> comparison)
        {
            return source.GetString() == comparison.GetString();
        }

        public static bool IsEquals(this List<MonoFunc> source, List<MonoFunc> comparison)
        {
            return source.Count() == comparison.Count() && source.All(x => comparison.Any(y => y.Equals(x)));
        }

        public static bool IsEquals(this List<Formula> source, List<Formula> comparison)
        {
            return source.Count() == comparison.Count() && source.All(x => comparison.Any(y => y == x));
        }

        public static bool IsEquals(this Dictionary<int,Formula> source, Dictionary<int,Formula> comparison)
        {
            return source.Count() == comparison.Count() 
                && source.All(x => comparison.ContainsKey(x.Key) && comparison[x.Key] == x.Value);
        }

        public static List<List<Monomial>> Grouping(this List<Monomial> m,
            bool IsNumberKey = true, bool IsCharKey = true, bool IsFuncsKey = true, bool IsRootKey = true)
        {
            var source = m;

            var groups = new List<List<Monomial>> { };

            while (source.Count() != 0)
            {
                var target = source[0];

                source.RemoveAt(0);

                var group = source.Where(x =>
                {
                    return (IsNumberKey ? x.Number == target.Number : true)
                        && (IsCharKey ? x.Character.IsEquals(target.Character) && x.Recipchara.IsEquals(target.Recipchara) : true)
                        && (IsFuncsKey ? x.NumFuncs.IsEquals(target.NumFuncs) && x.DenFuncs.IsEquals(target.DenFuncs) : true)
                        && (IsRootKey ? x.NthRoots.IsEquals(target.NthRoots) : true);
                }).ToList();
                
                source = source.ExceptAll(group).ToList();

                group.Add(target);

                groups.Add(group);
            }

            return groups;
        }

        
        /// <summary>
        /// ある要素からある要素を引き算する(重複はまとめない)
        /// from:https://stackoverflow.com/questions/2975944/except-has-similar-effect-to-distinct
        /// </summary>
        /// <example>
        /// >> a = { 1, 1, 2, 3, 5, 8 }
        /// >> b = { 1, 2, 4, 8 }
        /// >> a.ExceptAll(b)
        /// { 1, 3, 5 }
        /// </example>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static IEnumerable<TSource> ExceptAll<TSource>(this IEnumerable<TSource> first, IEnumerable<TSource> second)
        {
            return ExceptAll(first, second, null);
        }

        public static IEnumerable<TSource> ExceptAll<TSource>(
            this IEnumerable<TSource> first,
            IEnumerable<TSource> second,
            IEqualityComparer<TSource> comparer)
        {
            if (first == null) { throw new ArgumentNullException("first"); }
            if (second == null) { throw new ArgumentNullException("second"); }

            var secondCounts = new Dictionary<TSource, int>(comparer ?? EqualityComparer<TSource>.Default);
            int count;
            int nullCount = 0;

            // Count the values from second
            foreach (var item in second)
            {
                if (item == null)
                {
                    nullCount++;
                }
                else
                {
                    if (secondCounts.TryGetValue(item, out count))
                    {
                        secondCounts[item] = count + 1;
                    }
                    else
                    {
                        secondCounts.Add(item, 1);
                    }
                }
            }

            // Yield the values from first
            foreach (var item in first)
            {
                if (item == null)
                {
                    nullCount--;
                    if (nullCount < 0)
                    {
                        yield return item;
                    }
                }
                else
                {
                    if (secondCounts.TryGetValue(item, out count))
                    {
                        if (count == 0)
                        {
                            secondCounts.Remove(item);
                            yield return item;
                        }
                        else
                        {
                            secondCounts[item] = count - 1;
                        }
                    }
                    else
                    {
                        yield return item;
                    }
                }
            }
        }
    }


}
