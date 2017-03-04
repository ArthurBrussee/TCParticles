using System;
using UnityEngine;
using System.Collections.Generic;

namespace TC.Internal {
	[Serializable]
	public class ParticleEmitterShape {
		public EmitShapes shape = EmitShapes.Sphere;
		public MinMax radius = MinMax.Constant(5.0f);

		public Vector3 cubeSize = Vector3.one;
		public Mesh emitMesh;
		public bool normalizeArea = true;


		public Texture2D texture;
		public int uvChannel;



		[Range(0.0f, 89.9f)] public float coneAngle;

		public float coneHeight;
		public float coneRadius;
		public float ringOuterRadius;
		public float ringRadius;

		public float lineLength;
		public float lineRadius;

		public StartDirectionType startDirectionType = StartDirectionType.Normal;
		public Vector3 startDirectionVector;
		public float startDirectionRandomAngle;

		public bool spawnOnMeshSurface = true;

		float m_totalArea;

		static ComputeBuffer s_emitProtoBuffer;

		struct Face {
			public Vector3 a;
			public Vector3 b;
			public Vector3 c;

			public Vector3 na;
			public Vector3 nb;
			public Vector3 nc;

			public Vector2 uva;
			public Vector2 uvb;
			public Vector2 uvc;

			public float cweight;
		}


		void GenerateMeshData() {
			//Data already cached!
			if (TCParticleGlobalRender.MeshStore.ContainsKey(emitMesh)) {
				return;
			}
			
			//Build mesh data structure
			Vector3[] vertices = emitMesh.vertices;
			Vector3[] normals = emitMesh.normals;
			int[] triangles = emitMesh.triangles;

			int count = triangles.Length / 3;

			List<Vector2> uvs = new List<Vector2>();
			uvChannel = Mathf.Clamp(uvChannel, 0, 4);
			emitMesh.GetUVs(uvChannel, uvs);

			var faces = new Face[count];
			for (int i = 0; i < count; ++i) {
				int t0 = triangles[i * 3 + 0];
				int t1 = triangles[i * 3 + 1];
				int t2 = triangles[i * 3 + 2];

				faces[i] = new Face {
					a = vertices[t0],
					b = vertices[t1],
					c = vertices[t2],

					na = normals[t0],
					nb = normals[t1],
					nc = normals[t2],

					uva = uvs[t0],
					uvb = uvs[t1],
					uvc = uvs[t2]
				};

				//Do we accumulate area for normlisation?
				if (normalizeArea) {
					float area = Vector3.Cross(faces[i].a - faces[i].b, faces[i].c - faces[i].a).magnitude / 2.0f;
					faces[i].cweight = area;
					m_totalArea += area;
				} else {
					faces[i].cweight = 0.0f;
				}
			}

			if (normalizeArea) {
				float cumulative = 0;

				for (int i = 0; i < count; ++i) {
					cumulative += faces[i].cweight / m_totalArea;
					faces[i].cweight = cumulative;
				}
			}

			var buffer = new ComputeBuffer(count, ParticleComponent.SizeOf<Face>());
			buffer.SetData(faces);
			TCParticleGlobalRender.MeshStore[emitMesh] = buffer;
		}

		public void SetMeshData(ComputeShader cs, int kern, ref ParticleEmitter.Emitter emitter) {
			if (emitMesh == null) {
				return;
			}

			GenerateMeshData();

			var buffer = TCParticleGlobalRender.MeshStore[emitMesh];

			uint onSurface;

			if (!spawnOnMeshSurface)
				onSurface = 0;
			else {
				onSurface = normalizeArea ? (uint) 1 : 2;
			}



			emitter.MeshVertLen = (uint)buffer.count;
			emitter.OnSurface = onSurface;


			cs.SetBuffer(kern, "emitFaces", buffer);

			if (texture != null) {
				cs.SetTexture(kern, "_MeshTexture", texture);
			}
			else {
				cs.SetTexture(kern, "_MeshTexture", Texture2D.whiteTexture);
			}
		}

		public void SetListData(ComputeShader cs, int kern, ParticleProto[] particlePrototypes) {
			if (s_emitProtoBuffer == null || s_emitProtoBuffer.count < particlePrototypes.Length) {
				if (s_emitProtoBuffer != null) {
					s_emitProtoBuffer.Release();
				}

				s_emitProtoBuffer = new ComputeBuffer(particlePrototypes.Length, ParticleProto.Stride);
			}

			s_emitProtoBuffer.SetData(particlePrototypes);
			cs.SetBuffer(kern, "emitList", s_emitProtoBuffer);
		}

		public void ReleaseData() {
			//TODO: this releases the buffer too often if others still use it.
			//Not a big deal for point clouds though
			if (s_emitProtoBuffer != null) {
				s_emitProtoBuffer.Release();
			}
		}
	}
}