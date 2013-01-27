// fixed point arithmetic
// Copyright (c) 2013 Andrew Downing
// This work is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License. To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/.
// If this license is too restrictive for you, the copy on GitHub at https://github.com/ad510/decoherence is licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Decoherence
{
    public class FP
    {
        public const int Precision = 15; // fixed point bit precision past decimal point

        public struct Vector // fixed point vector
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

        public static long mul(long left, long right) // fixed point multiplication
        {
            return (left * right) >> Precision;
        }

        public static long div(long left, long right) // fixed point division
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
