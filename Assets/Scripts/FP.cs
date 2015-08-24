// Written in 2013 by Andrew Downing
// To the extent possible under law, the author(s) have dedicated all copyright and related and neighboring rights to this software to the public domain worldwide. This software is distributed without any warranty.
// You should have received a copy of the CC0 Public Domain Dedication along with this software. If not, see https://creativecommons.org/publicdomain/zero/1.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;

/// <summary>
/// fixed point arithmetic
/// </summary>
public static class FP {
	/// <summary>
	/// fixed point bit precision past decimal point
	/// </summary>
	public const int precision = 16;
	public const long sqrt2 = 92681;
	public const long pi = 205887;

	/// <summary>
	/// fixed point 3D vector
	/// </summary>
	[ProtoContract]
	public struct Vector : IEquatable<Vector> {
		[ProtoMember(1)] public long x;
		[ProtoMember(2)] public long y;
		[ProtoMember(3)] public long z;

		public Vector(long xVal, long yVal, long zVal = 0) {
			x = xVal;
			y = yVal;
			z = zVal;
		}

		/// <summary>
		/// returns the 2-dimensional (x and y) squared length of the vector, left shifted by Precision bits
		/// </summary>
		/// <remarks>this is much faster to calculate than the length, so use for distance comparisons</remarks>
		public long lengthSq() {
			return x * x + y * y;
		}

		/// <summary>
		/// returns the 2-dimensional (x and y) length of the vector
		/// </summary>
		public long length() {
			return sqrt(x * x + y * y);
		}

		/// <summary>
		/// returns the 3-dimensional squared length of the vector, left shifted by Precision bits
		/// </summary>
		/// <remarks>this is much faster to calculate than the length, so use for distance comparisons</remarks>
		public long length3Sq() {
			return x * x + y * y + z * z;
		}

		/// <summary>
		/// returns the 3-dimensional length of the vector
		/// </summary>
		public long length3() {
			return sqrt(x * x + y * y + z * z);
		}

		public override bool Equals(object obj) {
			if (obj is Vector) return Equals ((Vector)obj);
			return false;
		}

		public bool Equals(Vector other) {
			return x == other.x && y == other.y && z == other.z;
		}

		public override int GetHashCode() {
			// this algorithm suggested at https://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode
			return unchecked((x.GetHashCode () * 31 + y.GetHashCode ()) * 31 + z.GetHashCode ());
		}

		public static Vector operator -(Vector vec) {
			return new Vector(-vec.x, -vec.y, -vec.z);
		}

		public static Vector operator +(Vector left, Vector right) {
			return new Vector(left.x + right.x, left.y + right.y, left.z + right.z);
		}

		public static Vector operator -(Vector left, Vector right) {
			return new Vector(left.x - right.x, left.y - right.y, left.z - right.z);
		}

		public static Vector operator *(Vector left, long right) {
			return new Vector(mul(left.x, right), mul(left.y, right), mul(left.z, right));
		}

		public static Vector operator *(long left, Vector right) {
			return new Vector(mul(left, right.x), mul(left, right.y), mul(left, right.z));
		}

		public static Vector operator /(Vector left, long right) {
			return new Vector(div(left.x, right), div(left.y, right), div(left.z, right));
		}

		public static bool operator ==(Vector left, Vector right) {
			return left.Equals (right);
		}

		public static bool operator !=(Vector left, Vector right) {
			return !left.Equals (right);
		}
	}

	/// <summary>
	/// fixed point multiplication
	/// </summary>
	/// <remarks>
	/// I also tried making a fixed point data type to replace these functions in commit a2523a5,
	/// but there was too much of a performance hit so I reverted it in commit 7d01306
	/// </remarks>
	public static long mul(long left, long right) {
		return (left * right) >> precision;
	}
	
	/// <summary>
	/// fixed point division
	/// </summary>
	/// <remarks>
	/// I also tried making a fixed point data type to replace these functions in commit a2523a5,
	/// but there was too much of a performance hit so I reverted it in commit 7d01306
	/// </remarks>
	public static long div(long left, long right) {
		return (left << precision) / right;
	}

	/// <summary>
	/// integer square root (if passing in fixed point number, left shift it by Precision first)
	/// </summary>
	/// <remarks>uses Babylonian method, described at https://en.wikipedia.org/wiki/Methods_of_computing_square_roots#Babylonian_method </remarks>
	public static long sqrt(long x) {
		if (x == 0) return 0;
		long ret = 1;
		for (int i = 0; i < 30; i++) {
			ret = (ret + x / ret) >> 1;
		}
		return ret;
	}

	/// <summary>
	/// fixed point cosine
	/// </summary>
	public static long cos(long x) {
		// ensure -Pi/2 <= x <= Pi/2
		x = Math.Abs(x % (2 * pi));
		if (x > pi) x = 2 * pi - x;
		bool flip = (x > pi / 2);
		if (flip) x = pi - x;
		// use 4th order Taylor series
		x = (1 << precision) - mul(x, x) / 2 + mul(mul(x, x), mul(x, x)) / 24;
		return flip ? -x : x;
	}

	/// <summary>
	/// fixed point sine
	/// </summary>
	public static long sin(long x) {
		return cos(x - pi / 2);
	}

	/// <summary>
	/// returns fixed point value equivalent to specified double (except for rounding errors)
	/// </summary>
	public static long fromDouble(double from) {
		return (long)(from * Math.Pow(2, precision));
	}

	/// <summary>
	/// returns double value equivalent to specified fixed point number (except for rounding errors)
	/// </summary>
	public static double toDouble(long from) {
		return (double)from / Math.Pow(2, precision);
	}
	
	/// <summary>
	/// returns whether rectangle formed by r1 and r2 contains point p (checks x and y only)
	/// </summary>
	public static bool rectContains(Vector r1, Vector r2, Vector p) {
		return p.x >= r1.x && p.x <= r2.x && p.y >= r1.y && p.y <= r2.y;
	}
	
	/// <summary>
	/// returns whether rectangle formed by r1p1 and r2p2 fully contains rectangle formed by r2p1 and r2p2 (checks x and y only)
	/// </summary>
	public static bool rectContains(Vector r1p1, Vector r1p2, Vector r2p1, Vector r2p2) {
		return r2p1.x >= r1p1.x && r2p2.x <= r1p2.x && r2p1.y >= r1p1.y && r2p2.y <= r1p2.y;
	}
	
	/// <summary>
	/// returns whether rectangle formed by r1p1 and r1p2 intersects rectangle formed by r2p1 and r2p2 (checks x and y only)
	/// </summary>
	public static bool rectIntersects(Vector r1p1, Vector r1p2, Vector r2p1, Vector r2p2) {
		return r2p2.x >= r1p1.x && r2p1.x <= r1p2.x && r2p2.y >= r1p1.y && r2p1.y <= r1p2.y;
	}

	/// <summary>
	/// returns x value of line between p1 and p2 at specified y value
	/// </summary>
	public static long lineCalcX(Vector p1, Vector p2, long y) {
		return mul(y - p1.y, div(p2.x - p1.x, p2.y - p1.y)) + p1.x;
	}

	/// <summary>
	/// returns y value of line between p1 and p2 at specified x value
	/// </summary>
	public static long lineCalcY(Vector p1, Vector p2, long x) {
		return mul(x - p1.x, div(p2.y - p1.y, p2.x - p1.x)) + p1.y; // easily derived from point-slope form
	}
}
