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
	// AutoIndexBuffer - a class to manage index buffers 
	//-----------------------------------------------------------------------
	class AutoIndexBuffer
	{
		IndexBuffer mIndexBuffer;
		Direct3d mD3d;
		byte[] mIndexData;
		int mNumIndices;
		Usage mUsage;
		Pool mPool;

		/// <summary>
		/// Return the pathIndex buffer (or null when the DirectX device is lost)
		/// </summary>
		public IndexBuffer IB { get { return mIndexBuffer; } }
				
		/// <summary>
		/// Return the Direct3d object for this object
		/// </summary>
		public Direct3d D3d { get { return mD3d; } }

		/// <summary>
		/// Number of indices
		/// </summary>
		public int NumIndices { get { return mNumIndices; } }

		/// <summary>
		/// Create an AutoVertexBuffer (16 bits, ushort)
		/// </summary>
		public AutoIndexBuffer(Direct3d d3d, int numIndices, Usage usage, Pool pool)
		{
			mD3d = d3d;
			d3d.DxDebugNumAutoIndexBuffers++;
			mIndexBuffer = new IndexBuffer(typeof(ushort), numIndices, d3d.Dx, usage, pool);
			mNumIndices = numIndices;
			mUsage = usage;
			mPool = pool;

			d3d.DxLost += new Direct3d.DxDirect3dDelegate(d3d_DxLost);
			d3d.DxRestore += new Direct3d.DxDirect3dDelegate(d3d_DxRestore);
		}

		/// <summary>
		/// Create an AutoVertexBuffer initialized with the indices in buffer
		/// </summary>
		/// <param name="d3d"></param>
		/// <param name="buffer"></param>
		/// <param name="usage"></param>
		/// <param name="pool"></param>
		public AutoIndexBuffer(Direct3d d3d, ushort []buffer, Usage usage, Pool pool)
		{
			mD3d = d3d;
			d3d.DxDebugNumAutoIndexBuffers++;
			mIndexBuffer = new IndexBuffer(typeof(ushort), buffer.Length, d3d.Dx, usage, pool);
			mNumIndices = buffer.Length;
			mUsage = usage;
			mPool = pool;
			SetIndices(buffer);

			d3d.DxLost += new Direct3d.DxDirect3dDelegate(d3d_DxLost);
			d3d.DxRestore += new Direct3d.DxDirect3dDelegate(d3d_DxRestore);			
		}

		/// <summary>
		/// Set the pathIndex buffer
		/// </summary>
		/// <param name="indices"></param>
		public void SetIndices(ushort []indices)
		{
			ushort []buffer = (ushort[])mIndexBuffer.Lock(0, typeof(ushort), LockFlags.Discard, mNumIndices);
			indices.CopyTo(buffer, 0);
			mIndexBuffer.Unlock();
		}

		/// <summary>
		/// Gets the pathIndex buffer
		/// </summary>
		public ushort []GetIndices()
		{
			ushort []buffer = (ushort[])mIndexBuffer.Lock(0, typeof(ushort), LockFlags.Discard, mNumIndices);
			mIndexBuffer.Unlock();
			return buffer;
		}

		/// <summary>
		/// Dispose this object and the pathIndex buffer it holds
		/// </summary>
		public void Dispose()
		{
			if (mIndexBuffer != null)
			{
				mIndexBuffer.Dispose();
				mIndexBuffer = null;
			}
			if (mD3d != null)
			{
				mD3d.DxDebugNumAutoIndexBuffers--;
				mD3d.DxLost -= new Direct3d.DxDirect3dDelegate(d3d_DxLost);
				mD3d.DxRestore -= new Direct3d.DxDirect3dDelegate(d3d_DxRestore);
				mD3d = null;
			}
			mIndexBuffer = null;
		}

		/// <summary>
		/// Save the vertex buffer when the DirectX device is lost
		/// </summary>
		void d3d_DxLost(Direct3d d3d, Device dx)
		{
			if (mIndexBuffer == null)
				return;

			mIndexData = (byte[])mIndexBuffer.Lock(0, typeof(byte), LockFlags.ReadOnly, mIndexBuffer.Description.Size);
			mIndexBuffer.Unlock();
			mIndexBuffer.Dispose();
			mIndexBuffer = null;
		}

		/// <summary>
		/// Restore the vertex buffer when the DirectX device is restored
		/// </summary>
		void d3d_DxRestore(Direct3d d3d, Device dx)
		{
			// If the direct3d device wasn't lost in the first place, don't restore it.
			// This happens the first timeMs around.
			if (mIndexBuffer != null)
				return;

			mIndexBuffer = new IndexBuffer(typeof(ushort), mNumIndices, d3d.Dx, mUsage, mPool);
			byte []data = (byte[])mIndexBuffer.Lock(0, typeof(byte), LockFlags.Discard, mIndexBuffer.Description.Size);
			mIndexData.CopyTo(data, 0);
			mIndexBuffer.Unlock();
			mIndexData = null;
		}
	}
}
