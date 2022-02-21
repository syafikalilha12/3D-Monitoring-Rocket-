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
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Gosub
{
	//-----------------------------------------------------------------------
	// AutoVertexBuffer - a class to manage vertex buffers 
	//-----------------------------------------------------------------------
	class AutoVertexBuffer
	{
		VertexBuffer mVertexBuffer;
		Direct3d mD3d;

		byte[] mVertexData;
		Type mVertexType;
		int mVertexNumVertices;
		Usage mVertexUsage;
		VertexFormats mVertexFormat;
		Pool mVertexPool;

		/// <summary>
		/// Return the vertex buffer (or null when the DirectX device is lost)
		/// </summary>
		public VertexBuffer VB { get { return mVertexBuffer; } }

		/// <summary>
		/// Return the Direct3d object for this object
		/// </summary>
		public Direct3d D3d { get { return mD3d; } }

		/// <summary>
		/// Number of vertices in the vertex buffer
		/// </summary>
		public int NumVertices { get { return mVertexNumVertices; } }

		/// <summary>
		/// Create an AutoVertexBuffer
		/// </summary>
		public AutoVertexBuffer(Direct3d d3d, Type vertexType, int numVerts,
									Usage usage, VertexFormats format, Pool pool)
		{
			mD3d = d3d;
			d3d.DxDebugNumAutoVertexBuffers++;
			mVertexBuffer = new VertexBuffer(vertexType, numVerts == 0 ? 1 : numVerts, d3d.Dx, usage, format, pool);
			mVertexType = vertexType;
			mVertexNumVertices = numVerts;
			mVertexUsage = usage;
			mVertexFormat = format;
			mVertexPool = pool;
			d3d.DxLost += new Direct3d.DxDirect3dDelegate(d3d_DxLost);
			d3d.DxRestore += new Direct3d.DxDirect3dDelegate(d3d_DxRestore);
		}

		/// <summary>
		/// Dispose this object and the mesh it holds
		/// </summary>
		public void Dispose()
		{
			if (mVertexBuffer != null)
			{
				mVertexBuffer.Dispose();
				mVertexBuffer = null;
			}
			if (mD3d != null)
			{
				mD3d.DxDebugNumAutoVertexBuffers--;
				mD3d.DxLost -= new Direct3d.DxDirect3dDelegate(d3d_DxLost);
				mD3d.DxRestore -= new Direct3d.DxDirect3dDelegate(d3d_DxRestore);
				mD3d = null;
			}
			mVertexData = null;
		}

		/// <summary>
		/// Save the vertex buffer when the DirectX device is lost
		/// </summary>
		void d3d_DxLost(Direct3d d3d, Device dx)
		{
			if (mVertexBuffer == null)
				return;

			mVertexData = (byte[])mVertexBuffer.Lock(0, typeof(byte), LockFlags.ReadOnly,
												 mVertexBuffer.Description.Size);
			mVertexBuffer.Unlock();
			mVertexBuffer.Dispose();
			mVertexBuffer = null;
		}

		/// <summary>
		/// Restore the vertex buffer when the DirectX device is restored
		/// </summary>
		void d3d_DxRestore(Direct3d d3d, Device dx)
		{
			// If the direct3d device wasn't lost in the first place, don't restore it.
			// This happens the first timeMs around.
			if (mVertexBuffer != null)
				return;

			mVertexBuffer = new VertexBuffer(mVertexType, mVertexNumVertices,
									d3d.Dx, mVertexUsage, mVertexFormat, mVertexPool);
			mVertexBuffer.SetData(mVertexData, 0, LockFlags.None);
			mVertexData = null;
		}
	}	
}
