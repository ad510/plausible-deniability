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
    public struct FixPt
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
            public FixPt x;
            public FixPt y;
            public FixPt z;

            public Vector(FixPt xVal, FixPt yVal, FixPt zVal)
            {
                x = xVal;
                y = yVal;
                z = zVal;
            }

            public Vector(FixPt xVal, FixPt yVal)
            {
                x = xVal;
                y = yVal;
                z.data = 0;
            }

            public Vector(Vector vec)
            {
                this = vec;
            }

            public long lengthSq()
            {
                return x.data * x.data + y.data * y.data;
            }

            // todo: implement length and length3 using fixed point sqrt
            public FixPt length()
            {
                return new FixPt((long)Math.Sqrt(lengthSq()));
            }

            public long length3Sq()
            {
                return x.data * x.data + y.data * y.data + z.data * z.data;
            }

            public FixPt length3()
            {
                return new FixPt((long)Math.Sqrt(length3Sq()));
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

            public static Vector operator *(Vector left, FixPt right)
            {
                return new Vector(left.x * right, left.y * right, left.z * right);
            }

            public static Vector operator *(FixPt right, Vector left)
            {
                return left * right;
            }

            public static Vector operator /(Vector left, FixPt right)
            {
                return new Vector(left.x / right, left.y / right, left.z / right);
            }

            public static bool operator ==(Vector left, Vector right)
            {
                return left.x == right.x && left.y == right.y && left.z == right.z;
            }

            public static bool operator !=(Vector left, Vector right)
            {
                return !(left.x == right.x && left.y == right.y && left.z == right.z);
            }
        }

        public long data; // fixed point number data

        public FixPt(long dataVal)
        {
            data = dataVal;
        }

        public FixPt(FixPt fp)
        {
            data = fp.data;
        }

        public static FixPt fromLong(long from)
        {
            return new FixPt(from << Precision);
        }

        public static long toLong(FixPt fp)
        {
            return fp.data >> Precision;
        }

        public static FixPt fromDouble(double from)
        {
            return new FixPt((long)(from * Math.Pow(2, Precision)));
        }

        public double toDouble()
        {
            return (double)data / Math.Pow(2, Precision);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public bool Equals(FixPt fp)
        {
            return this == fp;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static FixPt max(FixPt left, FixPt right)
        {
            return (left > right) ? left : right;
        }

        public static FixPt min(FixPt left, FixPt right)
        {
            return (left < right) ? left : right;
        }

        public static FixPt operator -(FixPt fp)
        {
            return new FixPt(-fp.data);
        }

        public static FixPt operator +(FixPt left, FixPt right)
        {
            return new FixPt(left.data + right.data);
        }

        public static FixPt operator -(FixPt left, FixPt right)
        {
            return new FixPt(left.data - right.data);
        }

        public static FixPt operator *(FixPt left, FixPt right)
        {
            return new FixPt((left.data * right.data) >> Precision);
        }

        public static FixPt operator /(FixPt left, FixPt right)
        {
            return new FixPt((left.data << Precision) / right.data);
        }

        public static bool operator >(FixPt left, FixPt right)
        {
            return left.data > right.data;
        }

        public static bool operator <(FixPt left, FixPt right)
        {
            return left.data < right.data;
        }

        public static bool operator >=(FixPt left, FixPt right)
        {
            return left.data >= right.data;
        }

        public static bool operator <=(FixPt left, FixPt right)
        {
            return left.data <= right.data;
        }

        public static bool operator ==(FixPt left, FixPt right)
        {
            return left.data == right.data;
        }

        public static bool operator !=(FixPt left, FixPt right)
        {
            return left.data != right.data;
        }
    }
}
