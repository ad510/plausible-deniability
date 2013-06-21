// Copyright (c) 2013 Andrew Downing
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Decoherence
{
    /// <summary>
    /// fixed point arithmetic
    /// </summary>
    public class FP
    {
        /// <summary>
        /// fixed point bit precision past decimal point
        /// </summary>
        public const int Precision = 16;
        public const long Sqrt2 = 92681;
        public const long Pi = 205887;

        /// <summary>
        /// fixed point 3D vector
        /// </summary>
        public struct Vector
        {
            public long x;
            public long y;
            public long z;

            public Vector(long xVal, long yVal, long zVal = 0)
            {
                x = xVal;
                y = yVal;
                z = zVal;
            }

            public Vector(Vector vec)
            {
                this = vec;
            }

            /// <summary>
            /// returns the 2-dimensional (x and y) squared length of the vector, left shifted by Precision bits
            /// </summary>
            /// <remarks>this is much faster to calculate than the length, so use for distance comparisons</remarks>
            public long lengthSq()
            {
                return x * x + y * y;
            }

            /// <summary>
            /// returns the 2-dimensional (x and y) length of the vector
            /// </summary>
            public long length()
            {
                return sqrt(x * x + y * y);
            }

            /// <summary>
            /// returns the 3-dimensional squared length of the vector, left shifted by Precision bits
            /// </summary>
            /// <remarks>this is much faster to calculate than the length, so use for distance comparisons</remarks>
            public long length3Sq()
            {
                return x * x + y * y + z * z;
            }

            /// <summary>
            /// returns the 3-dimensional length of the vector
            /// </summary>
            public long length3()
            {
                return sqrt(x * x + y * y + z * z);
            }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public bool Equals(Vector vec)
            {
                return this == vec;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static Vector operator -(Vector vec)
            {
                return new Vector(-vec.x, -vec.y, -vec.z);
            }

            public static Vector operator +(Vector left, Vector right)
            {
                return new Vector(left.x + right.x, left.y + right.y, left.z + right.z);
            }

            public static Vector operator -(Vector left, Vector right)
            {
                return new Vector(left.x - right.x, left.y - right.y, left.z - right.z);
            }

            public static Vector operator *(Vector left, long right)
            {
                return new Vector(mul(left.x, right), mul(left.y, right), mul(left.z, right));
            }

            public static Vector operator *(long right, Vector left)
            {
                return left * right;
            }

            public static Vector operator /(Vector left, long right)
            {
                return new Vector(div(left.x, right), div(left.y, right), div(left.z, right));
            }

            public static bool operator ==(Vector left, Vector right)
            {
                if (left.x == right.x && left.y == right.y && left.z == right.z) return true;
                return false;
            }

            public static bool operator !=(Vector left, Vector right)
            {
                if (left.x == right.x && left.y == right.y && left.z == right.z) return false;
                return true;
            }
        }

        /// <summary>
        /// fixed point multiplication
        /// </summary>
        /// <remarks>
        /// I also tried making a fixed point data type to replace these functions in commit a2523a5,
        /// but there was too much of a performance hit so I reverted it in commit 7d01306
        /// </remarks>
        public static long mul(long left, long right)
        {
            return (left * right) >> Precision;
        }
        
        /// <summary>
        /// fixed point division
        /// </summary>
        /// <remarks>
        /// I also tried making a fixed point data type to replace these functions in commit a2523a5,
        /// but there was too much of a performance hit so I reverted it in commit 7d01306
        /// </remarks>
        public static long div(long left, long right)
        {
            return (left << Precision) / right;
        }

        /// <summary>
        /// integer square root (if passing in fixed point number, left shift it by Precision first)
        /// </summary>
        /// <remarks>uses Babylonian method, described at https://en.wikipedia.org/wiki/Methods_of_computing_square_roots#Babylonian_method </remarks>
        public static long sqrt(long x)
        {
            if (x == 0) return 0;
            long ret = 1;
            for (int i = 0; i < 30; i++)
            {
                ret = (ret + x / ret) >> 1;
            }
            return ret;
        }

        /// <summary>
        /// fixed point cosine
        /// </summary>
        public static long cos(long x)
        {
            // ensure -Pi/2 <= x <= Pi/2
            x = Math.Abs(x % (2 * Pi));
            if (x > Pi) x = 2 * Pi - x;
            bool flip = (x > Pi / 2);
            if (flip) x = Pi - x;
            // use 4th order Taylor series
            x = (1 << Precision) - mul(x, x) / 2 + mul(mul(x, x), mul(x, x)) / 24;
            return flip ? -x : x;
        }

        /// <summary>
        /// fixed point sine
        /// </summary>
        public static long sin(long x)
        {
            return cos(x - Pi / 2);
        }

        /// <summary>
        /// returns fixed point value equivalent to specified double (except for rounding errors)
        /// </summary>
        public static long fromDouble(double from)
        {
            return (long)(from * Math.Pow(2, Precision));
        }

        /// <summary>
        /// returns double value equivalent to specified fixed point number (except for rounding errors)
        /// </summary>
        public static double toDouble(long from)
        {
            return (double)from / Math.Pow(2, Precision);
        }

        /// <summary>
        /// returns x value of line between p1 and p2 at specified y value
        /// </summary>
        public static long lineCalcX(Vector p1, Vector p2, long y)
        {
            return mul(y - p1.y, div(p2.x - p1.x, p2.y - p1.y)) + p1.x;
        }

        /// <summary>
        /// returns y value of line between p1 and p2 at specified x value
        /// </summary>
        public static long lineCalcY(Vector p1, Vector p2, long x)
        {
            return mul(x - p1.x, div(p2.y - p1.y, p2.x - p1.x)) + p1.y; // easily derived from point-slope form
        }
    }
}
