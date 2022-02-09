//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated. To update the generation of this file, modify and re-run Unity.Mathematics.CodeGen.
// </auto-generated>
//------------------------------------------------------------------------------
using System;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

#pragma warning disable 0660, 0661

namespace Unity.Mathematics
{
    /// <summary>A 2x2 matrix of bools.</summary>
    [System.Serializable]
    [Il2CppEagerStaticClassConstruction]
    public partial struct bool2x2 : System.IEquatable<bool2x2>
    {
        /// <summary>Column 0 of the matrix.</summary>
        public bool2 c0;
        /// <summary>Column 1 of the matrix.</summary>
        public bool2 c1;


        /// <summary>Constructs a bool2x2 matrix from two bool2 vectors.</summary>
        /// <param name="c0">The matrix column c0 will be set to this value.</param>
        /// <param name="c1">The matrix column c1 will be set to this value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool2x2(bool2 c0, bool2 c1)
        {
            this.c0 = c0;
            this.c1 = c1;
        }

        /// <summary>Constructs a bool2x2 matrix from 4 bool values given in row-major order.</summary>
        /// <param name="m00">The matrix at row 0, column 0 will be set to this value.</param>
        /// <param name="m01">The matrix at row 0, column 1 will be set to this value.</param>
        /// <param name="m10">The matrix at row 1, column 0 will be set to this value.</param>
        /// <param name="m11">The matrix at row 1, column 1 will be set to this value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool2x2(bool m00, bool m01,
                       bool m10, bool m11)
        {
            this.c0 = new bool2(m00, m10);
            this.c1 = new bool2(m01, m11);
        }

        /// <summary>Constructs a bool2x2 matrix from a single bool value by assigning it to every component.</summary>
        /// <param name="v">bool to convert to bool2x2</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool2x2(bool v)
        {
            this.c0 = v;
            this.c1 = v;
        }


        /// <summary>Implicitly converts a single bool value to a bool2x2 matrix by assigning it to every component.</summary>
        /// <param name="v">bool to convert to bool2x2</param>
        /// <returns>Converted value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool2x2(bool v) { return new bool2x2(v); }


        /// <summary>Returns the result of a componentwise equality operation on two bool2x2 matrices.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise equality.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise equality.</param>
        /// <returns>bool2x2 result of the componentwise equality.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator == (bool2x2 lhs, bool2x2 rhs) { return new bool2x2 (lhs.c0 == rhs.c0, lhs.c1 == rhs.c1); }

        /// <summary>Returns the result of a componentwise equality operation on a bool2x2 matrix and a bool value.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise equality.</param>
        /// <param name="rhs">Right hand side bool to use to compute componentwise equality.</param>
        /// <returns>bool2x2 result of the componentwise equality.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator == (bool2x2 lhs, bool rhs) { return new bool2x2 (lhs.c0 == rhs, lhs.c1 == rhs); }

        /// <summary>Returns the result of a componentwise equality operation on a bool value and a bool2x2 matrix.</summary>
        /// <param name="lhs">Left hand side bool to use to compute componentwise equality.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise equality.</param>
        /// <returns>bool2x2 result of the componentwise equality.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator == (bool lhs, bool2x2 rhs) { return new bool2x2 (lhs == rhs.c0, lhs == rhs.c1); }


        /// <summary>Returns the result of a componentwise not equal operation on two bool2x2 matrices.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise not equal.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise not equal.</param>
        /// <returns>bool2x2 result of the componentwise not equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator != (bool2x2 lhs, bool2x2 rhs) { return new bool2x2 (lhs.c0 != rhs.c0, lhs.c1 != rhs.c1); }

        /// <summary>Returns the result of a componentwise not equal operation on a bool2x2 matrix and a bool value.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise not equal.</param>
        /// <param name="rhs">Right hand side bool to use to compute componentwise not equal.</param>
        /// <returns>bool2x2 result of the componentwise not equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator != (bool2x2 lhs, bool rhs) { return new bool2x2 (lhs.c0 != rhs, lhs.c1 != rhs); }

        /// <summary>Returns the result of a componentwise not equal operation on a bool value and a bool2x2 matrix.</summary>
        /// <param name="lhs">Left hand side bool to use to compute componentwise not equal.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise not equal.</param>
        /// <returns>bool2x2 result of the componentwise not equal.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator != (bool lhs, bool2x2 rhs) { return new bool2x2 (lhs != rhs.c0, lhs != rhs.c1); }


        /// <summary>Returns the result of a componentwise not operation on a bool2x2 matrix.</summary>
        /// <param name="val">Value to use when computing the componentwise not.</param>
        /// <returns>bool2x2 result of the componentwise not.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator ! (bool2x2 val) { return new bool2x2 (!val.c0, !val.c1); }


        /// <summary>Returns the result of a componentwise bitwise and operation on two bool2x2 matrices.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise bitwise and.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise bitwise and.</param>
        /// <returns>bool2x2 result of the componentwise bitwise and.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator & (bool2x2 lhs, bool2x2 rhs) { return new bool2x2 (lhs.c0 & rhs.c0, lhs.c1 & rhs.c1); }

        /// <summary>Returns the result of a componentwise bitwise and operation on a bool2x2 matrix and a bool value.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise bitwise and.</param>
        /// <param name="rhs">Right hand side bool to use to compute componentwise bitwise and.</param>
        /// <returns>bool2x2 result of the componentwise bitwise and.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator & (bool2x2 lhs, bool rhs) { return new bool2x2 (lhs.c0 & rhs, lhs.c1 & rhs); }

        /// <summary>Returns the result of a componentwise bitwise and operation on a bool value and a bool2x2 matrix.</summary>
        /// <param name="lhs">Left hand side bool to use to compute componentwise bitwise and.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise bitwise and.</param>
        /// <returns>bool2x2 result of the componentwise bitwise and.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator & (bool lhs, bool2x2 rhs) { return new bool2x2 (lhs & rhs.c0, lhs & rhs.c1); }


        /// <summary>Returns the result of a componentwise bitwise or operation on two bool2x2 matrices.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise bitwise or.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise bitwise or.</param>
        /// <returns>bool2x2 result of the componentwise bitwise or.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator | (bool2x2 lhs, bool2x2 rhs) { return new bool2x2 (lhs.c0 | rhs.c0, lhs.c1 | rhs.c1); }

        /// <summary>Returns the result of a componentwise bitwise or operation on a bool2x2 matrix and a bool value.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise bitwise or.</param>
        /// <param name="rhs">Right hand side bool to use to compute componentwise bitwise or.</param>
        /// <returns>bool2x2 result of the componentwise bitwise or.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator | (bool2x2 lhs, bool rhs) { return new bool2x2 (lhs.c0 | rhs, lhs.c1 | rhs); }

        /// <summary>Returns the result of a componentwise bitwise or operation on a bool value and a bool2x2 matrix.</summary>
        /// <param name="lhs">Left hand side bool to use to compute componentwise bitwise or.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise bitwise or.</param>
        /// <returns>bool2x2 result of the componentwise bitwise or.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator | (bool lhs, bool2x2 rhs) { return new bool2x2 (lhs | rhs.c0, lhs | rhs.c1); }


        /// <summary>Returns the result of a componentwise bitwise exclusive or operation on two bool2x2 matrices.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise bitwise exclusive or.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise bitwise exclusive or.</param>
        /// <returns>bool2x2 result of the componentwise bitwise exclusive or.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator ^ (bool2x2 lhs, bool2x2 rhs) { return new bool2x2 (lhs.c0 ^ rhs.c0, lhs.c1 ^ rhs.c1); }

        /// <summary>Returns the result of a componentwise bitwise exclusive or operation on a bool2x2 matrix and a bool value.</summary>
        /// <param name="lhs">Left hand side bool2x2 to use to compute componentwise bitwise exclusive or.</param>
        /// <param name="rhs">Right hand side bool to use to compute componentwise bitwise exclusive or.</param>
        /// <returns>bool2x2 result of the componentwise bitwise exclusive or.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator ^ (bool2x2 lhs, bool rhs) { return new bool2x2 (lhs.c0 ^ rhs, lhs.c1 ^ rhs); }

        /// <summary>Returns the result of a componentwise bitwise exclusive or operation on a bool value and a bool2x2 matrix.</summary>
        /// <param name="lhs">Left hand side bool to use to compute componentwise bitwise exclusive or.</param>
        /// <param name="rhs">Right hand side bool2x2 to use to compute componentwise bitwise exclusive or.</param>
        /// <returns>bool2x2 result of the componentwise bitwise exclusive or.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 operator ^ (bool lhs, bool2x2 rhs) { return new bool2x2 (lhs ^ rhs.c0, lhs ^ rhs.c1); }



        /// <summary>Returns the bool2 element at a specified index.</summary>
        unsafe public ref bool2 this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if ((uint)index >= 2)
                    throw new System.ArgumentException("index must be between[0...1]");
#endif
                fixed (bool2x2* array = &this) { return ref ((bool2*)array)[index]; }
            }
        }

        /// <summary>Returns true if the bool2x2 is equal to a given bool2x2, false otherwise.</summary>
        /// <param name="rhs">Right hand side argument to compare equality with.</param>
        /// <returns>The result of the equality comparison.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(bool2x2 rhs) { return c0.Equals(rhs.c0) && c1.Equals(rhs.c1); }

        /// <summary>Returns true if the bool2x2 is equal to a given bool2x2, false otherwise.</summary>
        /// <param name="o">Right hand side argument to compare equality with.</param>
        /// <returns>The result of the equality comparison.</returns>
        public override bool Equals(object o) { return o is bool2x2 converted && Equals(converted); }


        /// <summary>Returns a hash code for the bool2x2.</summary>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() { return (int)math.hash(this); }


        /// <summary>Returns a string representation of the bool2x2.</summary>
        /// <returns>String representation of the value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
            return string.Format("bool2x2({0}, {1},  {2}, {3})", c0.x, c1.x, c0.y, c1.y);
        }

    }

    public static partial class math
    {
        /// <summary>Returns a bool2x2 matrix constructed from two bool2 vectors.</summary>
        /// <param name="c0">The matrix column c0 will be set to this value.</param>
        /// <param name="c1">The matrix column c1 will be set to this value.</param>
        /// <returns>bool2x2 constructed from arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 bool2x2(bool2 c0, bool2 c1) { return new bool2x2(c0, c1); }

        /// <summary>Returns a bool2x2 matrix constructed from from 4 bool values given in row-major order.</summary>
        /// <param name="m00">The matrix at row 0, column 0 will be set to this value.</param>
        /// <param name="m01">The matrix at row 0, column 1 will be set to this value.</param>
        /// <param name="m10">The matrix at row 1, column 0 will be set to this value.</param>
        /// <param name="m11">The matrix at row 1, column 1 will be set to this value.</param>
        /// <returns>bool2x2 constructed from arguments.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 bool2x2(bool m00, bool m01,
                                      bool m10, bool m11)
        {
            return new bool2x2(m00, m01,
                               m10, m11);
        }

        /// <summary>Returns a bool2x2 matrix constructed from a single bool value by assigning it to every component.</summary>
        /// <param name="v">bool to convert to bool2x2</param>
        /// <returns>Converted value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 bool2x2(bool v) { return new bool2x2(v); }

        /// <summary>Return the bool2x2 transpose of a bool2x2 matrix.</summary>
        /// <param name="v">Value to transpose.</param>
        /// <returns>Transposed value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool2x2 transpose(bool2x2 v)
        {
            return bool2x2(
                v.c0.x, v.c0.y,
                v.c1.x, v.c1.y);
        }

        /// <summary>Returns a uint hash code of a bool2x2 matrix.</summary>
        /// <param name="v">Matrix value to hash.</param>
        /// <returns>uint hash of the argument.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint hash(bool2x2 v)
        {
            return csum(select(uint2(0xAAC3C25Du, 0xD21D0945u), uint2(0x88FCAB2Du, 0x614DA60Du), v.c0) +
                        select(uint2(0x5BA2C50Bu, 0x8C455ACBu), uint2(0xCD266C89u, 0xF1852A33u), v.c1));
        }

        /// <summary>
        /// Returns a uint2 vector hash code of a bool2x2 matrix.
        /// When multiple elements are to be hashes together, it can more efficient to calculate and combine wide hash
        /// that are only reduced to a narrow uint hash at the very end instead of at every step.
        /// </summary>
        /// <param name="v">Matrix value to hash.</param>
        /// <returns>uint2 hash of the argument.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint2 hashwide(bool2x2 v)
        {
            return (select(uint2(0x77E35E77u, 0x863E3729u), uint2(0xE191B035u, 0x68586FAFu), v.c0) +
                    select(uint2(0xD4DFF6D3u, 0xCB634F4Du), uint2(0x9B13B92Du, 0x4ABF0813u), v.c1));
        }

    }
}
