using System;
using System.Collections.Generic;
using System.Text;

namespace SolveEquations
{
    /// <summary>
    /// 分数クラス
    /// </summary>
    public class Fraction
    {
        private ulong _Numerator;
        private ulong _Denominator;
        private bool _Sign;

        /// <summary>
        /// 分子
        /// </summary>
        public ulong Numerator
        {
            get { return _Numerator; }
            set { _Numerator = value; }
        }
        /// <summary>
        /// 分母
        /// </summary>
        public ulong Denominator
        {
            get { return _Denominator; }
            set { _Denominator = value; }
        }
        /// <summary>
        /// 符号
        /// </summary>
        public bool Sign
        {
            get { return _Sign; }
            set { _Sign = value; }
        }

        /// <summary>
        /// 分数クラス
        /// </summary>
        /// <param name="positive"></param>
        /// <param name="numerator"></param>
        /// <param name="denominator"></param>
        public Fraction(bool positive, ulong numerator, ulong denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
            Sign = positive;

            Reduction();
        }

        /// <summary>
        /// 分数クラス
        /// </summary>
        /// <param name="numerator"></param>
        /// <param name="denominator"></param>
        public Fraction(long numerator, long denominator)
        {
            if (denominator == 0)
            {
                throw new DivideByZeroException();
            }

            if (numerator == 0)
            {
                Numerator = 0;
                Denominator = 1;
            }
            else
            {
                Sign = Math.Sign(numerator) == Math.Sign(denominator);

                Numerator = (ulong)Math.Abs(numerator);
                Denominator = (ulong)Math.Abs(denominator);

                Reduction();
            }

        }

        /// <summary>
        /// 分数クラス
        /// </summary>
        /// <param name="d"></param>
        public Fraction(double d)
        {
            Sign = true;

            if(d < 0)
            {
                d = Math.Abs(d);
                Sign = false;
            }

            int factor = 1;

            //がんばって整数になる数を探す
            //無理数など見つからない場合は近似する
            while((d * factor).ToString().Contains("."))
            {
                factor++;

                if (factor > 10000)
                {
                    d = Math.Round(d, 4);
                    factor = 1;
                }
            }

            Numerator = (ulong)(factor * d);
            Denominator = (ulong)factor;

            Reduction();
        }

        /// <summary>
        /// 分数クラス
        /// </summary>
        /// <param name="i"></param>
        public Fraction(long i)
        {
            if(i < 0)
            {
                Sign = false;
                i = Math.Abs(i);
            }
            else
            {
                Sign = true;
            }

            Numerator = (ulong)i;
            Denominator = 1;
            
        }

        /// <summary>
        /// 分数を簡単にする
        /// </summary>
        private void Reduction()
        {
            var factor = Factorizations.GCD((long)Numerator, (long)Denominator);

            Numerator = Numerator / (ulong)factor;
            Denominator = Denominator / (ulong)factor;
        }

        public static implicit operator Fraction(long i)
        {
            return new Fraction(i, 1);
        }
        public static implicit operator Fraction(double d)
        {
            return new Fraction(d);
        }

        //演算子オーバーロード
        public static Fraction operator +(Fraction a, Fraction b)
        {
            int anum = (int)a.Numerator;
            int bnum = (int)b.Numerator;

            if (!a.Sign) anum = -anum;
            if (!b.Sign) bnum = -bnum;

            return new Fraction(anum * (int)b.Denominator + bnum * (int)a.Denominator, (int)a.Denominator * (int)b.Denominator);
        }

        public static Fraction operator -(Fraction a, Fraction b)
        {
            return a + new Fraction(!b.Sign, b.Numerator, b.Denominator);
        }

        public static Fraction operator *(Fraction a, Fraction b)
        {
            return new Fraction(a.Sign == b.Sign, a.Numerator * b.Numerator, a.Denominator * b.Denominator);
        }

        public static Fraction operator /(Fraction a, Fraction b)
        {
            return a * b.Invert();
        }

        public static Fraction operator ^(Fraction a, int b)
        {
            return new Fraction(a.Sign || b % 2 == 0, (ulong)Math.Pow(a.Numerator,b), (ulong)Math.Pow(a.Denominator,b));
        }

        public static Fraction operator -(Fraction a)
        {
            return a.SignReverse();
        }

        public static bool operator <(Fraction a, Fraction b)
        {
            return a.ToDouble() < b.ToDouble();
        }

        public static bool operator >(Fraction a, Fraction b)
        {
            return a.ToDouble() > b.ToDouble();
        }

        public static bool operator >=(Fraction a, Fraction b)
        {
            return a.ToDouble() >= b.ToDouble();
        }

        public static bool operator <=(Fraction a, Fraction b)
        {
            return a.ToDouble() <= b.ToDouble();
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
            return (int)Numerator ^ (int)Denominator ^ Sign.GetHashCode();
        }

        /// <summary>
        /// 分数の値がゼロかどうかを判定
        /// </summary>
        /// <returns></returns>
        public bool IsZero()
        {
            return Numerator == 0;
        }

        /// <summary>
        /// 逆数
        /// </summary>
        /// <returns></returns>
        public Fraction Invert()
        {
            return new Fraction(Sign, Denominator, Numerator);
        }

        /// <summary>
        /// 逆符号
        /// </summary>
        /// <returns></returns>
        public Fraction SignReverse()
        {
            return new Fraction(!Sign, Numerator, Denominator);
        }

        /// <summary>
        /// 符号付きで分子を取得
        /// </summary>
        /// <returns></returns>
        public long GetNumerator()
        {
            if (Sign)
            {
                return (long)Numerator;
            }
            else
            {
                return -(long)Numerator;
            }
        }

        /// <summary>
        /// 符号付きで分母を取得
        /// </summary>
        /// <returns></returns>
        public long GetDenominator()
        {
            return (long)Denominator;
        }

        /// <summary>
        /// 小数への変換
        /// </summary>
        /// <returns></returns>
        public Double ToDouble()
        {
            if (Sign)
            {
                return (double)Numerator / Denominator;
            }
            else
            {
                return -(double)Numerator / Denominator;
            }
            
        }

        public override string ToString()
        {
            return (Sign ? "" : "-") + Numerator + "/" + Denominator;
        }
    }
}
