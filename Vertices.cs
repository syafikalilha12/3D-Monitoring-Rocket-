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
	/// <summary>
	/// Vertex type (Position only)
	/// </summary>
	struct VertexTypeP
	{
		public Vector3 Position;
		public const VertexFormats Format = VertexFormats.Position;

		public VertexTypeP(Vector3 position)
		{
			Position = position;
		}		
	}

	/// <summary>
	/// Vertex type (Position, colored)
	/// </summary>
	struct VertexTypePC
	{
		public Vector3 Position;
		public Color32 Color;
		public const VertexFormats Format = VertexFormats.Position | VertexFormats.Diffuse;		

		public VertexTypePC(Vector3 position, Color32 color)
		{
			Position = position;
			Color = color;
		}
	}

	/// <summary>
	/// Vertex type (Position, textured)
	/// </summary>
	struct VertexTypePT
	{
		public Vector3 Position;
		public float Tx, Ty;
		public const VertexFormats Format = VertexFormats.Position | VertexFormats.Texture1;

		public VertexTypePT(Vector3 position, float tx, float ty)
		{
			Position = position;
			Tx = tx;
			Ty = ty;
		}		
		public Vector2 Txy 
		{ 
			get { return new Vector2(Tx, Ty); } 
			set { Tx = value.X;  Ty = value.Y; }
		}		
	}

	/// <summary>
	/// Vertex type (Position textured, colored)
	/// </summary>
	struct VertexTypePCT
	{
		public Vector3 Position;
		public Color32 Color;
		public float Tx, Ty;
		public const VertexFormats Format = VertexFormats.Position | VertexFormats.Texture1 | VertexFormats.Diffuse;

		public VertexTypePCT(Vector3 position, Color32 color, float tx, float ty)
		{
			Position = position;
			Color = color;
			Tx = tx;
			Ty = ty;
		}
		public Vector2 Txy 
		{ 
			get { return new Vector2(Tx, Ty); } 
			set { Tx = value.X;  Ty = value.Y; }
		}		
	}

	/// <summary>
	/// Vertex type (Position, normal)
	/// </summary>
	struct VertexTypePN
	{
		public Vector3 Position;
		public Vector3 Normal;
		public const VertexFormats Format = VertexFormats.Position | VertexFormats.Normal;

		public VertexTypePN(Vector3 position, Vector3 normal)
		{
			Position = position;
			Normal = normal;
		}		
	}

	/// <summary>
	/// Vertex type (Position, normal, colored)
	/// </summary>
	struct VertexTypePNC
	{
		public Vector3 Position;
		public Vector3 Normal;
		public Color32 Color;
		public const VertexFormats Format = VertexFormats.Position | VertexFormats.Normal | VertexFormats.Diffuse;		

		public VertexTypePNC(Vector3 position, Vector3 normal, Color32 color)
		{
			Position = position;
			Normal = normal;
			Color = color;
		}
	}
	
	/// <summary>
	/// Vertex type (Position, nromal, textured)
	/// </summary>
	struct VertexTypePNT
	{
		public Vector3 Position;
		public Vector3 Normal;
		public float Tx, Ty;
		public const VertexFormats Format = VertexFormats.Position | VertexFormats.Normal | VertexFormats.Texture1;

		public VertexTypePNT(Vector3 position, Vector3 normal, float tx, float ty)
		{
			Position = position;
			Normal = normal;
			Tx = tx;
			Ty = ty;
		}		
		public Vector2 Txy 
		{ 
			get { return new Vector2(Tx, Ty); } 
			set { Tx = value.X;  Ty = value.Y; }
		}		
	}
	
	/// <summary>
	/// Vertex type (Position, normal, textured, colored - untested)
	/// </summary>
	struct VertexTypePNCT
	{
		public Vector3 Position;
		public Vector3 Normal;
		public Color32 Color;
		public float Tx, Ty;
		public const VertexFormats Format = VertexFormats.Position | VertexFormats.Normal | VertexFormats.Texture1 | VertexFormats.Diffuse;

		public VertexTypePNCT(Vector3 position, Vector3 normal, Color32 color, float tx, float ty)
		{
			Position = position;
			Normal = normal;
			Color = color;
			Tx = tx;
			Ty = ty;
		}
		public Vector2 Txy 
		{ 
			get { return new Vector2(Tx, Ty); } 
			set { Tx = value.X;  Ty = value.Y; }
		}		
	}

}
