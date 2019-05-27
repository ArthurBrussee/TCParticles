using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public static class MeshSampler {
	static ProfilerMarker s_sampleMeshMarker = new ProfilerMarker("SampleMeshPoints");

	static float2[] ToNative(Vector2[] array) {
		return array.Select(v => math.float2(v)).ToArray();
	}

	static float3[] ToNative(Vector3[] array) {
		return array.Select(v => math.float3(v)).ToArray();
	}
	public struct MeshPoint {
		public float3 Position;
		public float3 Normal;
		public float2 Uv;
	}

	[BurstCompile(CompileSynchronously = true)]
	struct TriangleAreaJob : IJobParallelFor {
		[ReadOnly] public NativeArray<float3> Vertices;
		[ReadOnly] public NativeArray<int> Triangles;

		public NativeArray<float> Sizes;

		public void Execute(int i) {
			int i0 = Triangles[i * 3 + 0];
			int i1 = Triangles[i * 3 + 1];
			int i2 = Triangles[i * 3 + 2];
			Sizes[i] = 0.5f * math.length(math.cross(Vertices[i1] - Vertices[i0], Vertices[i2] - Vertices[i0]));
		}
	}

	struct NormalTex {
		[ReadOnly] public NativeArray<float4> Values;
		public int2 Size;

		public float3 Get(float2 uv) {
			int2 xy = math.int2(uv * Size);
			xy = math.clamp(xy, 0, Size - 1);
			return Values[xy.x + xy.y * Size.x].xyz;
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	struct SampleMeshJob : IJob {
		[ReadOnly] public NativeArray<float3> Vertices;
		[ReadOnly] public NativeArray<float3> Normals;
		[ReadOnly] public NativeArray<float2> Uvs;
		
		[ReadOnly] public NativeArray<int> Triangles;
		[ReadOnly] public NativeArray<float> CumSizes;

		[ReadOnly] public NormalTex Tex;
		
		public NativeArray<MeshPoint> MeshPoints;

		public Random Rand;
		public float TotalAccum;

		public float NoiseLevel;
		public int PointCount;

		public void Execute() {
			for (int index = 0; index < PointCount; ++index) {
				//so everything above this point wants to be factored out
				float randomSample = Rand.NextFloat(TotalAccum);

				int triIndex = -1;

				for (int i = 0; i < CumSizes.Length; i++) {
					if (randomSample <= CumSizes[i]) {
						triIndex = i;
						break;
					}
				}

				int vertIndex0 = Triangles[triIndex * 3 + 0];
				int vertIndex1 = Triangles[triIndex * 3 + 1];
				int vertIndex2 = Triangles[triIndex * 3 + 2];

				float3 posA = Vertices[vertIndex0];
				float3 posB = Vertices[vertIndex1];
				float3 posC = Vertices[vertIndex2];

				float3 normalA = Normals[vertIndex0];
				float3 normalB = Normals[vertIndex1];
				float3 normalC = Normals[vertIndex2];

				float2 uvA = Uvs[vertIndex0];
				float2 uvB = Uvs[vertIndex1];
				float2 uvC = Uvs[vertIndex2];

				//generate random barycentric coordinates
				float r = Rand.NextFloat();
				float s = Rand.NextFloat();

				if (r + s >= 1) {
					r = 1 - r;
					s = 1 - s;
				}

				//and then turn them back to a Vector3
				float3 normalSample;

				float3 posSample = posA + r * (posB - posA) + s * (posC - posA);
				float2 uvSample = uvA + r * (uvB - uvA) + s * (uvC - uvA);

				// Use normal map instead hurray
				if (math.all(Tex.Size > 0)) {
					normalSample = Tex.Get(uvSample);
				}
				else {
					normalSample = normalA + r * (normalB - normalA) + s * (normalC - normalA);
				}

				posSample += normalSample * Rand.NextGaussian(NoiseLevel * 4.0f);
				posSample += Rand.NextGaussianSphere(NoiseLevel);

				MeshPoint point;
				point.Position = posSample;
				point.Normal = normalSample;
				point.Uv = uvSample;

				MeshPoints[index] = point;
			}
		}
	}
	
	public static NativeArray<MeshPoint> SampleRandomPointsOnMesh(Mesh mesh, Texture2D tangentNormalMap, int pointCount, float noiseLevel) {
		using (s_sampleMeshMarker.Auto()) {

			NormalTex normalMapTex;

			if (tangentNormalMap != null) {
				var tex = new RenderTexture(tangentNormalMap.width, tangentNormalMap.height, 0, RenderTextureFormat.ARGBFloat);
				tex.Create();

				var getNormalMapMaterial = new Material(Shader.Find("Hidden/NormalExtract"));

				Graphics.SetRenderTarget(tex);
				GL.Viewport(new Rect(0, 0, tangentNormalMap.width, tangentNormalMap.height));
				GL.LoadOrtho(); // build ortho camera
				getNormalMapMaterial.SetTexture("_BumpMap", tangentNormalMap);
				getNormalMapMaterial.SetPass(0);
				Graphics.DrawMeshNow(mesh, Matrix4x4.identity, 0);

				var readbackTex = new Texture2D(tangentNormalMap.width, tangentNormalMap.height, TextureFormat.RGBAFloat, false);
				Graphics.SetRenderTarget(tex);
				readbackTex.ReadPixels(new Rect(0, 0, tangentNormalMap.width, tangentNormalMap.height), 0, 0);
				Graphics.SetRenderTarget(null);
				tex.Release();

				var normalMapTexData = readbackTex.GetRawTextureData<float4>();
				normalMapTex = new NormalTex {Values = normalMapTexData, Size = math.int2(readbackTex.width, readbackTex.height)};
			}
			else {
				normalMapTex = new NormalTex {Values = new NativeArray<float4>(0, Allocator.TempJob), Size = math.int2(0, 0)};
			}

			var triangles = new NativeArray<int>(mesh.triangles, Allocator.TempJob);
			var vertices = new NativeArray<float3>(ToNative(mesh.vertices), Allocator.TempJob);
			var normals = new NativeArray<float3>(ToNative(mesh.normals), Allocator.TempJob);
			var uvs = new NativeArray<float2>(ToNative(mesh.uv), Allocator.TempJob);

			int triCount = triangles.Length / 3;
			var sizes = new NativeArray<float>(triCount, Allocator.TempJob);
			var areaJob = new TriangleAreaJob {Vertices = vertices, Triangles = triangles, Sizes = sizes};
			areaJob.Schedule(triCount, 64).Complete();

			var cumSizes = new NativeArray<float>(triCount, Allocator.TempJob);

			float totalAccum = 0;
			for (int i = 0; i < cumSizes.Length; i++) {
				totalAccum += sizes[i];
				cumSizes[i] = totalAccum;
			}

			var meshPoints = new NativeArray<MeshPoint>(pointCount, Allocator.Persistent);

			var rand = new Random(123345);
			var sampleJob = new SampleMeshJob {
				Vertices = vertices,
				Triangles = triangles,
				Normals = normals,
				Uvs = uvs,
				CumSizes = cumSizes,
				Rand = rand,
				TotalAccum = totalAccum,
				NoiseLevel = noiseLevel,
				Tex = normalMapTex,
				MeshPoints = meshPoints,
				PointCount = pointCount
			};

			sampleJob.Schedule().Complete();

			sizes.Dispose();
			cumSizes.Dispose();
			triangles.Dispose();
			vertices.Dispose();
			normals.Dispose();
			uvs.Dispose();

			return meshPoints;
		}
	}


}
