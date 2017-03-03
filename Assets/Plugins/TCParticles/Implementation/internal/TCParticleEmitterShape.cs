using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;

namespace TC.Internal {

	[Serializable]
	public class ParticleEmitterShape {
		public EmitShapes shape = EmitShapes.Sphere;
		public MinMax radius = MinMax.Constant(5.0f);

		public Vector3 cubeSize = Vector3.one;
		public int meshCount;
		public Mesh emitMesh;

		[Range(0.0f, 89.9f)] public float coneAngle;
		public float coneHeight;
		public float coneRadius;
		public float ringOuterRadius;
		public float ringRadius;

		public float lineLength;
		public float lineRadius;

		public bool spawnOnMeshSurface = true;
		public static int maxUniformMeshCount = 756;

		public StartDirectionType startDirectionType = StartDirectionType.Normal;
		public Vector3 startDirectionVector;
		public float startDirectionRandomAngle;
		public static Mesh setMesh;

		float m_totalArea;

		static Dictionary<Mesh, ComputeBuffer> s_emitMeshData;
		static ComputeBuffer s_emitProtoBuffer;

		struct Face {
			public Vector3 a;
			public Vector3 b;
			public Vector3 c;

			public Vector3 na;
			public Vector3 nb;
			public Vector3 nc;

			public float cweight;
		}

		public int OnSurface {
			get {
				if (!spawnOnMeshSurface) return 0;
				return (meshCount > maxUniformMeshCount) ? 2 : 1;
			}
		}

		public ParticleEmitterShape() {
			if (s_emitMeshData == null)
				s_emitMeshData = new Dictionary<Mesh, ComputeBuffer>();
		}

		public void GenerateMeshData() {
			meshCount = emitMesh.triangles.Length / 3;

			foreach (KeyValuePair<Mesh, ComputeBuffer> kp in s_emitMeshData.Where(pair => pair.Value == null)) {
				kp.Value.Release();
				s_emitMeshData.Remove(kp.Key);
				Object.Destroy(kp.Key);
			}

			if (emitMesh == null) {
				s_emitMeshData[emitMesh].Release();
				s_emitMeshData.Remove(emitMesh);

				Object.Destroy(emitMesh);

				setMesh = null;
			}

			if (s_emitMeshData.ContainsKey(emitMesh))
				return;

			Vector3[] vertices = emitMesh.vertices;
			Vector3[] normals = emitMesh.normals;
			int[] triangles = emitMesh.triangles;


			var faces = new Face[meshCount];

			for (int i = 0; i < meshCount; ++i) {
				faces[i] = new Face {
					a = vertices[triangles[i * 3 + 0]],
					b = vertices[triangles[i * 3 + 1]],
					c = vertices[triangles[i * 3 + 2]],
					na = normals[triangles[i * 3 + 0]],
					nb = normals[triangles[i * 3 + 1]],
					nc = normals[triangles[i * 3 + 2]]
				};

				if (meshCount < maxUniformMeshCount) {
					float area = Vector3.Cross(faces[i].a - faces[i].b, faces[i].c - faces[i].a).magnitude / 2.0f;
					faces[i].cweight = area;
					m_totalArea += area;
				}
				else
					faces[i].cweight = 0.0f;
			}


			if (meshCount < maxUniformMeshCount) {
				float cumulative = 0;
				for (int i = 0; i < meshCount; ++i) {
					cumulative += faces[i].cweight / m_totalArea;
					faces[i].cweight = cumulative;
				}
			}

			var cb = new ComputeBuffer(meshCount, 76);
			cb.SetData(faces);
			s_emitMeshData[emitMesh] = cb;
		}

		public void SetMeshData(ComputeShader cs, int kern, string name) {

			GenerateMeshData();
			if (setMesh == emitMesh) return;
			if (emitMesh == null) return;
			if (!s_emitMeshData.ContainsKey(emitMesh)) return;
			cs.SetBuffer(kern, name, s_emitMeshData[emitMesh]);
			setMesh = emitMesh;
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
			//Bug: Leaks if someone nulls positions buffer
			if (s_emitProtoBuffer != null) {
				s_emitProtoBuffer.Release();
			}


			//bug this leaks if we switch meshes at runtime
			if (emitMesh == null) return;
			if (!s_emitMeshData.ContainsKey(emitMesh)) return;

			s_emitMeshData[emitMesh].Release();
			s_emitMeshData.Remove(emitMesh);

			setMesh = null;
		}
	}
}