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
using System.Diagnostics;
using System.IO;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;

namespace Gosub
{
	//-----------------------------------------------------------------------
	// AutoMesh - a class to manage meshes
	//-----------------------------------------------------------------------
	class AutoMesh
	{
		Mesh mMesh;
		Direct3d mD3d;

		/// <summary>
		/// Unused by Direct3d class.  You can use this field
		/// to store miscellaneous info with the mesh.
		/// </summary>
		public object Tag;

		/// <summary>
		/// Set this variable to true if the mesh owns the textures it refers
		/// to.  When true, disposing this mesh also disposes the textures.
		/// Defaults to false.
		/// </summary>
		public bool OwnsTextures;

		// Save a copy of all Mesh data
		int mNumFaces;
		int mNumVertices;
		int mNumBytesPerVertex;
		MeshFlags mFlags;
		VertexFormats mVertexFormat;
		byte[] mIndexBufferCopy;
		byte[] mVertexBufferCopy;
		int[] mAttributeBufferCopy;

		// Textures and materials
		ExtendedMaterial []mMaterialsEx = new ExtendedMaterial[0];
		AutoTexture []mTextures = new AutoTexture[0];

		// Cached info (vertices, bounding box, bounding sphere)
		Vector3[] mVertexCache;

		bool    mBoundingBoxValid;
		Vector3 mBoundingBoxMin;
		Vector3 mBoundingBoxMax;

		bool    mSphereValid;
		Vector3 mSphereCenter;
		float   mSphereRadius;

		bool	mSphereMinValid;
		Vector3 mSphereMinCenter;
		float	mSphereMinRadius;

		/// <summary>
		/// Create an automesh.
		/// </summary>
		public AutoMesh(Direct3d d3d, Mesh mesh)
		{
			mD3d = d3d;
			d3d.DxDebugNumAutoMeshes++;
			mMesh = mesh;
			d3d.DxLost += new Direct3d.DxDirect3dDelegate(d3d_DxLost);
			d3d.DxRestore += new Direct3d.DxDirect3dDelegate(d3d_DxRestore);
		}

		/// <summary>
		/// Return the Direct3d object for this object
		/// </summary>
		public Direct3d D3d { get { return mD3d; } }

		/// <summary>
		/// Return the mesh owned by this object (or null if the DirectX device is lost).
		/// NOTE: Setting this property DOES NOT DISPOSE the old mesh.
		/// Don't set this property when the device is lost (set from render is ok)
		/// Be sure to set Materials[] and Textures[] is required.
		/// </summary>
		public Mesh DxMesh 
		{ 
			get 
			{ 
				return mMesh; 
			} 
			set
			{
				// Set mesh, force a recalculation of bounding info (later)
				mMesh = value;
				mBoundingBoxValid = false;
				mSphereValid = false;
				mSphereMinValid = false;
			}
		}

		/// <summary>
		/// Set the mesh (use this function if you know the bounding stuff).
		/// NOTE: Setting this property DOES NOT DISPOSE the old mesh.
		/// Don't call this function when the device is lost (call from render is ok).
		/// Be sure to set Materials[] and Textures[] is required.
		/// </summary>
		public void SetMesh(Mesh mesh, Vector3 center, float radius, Vector3 min, Vector3 max)
		{
			mMesh = mesh;
			mSphereMinValid = true;
			mSphereMinCenter = center;
			mSphereMinRadius = radius;
		    
			mSphereValid = true;
			mSphereCenter = center;
			mSphereRadius = radius;

			mBoundingBoxValid = true;
			mBoundingBoxMin = min;
			mBoundingBoxMax = max;
		}

		/// <summary>
		/// Gets/sets the array of materials (one for each subset to draw)
		/// </summary>
		public ExtendedMaterial[] MaterialsEx
		{
			get { return mMaterialsEx; }
			set { mMaterialsEx = value == null ? new ExtendedMaterial[0] : value; }
		}

		/// <summary>
		/// Gets/sets the array of textures (one for each subset to draw).
		/// Returns an empty array if there are no textures, and any array
		/// element can be null if there is no texture for the given
		/// subset.
		/// </summary>
		public AutoTexture[] Textures
		{
			get { return mTextures; }
			set { mTextures = value == null ? new AutoTexture[0] : value; }
		}

		/// <summary>
		/// Draw all subsets of the mesh with the material and texture for
		/// the given subset.  If no texture/material is specified, the
		/// current one is used.
		/// </summary>
		public void Draw(bool useMeshMaterial)
		{
			if (mMesh == null)
					return;

			mD3d.Dx.VertexFormat = mMesh.VertexFormat;
			if (mTextures.Length == 0)
			{
				mMesh.DrawSubset(0);
			}
			else
				for (int i = 0;  i < mTextures.Length;  i++)
				{
					if (mTextures[i] != null)
						mD3d.Dx.SetTexture(0, mTextures[i].DxTexture);
					if (useMeshMaterial)
						mD3d.Dx.Material = mMaterialsEx[i].Material3D;
					mMesh.DrawSubset(i);
				}
		}

		/// <summary>
		/// Dispose this object and the mesh it holds.  The textures are
		/// also disposed if OwnsTextures is true.
		/// </summary>
		public void Dispose()
		{
			if (mMesh != null)
			{
				mMesh.Dispose();
				mMesh = null;
			}
			if (mD3d != null)
			{
				mD3d.DxDebugNumAutoMeshes--;
				mD3d.DxLost -= new Direct3d.DxDirect3dDelegate(d3d_DxLost);
				mD3d.DxRestore -= new Direct3d.DxDirect3dDelegate(d3d_DxRestore);
				mD3d = null;
			}
			mIndexBufferCopy = null;
			mVertexBufferCopy = null;
			mAttributeBufferCopy = null;
			mVertexCache = null;
			mBoundingBoxValid = false;
			mSphereValid = false;
			mSphereMinValid = false;
			if (OwnsTextures)
				for (int i = 0;  i < mTextures.Length;  i++)
					if (mTextures[i] != null)
						mTextures[i].Dispose();
			mTextures = new AutoTexture[0];
			mMaterialsEx = new ExtendedMaterial[0];
		}

		/// <summary>
		/// Cone the mesh (and textures that it contains), optionally converting
		/// the vertex and texture format.  OwnsTextures is copied to the new 
		/// texture so the cloned textures get disposed.  Extended material file 
		/// names are changed from the file name to the full path.
		/// </summary>
		public AutoMesh Clone(Direct3d d3d, MeshFlags flags, VertexFormats vertexFormat,
						Format textureFormat, Usage usage, Pool pool)
		{
			// Clone the mesh vertex info
			Mesh mesh = mMesh.Clone(flags, vertexFormat, d3d.Dx);
			AutoMesh autoMesh = new AutoMesh(d3d, mesh);
			
			// Clone AutoMesh variables
			autoMesh.Tag = Tag;
			autoMesh.OwnsTextures = OwnsTextures;
			
			// Clone textures and materials
			if (mTextures.Length != 0)
			{
				// Clone materials
				autoMesh.mMaterialsEx = new ExtendedMaterial[mMaterialsEx.Length];
				for (int i = 0;  i < mMaterialsEx.Length;  i++)
					autoMesh.mMaterialsEx[i] = mMaterialsEx[i];
				
				// Clone textures
				autoMesh.mTextures = new AutoTexture[mTextures.Length];
				for (int i = 0;  i < mTextures.Length;  i++)
					if (mTextures[i] != null)
					{
						// Already cloned this texture?
						bool alreadyConvertedTexture = false;
						for (int j = 0;  j < i;  j++)
							if (mTextures[i] == mTextures[j])
							{
								alreadyConvertedTexture = true;
								autoMesh.mTextures[i] = autoMesh.mTextures[j];
								break;
							}
						// Clone new texture
						if (!alreadyConvertedTexture)
							autoMesh.mTextures[i] = mTextures[i].Clone(d3d, textureFormat, usage, pool);
					}
			}
			return autoMesh;			
		}

		/// <summary>
		/// Save the mesh when the DirectX device is lost
		/// </summary>
		void d3d_DxLost(Direct3d d3d, Device dx)
		{
			if (mMesh == null)
				return;

			// Save all data needed to restore the mesh
			mNumFaces = mMesh.NumberFaces;
			mNumVertices = mMesh.NumberVertices;
			mNumBytesPerVertex = mMesh.NumberBytesPerVertex;
			mFlags = mMesh.Options.Value;
			mVertexFormat = mMesh.VertexFormat;

			// Copy pathIndex buffer
			mIndexBufferCopy = (byte[])mMesh.LockIndexBuffer(typeof(byte),
										LockFlags.ReadOnly, mMesh.IndexBuffer.Description.Size);
			mMesh.UnlockIndexBuffer();

			// Copy vertex buffer
			mVertexBufferCopy = (byte[])mMesh.LockVertexBuffer(typeof(byte),
							LockFlags.ReadOnly, mMesh.NumberBytesPerVertex * mMesh.NumberVertices);
			mMesh.UnlockVertexBuffer();

			// Copy attribute buffer
			mAttributeBufferCopy = mMesh.LockAttributeBufferArray(LockFlags.ReadOnly);
			mMesh.UnlockAttributeBuffer(mAttributeBufferCopy);

			mMesh.Dispose();
			mMesh = null;
			mVertexCache = null;
		}

		/// <summary>
		/// Restore the mesh when the DirectX device is restored
		/// </summary>
		void d3d_DxRestore(Direct3d d3d, Device dx)
		{
			// If the direct3d device wasn't lost in the first place, don't restore it.
			// This happens the first timeMs around.
			if (mMesh != null)
				return;

			// Restore mesh			
			mMesh = new Mesh(mNumFaces, mNumVertices, mFlags, mVertexFormat, dx);
			Debug.Assert(mMesh.NumberBytesPerVertex == mNumBytesPerVertex);

			// Copy pathIndex buffer
			GraphicsStream stream = mMesh.LockIndexBuffer(LockFlags.Discard);
			stream.Write(mIndexBufferCopy);
			mMesh.UnlockIndexBuffer();
			
			// Copy vertex buffer
			stream = mMesh.LockVertexBuffer(LockFlags.Discard);
			stream.Write(mVertexBufferCopy);
			mMesh.UnlockVertexBuffer();

			// Copy attribute buffer
			int[] attributeBuffer = mMesh.LockAttributeBufferArray(LockFlags.Discard);
			mAttributeBufferCopy.CopyTo(attributeBuffer, 0);
			mMesh.UnlockAttributeBuffer(attributeBuffer);

			mIndexBufferCopy = null;
			mVertexBufferCopy = null;
			mAttributeBufferCopy = null;
		}

		/// <summary>
		/// Returns the bounding box of the mesh (only when the DirectX device is not lost).
		/// Caches the result for subsequent calls.
		/// </summary>
		public void BoundingBox(out Vector3 min, out Vector3 max)
		{
			if (!mBoundingBoxValid)
			{
				GraphicsStream stream = mMesh.LockVertexBuffer(LockFlags.ReadOnly);
				Geometry.ComputeBoundingBox(stream, mMesh.NumberVertices, mMesh.VertexFormat, 
											out mBoundingBoxMin, out mBoundingBoxMax);
				mMesh.UnlockVertexBuffer();
				mBoundingBoxValid = true;
			}
			min = mBoundingBoxMin;
			max = mBoundingBoxMax;
		}

		/// <summary>
		/// Returns the bounding sphere of the mesh (only when the DirectX device is not lost)
		/// Caches the result for subsequent calls.
		/// </summary>
		public float BoundingSphere(out Vector3 center)
		{
			if (!mSphereValid)
			{
				GraphicsStream stream = mMesh.LockVertexBuffer(LockFlags.ReadOnly);
				mSphereRadius = Geometry.ComputeBoundingSphere(stream, mMesh.NumberVertices, 
																mMesh.VertexFormat, out mSphereCenter);
				mMesh.UnlockVertexBuffer();
				mSphereValid = true;
			}
			center = mSphereCenter;
			return mSphereRadius;
		}

		/// <summary>
		/// Sometimes ComputeBoundingSphere doesn't return the smallest sphere,
		/// so this returns the smaller of ComputeBoundingSphere and ComputeBoundingBox
		/// </summary>
		public float BoundingSphereMin(out Vector3 center)
		{
			// Quick exit for previously calculated value
			if (mSphereMinValid)
			{
				center = mSphereMinCenter;
				return mSphereMinRadius;
			}
			
			// Get bounding sphere
			mSphereMinValid = true;
			mSphereMinRadius = BoundingSphere(out mSphereMinCenter);
			
			// Get bounding sphere around a bounding box
			float boxRadius;
			Vector3 min, max;
			BoundingBox(out min, out max);
			boxRadius = (max - min).Length()*0.5f;

			// Box radius smaller than bounding sphere?
			if (boxRadius <= mSphereRadius)
			{
				// Use bounding box instead of bounding sphere
				mSphereMinCenter = (min + max)*0.5f;
				mSphereMinRadius = boxRadius;
			}

			// Bounding box smaller
			center = mSphereMinCenter;
			return mSphereMinRadius;
		}

		/// <summary>
		/// This function returns all the vertices in the mesh.  The result is cached, and
		/// the same array is returned on repeated calls.  DO NOT MODIFY THE ARRAY.
		/// </summary>
		/// <returns></returns>
		public Vector3[] GetVertices()
		{
			// Return cached vertex list
			if (mVertexCache != null)
				return mVertexCache;
			if (mMesh == null)
				return new Vector3[0];

			// Convert the mesh to vertex-only vertexFormat, and read the vertex array
			Mesh vertexMesh = mMesh.Clone(MeshFlags.SystemMemory | MeshFlags.Use32Bit, VertexFormats.Position, mD3d.Dx);
			mVertexCache = (Vector3[])vertexMesh.LockVertexBuffer(typeof(Vector3), LockFlags.ReadOnly, mMesh.NumberVertices);
			vertexMesh.Dispose();
			return mVertexCache;
		}

		/// <summary>
		/// Read a mesh from an X file, and load the textures which are
		/// assumed to be in the same directory.  
		/// Sets OwnsTextures to true (they will be disposed when the mesh is disposed)
		/// </summary>
		public static AutoMesh LoadFromXFile(string path, MeshFlags flags, Direct3d d3d)
		{
			ExtendedMaterial[] extendedMaterials;
			AutoMesh mesh = new AutoMesh(d3d, Mesh.FromFile(path, 
										MeshFlags.SystemMemory, d3d.Dx, out extendedMaterials));

			mesh.OwnsTextures = true;
			mesh.mTextures = new AutoTexture[extendedMaterials.Length];
			mesh.mMaterialsEx = new ExtendedMaterial[extendedMaterials.Length];
			
			// Load all the textures for this mesh
			for (int i = 0;  i < extendedMaterials.Length;  i++)
			{
				if (extendedMaterials[i].TextureFilename != null)
				{					
					// Scan to see if we already have this texture
					bool alreadyHaveTexture = false;
					for (int j = 0;  j < i;  j++)
						if (extendedMaterials[i].TextureFilename == extendedMaterials[j].TextureFilename)
						{
							mesh.mTextures[i] = mesh.mTextures[j];
							alreadyHaveTexture = true;
							break;
						}
					// Load texture (if we don't already have it)
					string textureFileName = Path.Combine(Path.GetDirectoryName(path), extendedMaterials[i].TextureFilename);
					if (!alreadyHaveTexture)
						mesh.mTextures[i] = new AutoTexture(d3d, 
											TextureLoader.FromFile(d3d.Dx, textureFileName));
				}
				mesh.mMaterialsEx[i] = extendedMaterials[i];
				Material fixAmbient = mesh.mMaterialsEx[i].Material3D;
				fixAmbient.Ambient = mesh.mMaterialsEx[i].Material3D.Diffuse;
				mesh.mMaterialsEx[i].Material3D = fixAmbient;
			}
			return mesh;
		}
	}
}
