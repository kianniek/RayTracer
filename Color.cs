using OpenTK.Mathematics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace INFOGR_RAYTRACER
{
    [Serializable]
    public struct Color3 : IEquatable<Color3>, IFormattable
    {
        //
        // Summary:
        //     The red component of this Color4 structure.
        public float R;

        //
        // Summary:
        //     The green component of this Color4 structure.
        public float G;

        //
        // Summary:
        //     The blue component of this Color4 structure.
        public float B;

        //
        // Summary:
        //     Initializes a new instance of the Color3 struct.
        //
        // Parameters:
        //   r:
        //     The red component of the new Color3 structure.
        //
        //   g:
        //     The green component of the new Color3 structure.
        //
        //   b:
        //     The blue component of the new Color3 structure.
        public Color3(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        //
        // Summary:
        //     Converts this integer representation with 8 bits per channel to a color.
        //
        // Parameters:
        //     A System.Int32 that represents an 8-bit RGB color.
        //
        // Remarks:
        //     This method is intended for compatibility with OpenGL texture formats and System.Drawing.
        //     It extracts the color information from the bits that are packed into the int.
        public static implicit operator Color3(int c)
        {
            return new Color3((c & 0x0000ff) / 255.0f, ((c & 0x00ff00) >> 8) / 255.0f, ((c & 0xff0000) >> 16) / 255.0f);
        }

        //
        // Summary:
        //     Converts this color to an integer representation with 8 bits per channel.
        //
        // Returns:
        //     A System.Int32 that represents this instance.
        //
        // Remarks:
        //     This method is intended for compatibility with OpenGL texture formats and System.Drawing.
        //     It compresses the color into 8 bits per channel, which means color information is lost.
        public readonly int ToInt()
        {
            Color3 c = Clamp(this);
            return (int)(((uint)(c.R * 255f) << 16) | ((uint)(c.G * 255f) << 8) | (uint)(c.B * 255f));
        }

        //
        // Summary:
        //     Compares the specified Color3 structures for equality.
        //
        // Parameters:
        //   left:
        //     The left-hand side of the comparison.
        //
        //   right:
        //     The right-hand side of the comparison.
        //
        // Returns:
        //     True if left is equal to right; false otherwise.
        public static bool operator ==(Color3 left, Color3 right)
        {
            return left.Equals(right);
        }

        //
        // Summary:
        //     Compares the specified Color3 structures for inequality.
        //
        // Parameters:
        //   left:
        //     The left-hand side of the comparison.
        //
        //   right:
        //     The right-hand side of the comparison.
        //
        // Returns:
        //     True if left is not equal to right; false otherwise.
        public static bool operator !=(Color3 left, Color3 right)
        {
            return !left.Equals(right);
        }

        //
        // Summary:
        //     Converts the specified OpenTK.Mathematics.Color4 to a Color3 structure.
        //
        // Parameters:
        //   color:
        //     The OpenTK.Mathematics.Color4 to convert. The alpha channel will be ignored.
        //
        // Returns:
        //     A new Color3 structure containing the converted components.
        public static implicit operator Color3(Color4 color)
        {
            return new Color3(color.R, color.G, color.B);
        }

        //
        // Summary:
        //     Returns this Color3 as a Vector3. The resulting struct will have XYZ mapped
        //     to RGB, in that order.
        //
        // Parameters:
        //   c:
        //     The Color3 to convert.
        //
        // Returns:
        //     The Color3, converted into a Vector3.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector3(Color3 c)
        {
            return Unsafe.As<Color3, Vector3>(ref c);
        }

        //
        // Summary:
        //     Compares whether this Color3 structure is equal to the specified object.
        //
        // Parameters:
        //   obj:
        //     An object to compare to.
        //
        // Returns:
        //     True obj is a Color3 structure with the same components as this Color3; false
        //     otherwise.
        public override readonly bool Equals(object? obj)
        {
            if (obj is Color3 c)
            {
                return Equals(c);
            }

            return false;
        }

        //
        // Summary:
        //     Calculates the hash code for this Color3 structure.
        //
        // Returns:
        //     A System.Int32 containing the hashcode of this Color3 structure.
        public override readonly int GetHashCode()
        {
            return HashCode.Combine(R, G, B);
        }

        //
        // Summary:
        //     Creates a System.String that describes this Color3 structure.
        //
        // Returns:
        //     A System.String that describes this Color3 structure.
        public override readonly string ToString()
        {
            return ToString(null, null);
        }

        public readonly string ToString(string format)
        {
            return ToString(format, null);
        }

        public readonly string ToString(IFormatProvider formatProvider)
        {
            return ToString(null, formatProvider);
        }

        public readonly string ToString(string? format, IFormatProvider? formatProvider)
        {
            string listSeparator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

            if (formatProvider is CultureInfo cultureInfo)
            {
                listSeparator = cultureInfo.TextInfo.ListSeparator;
            }

            if (formatProvider?.GetFormat(typeof(TextInfo)) is TextInfo textInfo)
            {
                listSeparator = textInfo.ListSeparator;
            }

            string textR = R.ToString(format, formatProvider);
            string textG = G.ToString(format, formatProvider);
            string textB = B.ToString(format, formatProvider);
            return "(" + textR + listSeparator + " " + textG + listSeparator + " " + textB + ")";
        }

        //
        // Summary:
        //     Compares whether this Color3 structure is equal to the specified Color3.
        //
        // Parameters:
        //   other:
        //     The Color3 structure to compare to.
        //
        // Returns:
        //     True if both Color3 structures contain the same components; false otherwise.
        public readonly bool Equals(Color3 other)
        {
            return R == other.R && G == other.G && B == other.B;
        }

        //
        // Summary:
        //     Adds two colors.
        //
        // Parameters:
        //   a:
        //     Left operand.
        //
        //   b:
        //     Right operand.
        //
        // Returns:
        //     Result of operation.
        public static Color3 Add(Color3 a, Color3 b)
        {
            Add(in a, in b, out a);
            return a;
        }

        //
        // Summary:
        //     Adds two colors.
        //
        // Parameters:
        //   a:
        //     Left operand.
        //
        //   b:
        //     Right operand.
        //
        //   result:
        //     Result of operation.
        public static void Add(in Color3 a, in Color3 b, out Color3 result)
        {
            result.R = a.R + b.R;
            result.G = a.G + b.G;
            result.B = a.B + b.B;
        }

        //
        // Summary:
        //     Subtract one color from another.
        //
        // Parameters:
        //   a:
        //     First operand.
        //
        //   b:
        //     Second operand.
        //
        // Returns:
        //     Result of subtraction.
        public static Color3 Subtract(Color3 a, Color3 b)
        {
            Subtract(in a, in b, out a);
            return a;
        }

        //
        // Summary:
        //     Subtract one color from another.
        //
        // Parameters:
        //   a:
        //     First operand.
        //
        //   b:
        //     Second operand.
        //
        //   result:
        //     Result of subtraction.
        public static void Subtract(in Color3 a, in Color3 b, out Color3 result)
        {
            result.R = a.R - b.R;
            result.G = a.G - b.G;
            result.B = a.B - b.B;
        }

        //
        // Summary:
        //     Multiplies a color by a scalar.
        //
        // Parameters:
        //   color:
        //     Left operand.
        //
        //   _scale:
        //     Right operand.
        //
        // Returns:
        //     Result of the operation.
        public static Color3 Multiply(Color3 color, float scale)
        {
            Multiply(in color, scale, out color);
            return color;
        }

        //
        // Summary:
        //     Multiplies a color by a scalar.
        //
        // Parameters:
        //   color:
        //     Left operand.
        //
        //   _scale:
        //     Right operand.
        //
        //   result:
        //     Result of the operation.
        public static void Multiply(in Color3 color, float scale, out Color3 result)
        {
            result.R = color.R * scale;
            result.G = color.G * scale;
            result.B = color.B * scale;
        }

        //
        // Summary:
        //     Multiplies two colors.
        //
        // Parameters:
        //   a:
        //     Left operand.
        //
        //   b:
        //     Right operand.
        //
        // Returns:
        //     Result of the operation.
        public static Color3 Multiply(Color3 a, Color3 b)
        {
            Multiply(in a, in b, out a);
            return a;
        }

        //
        // Summary:
        //     Multiplies two colors.
        //
        // Parameters:
        //   a:
        //     Left operand.
        //
        //   b:
        //     Right operand.
        //
        //   result:
        //     Result of the operation.
        public static void Multiply(in Color3 a, in Color3 b, out Color3 result)
        {
            result.R = a.R * b.R;
            result.G = a.G * b.G;
            result.B = a.B * b.B;
        }

        //
        // Summary:
        //     Divides a color by a scalar.
        //
        // Parameters:
        //   color:
        //     Left operand.
        //
        //   _scale:
        //     Right operand.
        //
        // Returns:
        //     Result of the operation.
        public static Color3 Divide(Color3 color, float scale)
        {
            Divide(in color, scale, out color);
            return color;
        }

        //
        // Summary:
        //     Divides a color by a scalar.
        //
        // Parameters:
        //   color:
        //     Left operand.
        //
        //   _scale:
        //     Right operand.
        //
        //   result:
        //     Result of the operation.
        public static void Divide(in Color3 color, float scale, out Color3 result)
        {
            result.R = color.R / scale;
            result.G = color.G / scale;
            result.B = color.B / scale;
        }

        //
        // Summary:
        //     Clamp a color to the given minimum and maximum colors.
        //
        // Parameters:
        //   color:
        //     Input color.
        //
        //   min:
        //     Minimum color.
        //
        //   max:
        //     Maximum color.
        //
        // Returns:
        //     The clamped color.
        public static Color3 Clamp(Color3 color, Color3 min, Color3 max)
        {
            color.R = (color.R < min.R) ? min.R : ((color.R > max.R) ? max.R : color.R);
            color.G = (color.G < min.G) ? min.G : ((color.G > max.G) ? max.G : color.G);
            color.B = (color.B < min.B) ? min.B : ((color.B > max.B) ? max.B : color.B);
            return color;
        }

        //
        // Summary:
        //     Clamp a color to the given minimum and maximum colors.
        //
        // Parameters:
        //   color:
        //     Input color.
        //
        //   min:
        //     Minimum color.
        //
        //   max:
        //     Maximum color.
        //
        //   result:
        //     The clamped color.
        public static void Clamp(in Color3 color, in Color3 min, in Color3 max, out Color3 result)
        {
            result.R = (color.R < min.R) ? min.R : ((color.R > max.R) ? max.R : color.R);
            result.G = (color.G < min.G) ? min.G : ((color.G > max.G) ? max.G : color.G);
            result.B = (color.B < min.B) ? min.B : ((color.B > max.B) ? max.B : color.B);
        }

        //
        // Summary:
        //     Clamp a color between 0 and 1.
        //
        // Parameters:
        //   color:
        //     Input color.
        //
        // Returns:
        //     The clamped color.
        public static Color3 Clamp(Color3 color)
        {
            return Clamp(color, Color4.Black, Color4.White);
        }

        //
        // Summary:
        //     Clamp a color between 0 and 1.
        //
        // Parameters:
        //   color:
        //     Input color.
        //
        //   result:
        //     The clamped color.
        public static void Clamp(in Color3 color, out Color3 result)
        {
            Clamp(color, Color4.Black, Color4.White, out result);
        }

        //
        // Summary:
        //     Returns a new color that is the linear blend of the 2 given colors.
        //
        // Parameters:
        //   a:
        //     First input color.
        //
        //   b:
        //     Second input color.
        //
        //   blend:
        //     The blend factor.
        //
        // Returns:
        //     a when blend=0, b when blend=1, and a linear combination otherwise.
        public static Color3 Lerp(Color3 a, Color3 b, float blend)
        {
            a.R = (blend * (b.R - a.R)) + a.R;
            a.G = (blend * (b.G - a.G)) + a.G;
            a.B = (blend * (b.B - a.B)) + a.B;
            return a;
        }

        //
        // Summary:
        //     Returns a new color that is the linear blend of the 2 given colors.
        //
        // Parameters:
        //   a:
        //     First input color.
        //
        //   b:
        //     Second input color.
        //
        //   blend:
        //     The blend factor.
        //
        //   result:
        //     a when blend=0, b when blend=1, and a linear combination otherwise.
        public static void Lerp(in Color3 a, in Color3 b, float blend, out Color3 result)
        {
            result.R = (blend * (b.R - a.R)) + a.R;
            result.G = (blend * (b.G - a.G)) + a.G;
            result.B = (blend * (b.B - a.B)) + a.B;
        }

        //
        // Summary:
        //     Returns a new color that is the component-wise linear blend of the 2 given colors.
        //
        //
        // Parameters:
        //   a:
        //     First input color.
        //
        //   b:
        //     Second input color.
        //
        //   blend:
        //     The blend factor.
        //
        // Returns:
        //     a when blend=0, b when blend=1, and a component-wise linear combination otherwise.
        public static Color3 Lerp(Color3 a, Color3 b, Color3 blend)
        {
            a.R = (blend.R * (b.R - a.R)) + a.R;
            a.G = (blend.G * (b.G - a.G)) + a.G;
            a.B = (blend.B * (b.B - a.B)) + a.B;
            return a;
        }

        //
        // Summary:
        //     Returns a new color that is the component-wise linear blend of the 2 given colors.
        //
        //
        // Parameters:
        //   a:
        //     First input color.
        //
        //   b:
        //     Second input color.
        //
        //   blend:
        //     The blend factor.
        //
        //   result:
        //     a when blend=0, b when blend=1, and a component-wise linear combination otherwise.
        public static void Lerp(in Color3 a, in Color3 b, Color3 blend, out Color3 result)
        {
            result.R = (blend.R * (b.R - a.R)) + a.R;
            result.G = (blend.G * (b.G - a.G)) + a.G;
            result.B = (blend.B * (b.B - a.B)) + a.B;
        }

        //
        // Summary:
        //     Interpolate 3 colors using Barycentric coordinates.
        //
        // Parameters:
        //   a:
        //     First input color.
        //
        //   b:
        //     Second input color.
        //
        //   c:
        //     Third input color.
        //
        //   u:
        //     First Barycentric Coordinate.
        //
        //   v:
        //     Second Barycentric Coordinate.
        //
        // Returns:
        //     a when u=v=0, b when u=1,v=0, c when u=0,v=1, and a linear combination of a,b,c
        //     otherwise.
        public static Color3 BaryCentric(Color3 a, Color3 b, Color3 c, float u, float v)
        {
            BaryCentric(in a, in b, in c, u, v, out var result);
            return result;
        }

        //
        // Summary:
        //     Interpolate 3 colors using Barycentric coordinates.
        //
        // Parameters:
        //   a:
        //     First input color.
        //
        //   b:
        //     Second input color.
        //
        //   c:
        //     Third input color.
        //
        //   u:
        //     First Barycentric Coordinate.
        //
        //   v:
        //     Second Barycentric Coordinate.
        //
        //   result:
        //     Output Vector. a when u=v=0, b when u=1,v=0, c when u=0,v=1, and a linear combination
        //     of a,b,c otherwise.
        public static void BaryCentric(in Color3 a, in Color3 b, in Color3 c, float u, float v, out Color3 result)
        {
            Subtract(in b, in a, out var result2);
            Multiply(in result2, u, out var result3);
            Add(in a, in result3, out var result4);
            Subtract(in c, in a, out var result5);
            Multiply(in result5, v, out var result6);
            Add(in result4, in result6, out result);
        }

        //
        // Summary:
        //     Adds two instances.
        //
        // Parameters:
        //   left:
        //     The first instance.
        //
        //   right:
        //     The second instance.
        //
        // Returns:
        //     The result of the calculation.
        public static Color3 operator +(Color3 left, Color3 right)
        {
            left.R += right.R;
            left.G += right.G;
            left.B += right.B;
            return left;
        }

        //
        // Summary:
        //     Subtracts two instances.
        //
        // Parameters:
        //   left:
        //     The first instance.
        //
        //   right:
        //     The second instance.
        //
        // Returns:
        //     The result of the calculation.
        public static Color3 operator -(Color3 left, Color3 right)
        {
            left.R -= right.R;
            left.G -= right.G;
            left.B -= right.B;
            return left;
        }

        //
        // Summary:
        //     Negates an instance.
        //
        // Parameters:
        //   color:
        //     The instance.
        //
        // Returns:
        //     The result of the calculation.
        public static Color3 operator -(Color3 color)
        {
            color.R = 0f - color.R;
            color.G = 0f - color.G;
            color.B = 0f - color.B;
            return color;
        }

        //
        // Summary:
        //     Multiplies an instance by a scalar.
        //
        // Parameters:
        //   color:
        //     The instance.
        //
        //   _scale:
        //     The scalar.
        //
        // Returns:
        //     The result of the calculation.
        public static Color3 operator *(Color3 color, float scale)
        {
            color.R *= scale;
            color.G *= scale;
            color.B *= scale;
            return color;
        }

        //
        // Summary:
        //     Multiplies an instance by a scalar.
        //
        // Parameters:
        //   _scale:
        //     The scalar.
        //
        //   color:
        //     The instance.
        //
        // Returns:
        //     The result of the calculation.
        public static Color3 operator *(float scale, Color3 color)
        {
            color.R *= scale;
            color.G *= scale;
            color.B *= scale;
            return color;
        }

        //
        // Summary:
        //     Component-wise multiplication between the specified instance by a _scale color.
        //
        //
        // Parameters:
        //   _scale:
        //     Left operand.
        //
        //   color:
        //     Right operand.
        //
        // Returns:
        //     Result of multiplication.
        public static Color3 operator *(Color3 color, Color3 scale)
        {
            color.R *= scale.R;
            color.G *= scale.G;
            color.B *= scale.B;
            return color;
        }

        //
        // Summary:
        //     Divides an instance by a scalar.
        //
        // Parameters:
        //   color:
        //     The instance.
        //
        //   _scale:
        //     The scalar.
        //
        // Returns:
        //     The result of the calculation.
        public static Color3 operator /(Color3 color, float scale)
        {
            color.R /= scale;
            color.G /= scale;
            color.B /= scale;
            return color;
        }

        public static implicit operator Color3((float R, float G, float B) values)
        {
            return new Color3(values.R, values.G, values.B);
        }
    }
}
