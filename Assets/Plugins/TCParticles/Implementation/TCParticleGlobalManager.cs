using System;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
#if UNITY_EDITOR
	using UnityEditor;
#endif

namespace TC.Internal {
	public class TCParticleGlobalManager : MonoBehaviour {
		public static TCParticleGlobalManager Instance;

		static Dictionary<MeshIndex, ComputeBuffer> s_meshStore = new Dictionary<MeshIndex, ComputeBuffer>();

		public static Mesh GlobalQuad;
		public ComputeShader ComputeShader;

#if UNITY_EDITOR
		// ReSharper disable once ArrangeAttributes
		[InitializeOnLoadMethod]
#endif
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		public static void Init() {
			var instances = Resources.FindObjectsOfTypeAll<TCParticleGlobalManager>();
			foreach (var inst in instances) {
				DestroyImmediate(inst);
			}

			Instance = new GameObject("TCParticleGlobalManager").AddComponent<TCParticleGlobalManager>();
			Instance.gameObject.hideFlags = HideFlags.HideAndDontSave;

			if (Application.isPlaying) {
				DontDestroyOnLoad(Instance);
			}

			//Have to load compute shader first frame as other components
			//TODO: I don't like resource loading this
			Instance.ComputeShader = Resources.Load("Compute/MassParticle") as ComputeShader;

			if (Instance.ComputeShader == null) {
				Debug.LogError("Failed to load compute shader!");
			}
		}

		void OnEnable() {
			Camera.onPreCull += OnCamRender;
	
			GlobalQuad = new Mesh();
			GlobalQuad.vertices = new[] {
				new Vector3(-0.5f, 0.5f, 0.0f),
				new Vector3(0.5f, 0.5f, 0.0f),
				new Vector3(0.5f, -0.5f, 0.0f),
				new Vector3(-0.5f, -0.5f, 0.0f)
			};

			GlobalQuad.uv = new[] {
				new Vector2(0, 1),
				Vector2.one,
				new Vector2(1, 0),
				Vector2.zero
			};

			//Stretch factors encoded in uv2
			GlobalQuad.uv2 = new[] { Vector2.zero, Vector2.one, Vector2.zero, Vector2.one };

			//TODO: Check if this is reversed or not
			GlobalQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
			GlobalQuad.RecalculateNormals();
			GlobalQuad.RecalculateBounds();
			GlobalQuad.RecalculateTangents();

			GlobalQuad.hideFlags = HideFlags.HideAndDontSave;
		}

		void OnDisable() {
			Camera.onPreCull -= OnCamRender;
			DestroyImmediate(GlobalQuad);
		}

		void Update() {
			for (int i = 0; i < Tracker<TCParticleSystem>.Count; ++i) {
				Tracker<TCParticleSystem>.All[i].TCUpdate();
			}
		}

		static void OnCamRender(Camera cam) {
			for (int i = 0; i < Tracker<TCParticleSystem>.Count; ++i) {
				var rend = Tracker<TCParticleSystem>.All[i].ParticleRenderer;
				rend.SetupDrawCall(cam);
			}
		}

		public void OnApplicationQuit() {
			foreach (var m in s_meshStore) {
				m.Value.Release();
			}

			s_meshStore.Clear();
		}

		//Used for retrieving mesh data
		static List<Vector3> s_verts = new List<Vector3>();
		static List<Vector3> s_normals = new List<Vector3>();

		static List<int> s_triangles = new List<int>();
		static List<Vector2> s_uvs = new List<Vector2>();
		static Face[] s_faces = new Face[128];

		[SuppressMessage("ReSharper", "NotAccessedField.Local"),SuppressMessage("ReSharper", "InconsistentNaming")]
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

		public struct MeshIndex : IEquatable<MeshIndex> {
			public readonly Mesh Mesh;
			public readonly int UvChannel;

			public MeshIndex(Mesh mesh, int uvChannel) {
				Mesh = mesh;
				UvChannel = uvChannel;
			}

			public bool Equals(MeshIndex other) {
				return Equals(Mesh, other.Mesh) && UvChannel == other.UvChannel;
			}

			public override bool Equals(object obj) {
				if (ReferenceEquals(null, obj)) return false;
				return obj is MeshIndex && Equals((MeshIndex) obj);
			}

			public override int GetHashCode() {
				unchecked {
					return ((Mesh != null ? Mesh.GetHashCode() : 0) * 397) ^ UvChannel;
				}
			}
		}

		static ComputeBuffer GenerateMeshData(MeshIndex meshIndex) {
			var mesh = meshIndex.Mesh;
			var uvChannel = meshIndex.UvChannel;

			//Build mesh data structure
			mesh.GetVertices(s_verts);
			mesh.GetNormals(s_normals);
			mesh.GetTriangles(s_triangles, 0);

			int count = s_triangles.Count / 3;

			uvChannel = Mathf.Clamp(uvChannel, 0, 4);
			mesh.GetUVs(uvChannel, s_uvs);

			//TODO: Reuse face somehow?

			ComputeBuffer buffer;
			if (!s_meshStore.TryGetValue(meshIndex, out buffer)) {
				buffer = new ComputeBuffer(count, ParticleComponent.SizeOf<Face>());
				s_meshStore[meshIndex] = buffer;
			} else {
				if (buffer == null || buffer.count != count) {
					if (buffer != null){
						buffer.Dispose();
					}

					buffer = new ComputeBuffer(count, ParticleComponent.SizeOf<Face>());
					s_meshStore[meshIndex] = buffer;
				}
			}

			while (s_faces.Length < buffer.count) {
				Array.Resize(ref s_faces, s_faces.Length * 2);
			}

			float totalArea = 0.0f;

			//TODO: Can't we parralise this easily?
			for (int i = 0; i < count; ++i) {
				int i3 = i * 3;
				int t0 = s_triangles[i3 + 0];
				int t1 = s_triangles[i3 + 1];
				int t2 = s_triangles[i3 + 2];

				s_faces[i] = new Face {
					a = s_verts[t0],
					b = s_verts[t1],
					c = s_verts[t2],

					na = s_normals[t0],
					nb = s_normals[t1],
					nc = s_normals[t2],

					uva = s_uvs[t0],
					uvb = s_uvs[t1],
					uvc = s_uvs[t2]
				};

				//Calculate area of triangle manually.
				//Equivalent to 0.5 * | (a - b) x (a - c) |
				float x1 = s_verts[t0].x - s_verts[t1].x;
				float x2 = s_verts[t0].y - s_verts[t1].y;
				float x3 = s_verts[t0].z - s_verts[t1].z;

				float y1 = s_verts[t0].x - s_verts[t2].x;
				float y2 = s_verts[t0].y - s_verts[t2].y;
				float y3 = s_verts[t0].z - s_verts[t2].z;

				float r1 = x2 * y3 - x3 * y2;
				float r2 = x3 * y1 - x1 * y3;
				float r3 = x1 * y2 - x2 * y1;

				float area = 0.5f * Mathf.Sqrt(r1 * r1 + r2 * r2 + r3 * r3);

				s_faces[i].cweight = area;
				totalArea += area;
			}

			float cumulative = 0;
			for (int i = 0; i < count; ++i) {
				cumulative += s_faces[i].cweight / totalArea;
				s_faces[i].cweight = cumulative;
			}

			buffer.SetData(s_faces, 0, 0, count);
			return buffer;
		}

		public static ComputeBuffer GetMeshBuffer(Mesh mesh, int uvChannel) {
			var meshIndex = new MeshIndex(mesh, uvChannel);

			if (!s_meshStore.TryGetValue(meshIndex, out ComputeBuffer buffer)) {
				buffer = GenerateMeshData(meshIndex);
			}
			
			return buffer;
		}

		public static void UpdateCacheForEmitMesh(Mesh mesh, int uvChannel) {
			var meshIndex = new MeshIndex(mesh, uvChannel);

			if (s_meshStore.ContainsKey(meshIndex)) {
				GenerateMeshData(meshIndex);
			}
		}

		public static void ReleaseCacheForEmitMesh(Mesh mesh, int uvChannel) {
			var meshIndex = new MeshIndex(mesh, uvChannel);

			if (!s_meshStore.ContainsKey(meshIndex)) {
				return;
			}

			var buffer = s_meshStore[meshIndex];
			buffer.Dispose();
			s_meshStore.Remove(meshIndex);
		}
	}
}