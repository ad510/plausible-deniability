// fixed point arithmetic
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
    public class FP
    {
        /// <summary>
        /// fixed point bit precision past decimal point
        /// </summary>
        public const int Precision = 16;

        /// <summary>
        /// fixed point vector
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

            public long lengthSq()
            {
                return x * x + y * y;
            }

            // todo: implement length and length3 using fixed point sqrt
            public long length()
            {
                return (long)Math.Sqrt(x * x + y * y);
            }

            public long length3Sq()
            {
                return x * x + y * y + z * z;
            }

            public long length3()
            {
                return (long)Math.Sqrt(x * x + y * y + z * z);
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
        public static long mul(long left, long right)
        {
            return (left * right) >> Precision;
        }
        
        /// <summary>
        /// fixed point division
        /// </summary>
        public static long div(long left, long right)
        {
            return (left << Precision) / right;
        }

        public static long fromDouble(double from)
        {
            return (long)(from * Math.Pow(2, Precision));
        }

        public static double toDouble(long from)
        {
            return (double)from / Math.Pow(2, Precision);
        }
    }
}
