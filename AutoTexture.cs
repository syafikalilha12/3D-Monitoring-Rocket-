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
using System.Drawing.Imaging;
using System.Diagnostics;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Gosub
{
	//-----------------------------------------------------------------------
	// AutoTexture - A class to manage textures
	//-----------------------------------------------------------------------
	class AutoTexture
	{
		Texture mTexture;
		Direct3d mD3d;
		List<TextureData> mTextureData;
		SurfaceDescription mSurfDescription;
	
		class TextureData
		{
			public byte					[]Data;
			public SurfaceDescription	Description;
		}

		

		/// <summary>
		/// Unused by Direct3d class.  You can use this field
		/// to store miscellaneous info with the texture.
		/// </summary>
		public object Tag;

		/// <summary>
		/// Returns information about level 0 surface
		/// </summary>
		public SurfaceDescription Description
		{
			get { return mSurfDescription; }
		}

		public int Width { get { return mSurfDescription.Width; } }
		public int Height { get { return mSurfDescription.Height; } }

		/// <summary>
		/// Return the mesh (or null if the device is lost)
		/// </summary>
		public Texture DxTexture { get { return mTexture; } }		

		/// <summary>
		/// Return the Direct3d object for this object
		/// </summary>
		public Direct3d D3d { get { return mD3d; } }

		/// <summary>
		/// Create an AutoTexture
		/// </summary>
		public AutoTexture(Direct3d d3d, Texture texture)
		{
			mD3d = d3d;
			d3d.DxDebugNumAutoTextures++;
			mTexture = texture;
			mSurfDescription = mTexture.GetLevelDescription(0);
			d3d.DxLost += new Direct3d.DxDirect3dDelegate(d3d_DxLost);
			d3d.DxRestore += new Direct3d.DxDirect3dDelegate(d3d_DxRestore);
		}

		/// <summary>
		/// Dispose this object and the texture it holds
		/// </summary>
		public void Dispose()
		{
			if (mTexture != null)
			{
				mTexture.Dispose();
				mTexture = null;
			}
			if (mD3d != null)
			{
				mD3d.DxDebugNumAutoTextures--;
				mD3d.DxLost -= new Direct3d.DxDirect3dDelegate(d3d_DxLost);
				mD3d.DxRestore -= new Direct3d.DxDirect3dDelegate(d3d_DxRestore);
				mD3d = null;
			}
			mTextureData = null;
		}

		/// <summary>
		/// The device must not be lost when this function is called.  This clone function
		/// can copy textures to different DirectX devices and also change the format.
		/// </summary>
		public AutoTexture Clone(Direct3d d3d, Format format, Usage usage, Pool pool)
		{
			// Copy the texture
			Texture toTexture = new Texture(d3d.Dx, mSurfDescription.Width, mSurfDescription.Height, 
											1, usage, format, pool);
			
			Surface toSurface = toTexture.GetSurfaceLevel(0);
			Surface fromSurface = mTexture.GetSurfaceLevel(0);			
			SurfaceLoader.FromSurface(toSurface, fromSurface, Filter.Point, 0);
			toSurface.Dispose();
			fromSurface.Dispose();

			// Copy this AutoTexture
			AutoTexture autoTexture = new AutoTexture(d3d, toTexture);
			autoTexture.Tag = Tag;
			return autoTexture;
		}

		/// <summary>
		/// The device must not be lost when this function is called.
		/// </summary>
		/// <returns></returns>
		public AutoTexture Clone()
		{
			return Clone(mD3d, mSurfDescription.Format, mSurfDescription.Usage, mSurfDescription.Pool);
		}

		/// <summary>
		/// Save the mesh when the DirectX device is lost
		/// </summary>
		void d3d_DxLost(Direct3d d3d, Device dx)
		{
			if (mTexture == null)
				return;

			// Capture all texture levels
			mTextureData = new List<TextureData>();
			for (int level = 0;  level < mTexture.LevelCount;  level++)
			{
				int pitch;
				TextureData data = new TextureData();
				data.Description = mTexture.GetLevelDescription(level);
				data.Data = (byte [])mTexture.LockRectangle(typeof(byte), level, LockFlags.ReadOnly,
					out pitch, PixelSizeBits(data.Description.Format)
								* data.Description.Width * data.Description.Height / 8);
				mTexture.UnlockRectangle(level);
				mTextureData.Add(data);
			}
			mTexture.Dispose();
			mTexture = null;
		}

		/// <summary>
		/// Restore the mesh when the DirectX device is restored
		/// </summary>
		void d3d_DxRestore(Direct3d d3d, Device dx)
		{
			// If the direct3d device wasn't lost in the first place, don't restore it.
			// This happens the first timeMs around.
			if (mTexture != null)
				return;

			// Create mesh
			int width = mSurfDescription.Width;
			int height = mSurfDescription.Height;
			mTexture = new Texture(d3d.Dx, width, height,
									mTextureData.Count, mSurfDescription.Usage, mSurfDescription.Format, mSurfDescription.Pool);

			// Restore all texture levels
			for (int level = 0;  level < mTextureData.Count;  level++)
			{
				int pitch;
				GraphicsStream stream = mTexture.LockRectangle(level, LockFlags.Discard, out pitch);
				stream.Write(mTextureData[level].Data);
				mTexture.UnlockRectangle(level);
			}
			// Write the texture data
			mTextureData = null;
		}


		int PixelSizeBits(Format format)
		{
			// Isn't there a better way to do this?
			int pixelSizeBits = 8;
			switch (format)
			{
				case Format.Dxt1:
					Debug.Assert(false); // Strange format
					pixelSizeBits = 4;
					break;
					
				case Format.A8:				
				case Format.L8:				
				case Format.P8:				
					pixelSizeBits = 8;
					break;
								
				case Format.A4L4:
				case Format.Dxt2:
				case Format.Dxt3:
				case Format.Dxt4:
				case Format.Dxt5:
					pixelSizeBits = 8;
					Debug.Assert(false);
					break;
				

				case Format.A8R3G3B2:
				case Format.A4R4G4B4:
				case Format.A1R5G5B5:
					pixelSizeBits = 16;
					break;
					
				case Format.G8R8G8B8:
				case Format.A8R8G8B8:
				case Format.X8R8G8B8:
				case Format.A8B8G8R8:
				case Format.X8B8G8R8:
					pixelSizeBits = 32;
					break;
				default:
					// Insert your vertexFormat above
					pixelSizeBits = 32;
					Debug.Assert(false);
					break;
			}
			return pixelSizeBits;
		}

		/// <summary>
		/// Convert a gray bitmap to white wite with alpha color
		/// </summary>
		public void SetAlphaConstant(int alpha)
		{
			SurfaceDescription description = mTexture.GetLevelDescription(0);
			if (description.Format != Format.A8R8G8B8)
				throw new Direct3dException("SetAlphaConstant: Invalid pixel format (A8R8G8B8 required)");
				
			// Generate an alphamap
			int width = description.Width;
			int height = description.Height;
			int pitch;
			Color32 []bm = (Color32[])mTexture.LockRectangle(
											typeof(Color32), 0, 0, out pitch, width*height);
			
			for (int i = 0;  i < bm.Length;  i++)
				bm[i] = new Color32(alpha, bm[i]);
			mTexture.UnlockRectangle(0);	
		}

		/// <summary>
		/// Convert a gray bitmap to white with alpha color
		/// </summary>
		public void SetAlphaFromGray()
		{
			SurfaceDescription description = mTexture.GetLevelDescription(0);
			if (description.Format != Format.A8R8G8B8)
				throw new Direct3dException("SetAlphaFromGray: Invalid pixel format (A8R8G8B8 required)");

			// Generate an alphamap
			int width = description.Width;
			int height = description.Height;
			int pitch;
			Color32 []bm = (Color32[])mTexture.LockRectangle(
											typeof(Color32), 0, 0, out pitch, width*height);
			
			for (int i = 0;  i < bm.Length;  i++)
			{
				Color32 color = bm[i];
				int alpha = (color.R + color.G + color.B)/3;
				bm[i] = new Color32(alpha, new Color32(255, 255, 255));
			}
			mTexture.UnlockRectangle(0);	
		}

		/// <summary>
		/// Reset texture color (leave apha channel alone)
		/// </summary>
		public void SetAlpha(int alpha)
		{
			SurfaceDescription description = mTexture.GetLevelDescription(0);
			if (description.Format != Format.A8R8G8B8)
				throw new Direct3dException("SetAlphaColor: Invalid pixel format (A8R8G8B8 required)");

			// Generate an alphamap
			int width = description.Width;
			int height = description.Height;
			int pitch;
			Color32 []bm = (Color32[])mTexture.LockRectangle(typeof(Color32),
											0, LockFlags.Discard, out pitch, width*height);

			for (int i = 0; i < bm.Length; i++)
				bm[i] = new Color32(alpha, bm[i]);
			mTexture.UnlockRectangle(0);
		}


		/// <summary>
		/// Reset texture color (leave apha channel alone)
		/// </summary>
		public void SetAlphaColor(Color32 color)
		{
			SurfaceDescription description = mTexture.GetLevelDescription(0);
			if (description.Format != Format.A8R8G8B8)
				throw new Direct3dException("SetAlphaColor: Invalid pixel format (A8R8G8B8 required)");

			// Generate an alphamap
			int width = description.Width;
			int height = description.Height;
			int pitch;
			Color32 []bm = (Color32[])mTexture.LockRectangle(typeof(Color32), 
											0, LockFlags.Discard, out pitch, width*height);
			
			for (int i = 0;  i < bm.Length;  i++)
				bm[i] = new Color32(bm[i].A, color);
			mTexture.UnlockRectangle(0);				
		}
		
		/// <summary>
		/// Generate an alpha map for the texture.  min/max color and alpha parameters are 0..255.
		/// Typically min/max alpha will be (0, 255), and min/max color will be a small range
		/// like (16, 32) to create a soft edge.
		/// </summary>
		public void SetAlphaFade(int minAlpha, int maxAlpha, int minColor, int maxColor)
		{
			SurfaceDescription description = mTexture.GetLevelDescription(0);
			if (description.Format != Format.A8R8G8B8)
				throw new Direct3dException("SetAlphaColor: Invalid pixel format (A8R8G8B8 required)");

			// Generate an alphamap
			int width = description.Width;
			int height = description.Height;
			int pitch;
			Color32 []bm = (Color32[])mTexture.LockRectangle(typeof(Color32), 
															0, 0, out pitch, width*height);
			
			// Multiply by 3 because alpha calculation is R+G+B
			minColor *= 3;
			maxColor *= 3;
			
			for (int i = 0;  i < bm.Length;  i++)
			{
				Color32 color = bm[i];
				
				int colorSum = color.R + color.G + color.B; // alpha = color*3
				
				// Calculate alpha
				int alpha;
				if (colorSum >= maxColor)
					alpha = maxAlpha;
				else if (colorSum <= minColor)
					alpha = minAlpha;
				else
				{
					// Convert colorSum from (minColor..maxColor) to (minApha..maxAlpha)
					alpha = (colorSum-minColor)*(maxAlpha-minAlpha)/(maxColor-minColor)  + minAlpha;
				}
				
				bm[i] = new Color32(alpha, color);
			}
			mTexture.UnlockRectangle(0);	
		}

		/// <summary>
		/// Make a copy of the bitmap in 32bppArgb.  
		/// NOTE: Bitmap.Clone doesn't always work.  This function always makes a true copy.
		/// </summary>
		static public Bitmap CopyBitmapTo32Bpp(Bitmap bitmap)
		{
			// Since Bitmap.Clone doesn't always work, we're going to force a "real" copy.
			Bitmap newBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
			Graphics gr = Graphics.FromImage(newBitmap);
			gr.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
			gr.Dispose();
			return newBitmap;
		}

	}
}
