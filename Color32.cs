/*
Direct3DControl (version 11)
Copyright (C) 2015 by Jeremy Spiller.  All rights reserved.

Redistribution and use in source and binary forms, with or without 
modification, are permitted provided that the following conditions are met:

   1. Redistributions of source code must retain the above copyright 
      notice, this list of conditions and the following disclaimer.
   2. Redistributions in binary form must reproduce the above copyright 
      notice, this list of conditions and the following disclaimer in 
      the documentation and/or other materials provided with the distribution.
 
THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Gosub
{
	/// <summary>
	/// Light weight wrapper for a color (32 bits)
	/// </summary>
	struct Color32
	{
		int mArgb;

		/// <summary>
		/// Convert from an int to a color
		/// </summary>
		public Color32(int argb)
		{
			mArgb = argb;
		}
		
		/// <summary>
		/// Convert RGB (0..255) to a color
		/// </summary>
		public Color32(int red, int green, int blue)
		{
			mArgb = (red << 16) | (green << 8) | blue | (0xFF << 24);
		}
		
		/// <summary>
		/// Convert alpha and RGB (0..255) to a color
		/// </summary>
		public Color32(int alpha, int red, int green, int blue)
		{
			mArgb = (alpha << 24) | (red << 16) | (green << 8) | blue;
		}
		
		/// <summary>
		/// Copy a color
		/// </summary>
		public Color32(Color32 color)
		{
			mArgb = color.mArgb;
		}
		
		/// <summary>
		/// Calculate a new alpha (0..255) for the color
		/// </summary>
		public Color32(int alpha, Color32 colorBase)
		{
			mArgb = (colorBase.mArgb & 0xFFFFFF) | (alpha << 24);
		}
		
		/// <summary>
		/// Convert RGB (0..1) to a color
		/// </summary>
		public Color32(float red, float green, float blue)
		{
			red = red * 256;
			red = Math.Min(255.5f, Math.Max(0, red));
			green = green * 256;
			green = Math.Min(255.5f, Math.Max(0, green));
			blue = blue * 256;
			blue = Math.Min(255.5f, Math.Max(0, blue));
			mArgb = new Color32((int)red, (int)green, (int)blue);
		}
		
		/// <summary>
		/// Convert alpha and RGB (0..1) to a color
		/// </summary>
		public Color32(float alpha, float red, float green, float blue)
		{
			red = red * 256;
			red = Math.Min(255.5f, Math.Max(0, red));
			green = green * 256;
			green = Math.Min(255.5f, Math.Max(0, green));
			blue = blue * 256;
			blue = Math.Min(255.5f, Math.Max(0, blue));
			alpha = alpha * 256;
			alpha = Math.Min(255.5f, Math.Max(0, alpha));
			mArgb = new Color32((int)alpha, (int)red, (int)green, (int)blue);
		}

		/// <summary>
		/// Convert to string representation
		/// </summary>
		public override string ToString()
		{
			return "" + R + ", " + G + ", " + B + " (" + A + ")";
		}
		
		/// <summary>
		/// Fade from one color to another (percent is 0..1, 0 = source color, 1 = to color)
		/// </summary>
		public Color32 Fade(Color32 to, float percent)
		{
			return Fade(to, (int)(percent*256f));
		}
		
		/// <summary>
		/// Fade from one color to another (factor is 0..256, 0 = source color, 256 = to color)
		/// </summary>
		public Color32 Fade(Color32 to, int factor)
		{
			int invAlpha = 256-factor;
			to.mArgb =	((((((mArgb & 0x00FF00FF)*invAlpha))
						+ (((to.mArgb & 0x00FF00FF)*factor))) >> 8) & 0x00FF00FF)
						+ (((((mArgb >> 8) & 0x00FF00FF)*invAlpha)
						+ (((to.mArgb >> 8) & 0x00FF00FF)*factor)) & unchecked((int)0xFF00FF00));
			return to;
		}
		
		/// <summary>
		/// Fade from one color to another (percent is 0..1, 0 = source color, 1 = to color)
		/// </summary>
		public Color32 Fade(Color32 to, float alphaPercent, float colorPercent)
		{
			return Fade(to, (int)(alphaPercent*256f), (int)(colorPercent*256f));
		}
		
		/// <summary>
		/// Fade from one color to another (factor is 0..255, 0 = source color, 255 = to color)
		/// </summary>
		public Color32 Fade(Color32 to, int alphaFactor, int colorFactor)
		{
			int invAlpha = 256-alphaFactor;
			int invColor = 256-colorFactor;
			to.mArgb =	((((mArgb & 0x00FF00FF)*invColor) >> 8) & 0x00FF00FF)
						+ ((((to.mArgb & 0x00FF00FF)*colorFactor) >> 8) & 0x00FF00FF)
						+ ((((mArgb & 0x0000FF00)*invColor) >> 8) & 0x0000FF00)
						+ ((((to.mArgb & 0x0000FF00)*colorFactor) >> 8) & 0x0000FF00)
						+ ((((mArgb >> 8) & 0x00FF00FF)*invAlpha) & unchecked((int)0xFF000000))
						+ ((((to.mArgb >> 8) & 0x00FF00FF)*alphaFactor) & unchecked((int)0xFF000000));
			return to;
		}

		// Return RGB or A component of color (0..255)
		public int A { get { return (mArgb >> 24) & 255; } }
		public int R { get { return (mArgb >> 16) & 255; } }
		public int G { get { return (mArgb >>  8) & 255; } }
		public int B { get { return mArgb & 255; } }

		/// Implicitly convert an integer to a Color32
		public static implicit operator Color32(int color)
		{
			return new Color32(color);
		}

		/// Implicitly convert a Color32 to an int
		public static implicit operator int(Color32 color)
		{
			return color.mArgb;
		}

		/// Implicitly convert a Color to a Color32
		public static implicit operator Color32(Color color)
		{
			return new Color32(color.ToArgb());
		}

		/// Implicitly convert a Color32 to a Color
		public static implicit operator Color(Color32 color)
		{
			return Color.FromArgb(color.mArgb);
		}

		/// <summary>
		/// Implicitly convert a Color32 to a ColorValue
		/// </summary>
		public static implicit operator ColorValue(Color32 color)
		{
			return ColorValue.FromArgb(color.mArgb);
		}

		public static implicit operator Color32(ColorValue color)
		{
			return new Color32(color.ToArgb());
		}

		public static bool operator==(Color32 a, Color32 b)
		{
			return a.mArgb == b.mArgb;
		}

		public static bool operator!=(Color32 a, Color32 b)
		{
			return a.mArgb != b.mArgb;
		}

		public override int GetHashCode()
		{
			return mArgb;
		}

		public override bool Equals(object obj)
		{
			return mArgb == (Color32)obj;
		}
	}	
}
