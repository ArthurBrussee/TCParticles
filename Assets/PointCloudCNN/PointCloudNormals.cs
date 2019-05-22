using System.Linq;
using DataStructures.ViliWonka.KDTree;
using TC;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Windows;
using Random = Unity.Mathematics.Random;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// Based on: https://www.alanzucconi.com/2015/09/16/how-to-sample-from-a-gaussian-distribution/
public static class RandomExt {
	public static float NextGaussian(this ref Random rand) {
		float2 v;
		float s;
		do {
			v = 2.0f * rand.NextFloat2() - 1.0f;
			s = math.lengthsq(v);
		} while (s >= 1.0f || s == 0.0f);

		s = math.sqrt(-2.0f * math.log(s) / s);
		return v.x * s;
	}

	public static float3 NextGaussianSphere(this ref Random rand, float sigma) {
		float3 ret = new float3(
			rand.NextGaussian() * sigma,
			rand.NextGaussian() * sigma,
			rand.NextGaussian() * sigma
		);
		
		return ret;
	}
}

public static class MeshSampler {
	public struct MeshPoint {
		public float3 Position;
		public float3 Normal;
		public float2 Uv;
	}
	
	static ProfilerMarker s_sampleMeshMarker = new ProfilerMarker("SampleMeshPoints");

	[BurstCompile]
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

	[BurstCompile]
	struct SampleMeshJob : IJob {
		[ReadOnly] public NativeArray<float3> Vertices;
		[ReadOnly] public NativeArray<float3> Normals;
		[ReadOnly] public NativeArray<float2> Uvs;
		[ReadOnly] public NativeArray<int> Triangles;

		[ReadOnly] public NativeArray<float> CumSizes;

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
				float3 posSample = posA + r * (posB - posA) + s * (posC - posA);
				float3 normalSample = normalA + r * (normalB - normalA) + s * (normalC - normalA);
				float2 uvSample = uvA + r * (uvB - uvA) + s * (uvC - uvA);

				posSample += Rand.NextGaussianSphere(NoiseLevel);

				MeshPoint point;
				point.Position = posSample;
				point.Normal = normalSample;
				point.Uv = uvSample;

				MeshPoints[index] = point;
			}
		}
	}

	public static float2[] ToNative(Vector2[] array) {
		return array.Select(v => math.float2(v)).ToArray();
	}

	public static float3[] ToNative(Vector3[] array) {
		return array.Select(v => math.float3(v)).ToArray();
	}
	
	public static NativeArray<MeshPoint> SampleRandomPointsOnMesh(Mesh mesh, int pointCount, float noiseLevel) {
		using (s_sampleMeshMarker.Auto()) {
			var triangles = new NativeArray<int>(mesh.triangles, Allocator.TempJob);
			var vertices = new NativeArray<float3>(ToNative(mesh.vertices), Allocator.TempJob);
			var normals = new NativeArray<float3>(ToNative(mesh.normals), Allocator.TempJob);
			var uvs = new NativeArray<float2>(ToNative(mesh.uv), Allocator.TempJob);

			int triCount = triangles.Length / 3;
			NativeArray<float> sizes = new NativeArray<float>(triCount, Allocator.TempJob);
			var areaJob = new TriangleAreaJob {Vertices = vertices, Triangles = triangles, Sizes = sizes};
			areaJob.Schedule(triCount, 64).Complete();

			NativeArray<float> cumSizes = new NativeArray<float>(triCount, Allocator.TempJob);

			float totalAccum = 0;
			for (int i = 0; i < cumSizes.Length; i++) {
				totalAccum += sizes[i];
				cumSizes[i] = totalAccum;
			}

			NativeArray<MeshPoint> meshPoints = new NativeArray<MeshPoint>(pointCount, Allocator.Persistent);

			Random rand = new Random();
			rand.InitState();

			var sampleJob = new SampleMeshJob {
				Vertices = vertices,
				Triangles = triangles, 
				Normals = normals, 
				Uvs = uvs, 
				CumSizes = cumSizes, 
				Rand = rand, 
				TotalAccum = totalAccum,
				NoiseLevel = noiseLevel,
				
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

public static class PointCloudNormals {
	// Hyper parameters
	const int M = 33;
	
	// TODO: Multi-scale...
	const int BaseK = 100;

	static readonly int[] KScales1 = {BaseK};
	static readonly int[] KScales3 = {BaseK / 2, BaseK, BaseK * 2};
	static readonly int[] KScales5 = {BaseK / 4, BaseK / 2, BaseK, BaseK * 2, BaseK * 4};
	
	const int KDens = 5;

	// const float Alpha = 0.95f;
	// const float Epsilon = 0.073f;
	// Could be calculated from Alpha & Epsilon above but better to just fix it here
	const int Hypotheses = 1000;
	
	public unsafe struct HoughTex {
		public fixed int Tex[M * M];

		public int Get(int2 pos) {
			int index = pos.x + pos.y * M;
			return Tex[index];			
		}
	}
	
	static float2 CartesianToSpherical(in float3 xyz) {
		float theta = math.atan2(xyz.y, xyz.x);
		float phi = math.acos(xyz.z);
		
		if (theta < 0) {
			theta += 2 * math.PI;
		}

		return new float2(theta, phi);
	}

	static float3 SphericalToCartesian(float2 spherical) {
		float theta = spherical.x;
		float phi = spherical.y;
			
		float3 ret;
		ret.x = math.cos(theta) * math.cos(phi);
		ret.y = math.sin(theta) * math.cos(phi);
		ret.z = math.sin(phi);
		return ret;
	}
	
	[BurstCompile]
	unsafe struct HoughTexturesJob : IJobParallelFor {
		[ReadOnly] public NativeArray<float3> Positions;
		[ReadOnly] public NativeArray<int> SampleIndices;
		[ReadOnly] public NativeArray<float> PointDensities;
		[ReadOnly] public NativeArray<int> Neighbours;
		[ReadOnly] public NativeArray<int> KLevels;
		
		[NativeDisableParallelForRestriction]
		public NativeArray<HoughTex> HoughTextures;
		
		public Random Rand;
		
		int PickWeightedRandomIndex(float* cumProb, int kVal) {
			float maxVal = cumProb[kVal - 1];
			float randomSample = Rand.NextFloat(maxVal);

			for (int i = 0; i < kVal; i++) {
				if (randomSample <= cumProb[i]) {
					return i;
				}
			}

			return -1;
		}
		
		public void Execute(int rawIndex) {
			int pointIndex = SampleIndices[rawIndex];
			int kMax = KLevels[KLevels.Length - 1];

			int neighbourBaseOffset = pointIndex * kMax;
			var cumProb = stackalloc float[kMax];

			float totalDensity = 0.0f;
			for (int i = 0; i < kMax; ++i) {
				int neighbourIndex = Neighbours[neighbourBaseOffset + i];
				totalDensity += PointDensities[neighbourIndex];
				cumProb[i] = totalDensity;
			}

			for (int kIndex = 0; kIndex < KLevels.Length; ++kIndex) {
				// Pick neighbours up to kVal
				int kVal = KLevels[kIndex];

				// Create a texture and zero initialize it
				HoughTex tex;
				for (int i = 0; i < M * M; ++i) {
					tex.Tex[i] = 0;
				}
	
				for (int i = 0; i < Hypotheses; ++i) {
					// Pick 3 unique points, weighted by the local density
					int n0, n1, n2;
					do {
						// TODO: This freaks out for some reason :/
						/*
						n0 = PickWeightedRandomIndex(cumProb, kVal);
						n1 = PickWeightedRandomIndex(cumProb, kVal);
						n2 = PickWeightedRandomIndex(cumProb, kVal);
						*/
						
						n0 = Rand.NextInt(kVal);
						n1 = Rand.NextInt(kVal);
						n2 = Rand.NextInt(kVal);
					} while (n0 == n1 || n0 == n2 || n1 == n2);


					int pi0 = Neighbours[neighbourBaseOffset + n0];
					int pi1 = Neighbours[neighbourBaseOffset + n1];
					int pi2 = Neighbours[neighbourBaseOffset + n2];

					float3 hypNormal = math.normalize(math.cross(Positions[pi2] - Positions[pi0], Positions[pi1] - Positions[pi0]));
					
					// Normally the sign ambiguity in the normal is resolved by facing towards the 
					// eg. Lidar scanner. In this case, since these point clouds do not come from scans
					// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
					if (hypNormal.z < 0) {
						hypNormal *= -1;
					}
					
					// For CNN:
					int2 texPos = math.int2(math.floor(M * (hypNormal.xy + 1.0f) / 2.0f));

					// For normal hough tex:
					// float2 angleCoords = CartesianToSpherical(hypNormal) * new float2(1 / (2 * math.PI), 1 / math.PI);
					// int2 texPos = math.int2(math.floor(angleCoords * M));

					int houghIndex = math.clamp(texPos.x + texPos.y * M, 0, M * M - 1);
					tex.Tex[houghIndex]++;
				}
				
				HoughTextures[rawIndex * KLevels.Length + kIndex] = tex;
			}
		}
	}

	public static float3 CalculateNormalWithMaxBin(HoughTex textures) {
		int2 maxBin = math.int2(-1, -1);
		int maxBinCount = 0;

		for (int x = 0; x < M; ++x) {
			for (int y = 0; y < M; ++y) {
				int2 bin = math.int2(x, y);
			
				int count = textures.Get(bin);

				if (count > maxBinCount) {
					maxBinCount = count;
					maxBin = bin;
				}
			}
		}

		// For CNN:
		float2 normalXy = math.float2(maxBin) * 2.0f / M - 1.0f;
		float normalZ = 1.0f - math.lengthsq(normalXy);
		return math.float3(normalXy, normalZ);

		// For normal hough tex
		// return SphericalToCartesian(new float2(2 * math.PI, math.PI) * new float2(maxBin) / M);
	}

	// [BurstCompile]
	struct CalculateNormalsWithMaxBinJob : IJobParallelFor {
		[ReadOnly] public NativeArray<HoughTex> Textures;
		[ReadOnly] public NativeArray<float3> TrueNormals;
		
		[WriteOnly] public NativeArray<float3> Normals;

		public int ScaleLevels;

		public void Execute(int index) {
			float3 normalSum = float3.zero;
			float3 trueNormal = TrueNormals[index];
			
			for (int kIndex = 0; kIndex < ScaleLevels; ++kIndex) {			
				int texIndex = index * ScaleLevels + kIndex;
				float3 normal =  CalculateNormalWithMaxBin(Textures[texIndex]);

				// Normally the sign ambiguity in the normal is resolved by facing towards the 
				// eg. Lidar scanner. In this case, since these point clouds do not come from scans
				// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
				if (math.dot(normal, trueNormal) < 0) {
					normal *= -1;
				}

				normalSum += normal;
			}


			Normals[index] = math.normalize(normalSum);
		}
	}

	public static NativeArray<float3> CalculateNormalsWithMaxBin(NativeArray<HoughTex> textures, int scaleLevels, NativeArray<float3> trueNormals) {
		// First calculate normals for every bin
		int normalCount = textures.Length / scaleLevels;
		
		var normals = new NativeArray<float3>(normalCount, Allocator.Persistent);
		var job = new CalculateNormalsWithMaxBinJob {
			Textures = textures,
			Normals = normals,
			ScaleLevels = scaleLevels,
			TrueNormals = trueNormals
		};
		
		job.Schedule(normalCount, 64).Complete();
		return normals;
	}

	static Color32 ToCol32(Color col) {
		return new Color32((byte) (col.r * 255.0f), (byte) (col.g * 255.0f), (byte) (col.b * 255.0f), (byte)(col.a * 255.0f));
	}
	
	static ProfilerMarker s_generateDataMarker = new ProfilerMarker("GenerateDataMarker");
	static ProfilerMarker s_queryNeighboursMarker = new ProfilerMarker("QueryNeighbours");
	static ProfilerMarker s_houghTexMarker = new ProfilerMarker("HoughTexture");
	static ProfilerMarker s_saveCloudMarker = new ProfilerMarker("SaveCloud");

	[BurstCompile]
	struct QueryNeighboursJob : IJob {
		public NativeArray<int> Neighbours;
		public NativeArray<float> PointDensities;
		
		[ReadOnly] public KDTree Tree;
		public int MaxK;

		public KnnQuery.QueryCache Cache;

		public int Start;
		public int Count;
		
		public void Execute() {
			for (int i = 0; i < Count; ++i) {
				int index = Start + i;
				
				float3 pos = Tree.Points[index];
				KnnQuery.KNearest(Tree, MaxK, pos, Neighbours.Slice(i * MaxK, MaxK), Cache);

				// Save local density weighting as squared distance to k_dens nearest neighbour
				int kdensIndex = KnnQuery.KNearestLast(Tree, KDens, pos, Cache);
				PointDensities[i] = math.distancesq(pos, Tree.Points[kdensIndex]);
			}
		}
	}

	public static PointCloudData GenerateTrainingData(GameObject go, int pointCloudCount, float sampleRate, string name) {
		using (s_generateDataMarker.Auto()) {
			var mesh = go.GetComponent<MeshFilter>().sharedMesh;
			var tex = go.GetComponent<MeshRenderer>().sharedMaterial.mainTexture as Texture2D;

			var meshPoints = MeshSampler.SampleRandomPointsOnMesh(mesh, pointCloudCount, 0.0f);
			
			var particlePositions = new NativeArray<float3>(meshPoints.Select(p => p.Position).ToArray(), Allocator.TempJob);
			var particleNormals = new NativeArray<float3>(meshPoints.Select(p => p.Normal).ToArray(), Allocator.Persistent);

			var kLevels = new NativeArray<int>(KScales1, Allocator.TempJob);
			
			int maxK = kLevels[kLevels.Length - 1];
			
			var pointDensities = new NativeArray<float>(particlePositions.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var neighbours = new NativeArray<int>(particlePositions.Length * maxK, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			
			using (s_queryNeighboursMarker.Auto()) {
				KDTree tree = new KDTree(particlePositions, 256);

				const int threadCount = 8;
				int scheduleRange = particlePositions.Length / threadCount;

				var handles = new NativeArray<JobHandle>(threadCount, Allocator.TempJob);
				var caches = new KnnQuery.QueryCache[threadCount];

				var results = new NativeArray<int>[threadCount];
				var densityResults = new NativeArray<float>[threadCount];
				
				for (int t = 0; t < threadCount; ++t) {
					int start = scheduleRange * t;
					int end = t == threadCount - 1 ? particlePositions.Length : scheduleRange * (t + 1);

					results[t] = new NativeArray<int>((end - start) * maxK, Allocator.TempJob);
					densityResults[t] = new NativeArray<float>(end - start, Allocator.TempJob);
					
					var job = new QueryNeighboursJob {
						Neighbours = results[t],
						PointDensities = densityResults[t],
						MaxK = maxK,
						Tree = tree,
						Start = start,
						Count = end - start,
						Cache = KnnQuery.GetQueryCache(tree, maxK)
					};

					handles[t] = job.Schedule();
					caches[t] = job.Cache;
				}

				JobHandle.CompleteAll(handles);

				for (int t = 0; t < threadCount; ++t) {
					NativeArray<int>.Copy(results[t], 0, neighbours, t * scheduleRange * maxK, results[t].Length);
					NativeArray<float>.Copy(densityResults[t], 0, pointDensities, t * scheduleRange, densityResults[t].Length);

					densityResults[t].Dispose();
					results[t].Dispose();
					caches[t].Dispose();
				}

				handles.Dispose();
				tree.Release();
			}
			
			// TODO: Apply subsampling here to generate a bit less training data - current amount is a bit too much...
			
			// Create a hough texture for each particle for each scale

			var sampleIndices = new NativeList<int>(Allocator.TempJob);

			Random rand = new Random(8976543);
			
			for (int i = 0; i < particlePositions.Length; ++i) {
				if (rand.NextFloat() < sampleRate) {
					sampleIndices.Add(i);
				}
			}

			var houghCube = new NativeArray<HoughTex>(sampleIndices.Length * kLevels.Length, Allocator.TempJob);

			using (s_houghTexMarker.Auto()) {
				var random = new Random();
				random.InitState();

				var createHoughTexJob = new HoughTexturesJob {
					Positions = particlePositions,
					SampleIndices = sampleIndices,
					PointDensities = pointDensities,
					Neighbours = neighbours,
					Rand = random,
					HoughTextures = houghCube,
					KLevels = kLevels,
				};

				createHoughTexJob.Schedule(sampleIndices.Length, 64).Complete();
			}

			NativeArray<float3> trainNormals = new NativeArray<float3>(sampleIndices.Length, Allocator.Persistent);
			for (int i = 0; i < sampleIndices.Length; ++i) {
				trainNormals[i] = particleNormals[sampleIndices[i]];
			}

			var reconstructedNormals = CalculateNormalsWithMaxBin(houghCube, kLevels.Length, trainNormals);
			
			float rms = 0.0f;
			for (int i = 0; i < reconstructedNormals.Length; ++i) {
				// TODO: Radians or degrees?
				float angle = math.acos(math.dot(reconstructedNormals[i], trainNormals[i]));
				rms += angle * angle;
			}

			rms = math.sqrt(rms / reconstructedNormals.Length);

			Debug.Log("Finished analyzing normals with max hough bin. Total RMS: " + rms);

			PointCloudData pointCloudData;

			using (s_saveCloudMarker.Auto()) {
				string path = "Assets/PointCloudCNN/Data/TCClouds/" + name + ".asset";

				if (!File.Exists(path)) {
					pointCloudData = ScriptableObject.CreateInstance<PointCloudData>();
					AssetDatabase.CreateAsset(pointCloudData, path);
				} else {
					pointCloudData = AssetDatabase.LoadAssetAtPath<PointCloudData>(path);
				}

				pointCloudData.Initialize(
					positions: meshPoints.Select(p => new Vector3(p.Position.x, p.Position.y, p.Position.z)).ToArray(),
					normals: reconstructedNormals.Select(v => new Vector3(v.x, v.y, v.z)).ToArray(),
					colors: meshPoints.Select(p => ToCol32(tex.GetPixelBilinear(p.Uv.x, p.Uv.y).linear)).ToArray(),
					scale: 1.0f,
					pivot: Vector3.zero,
					normalRotate: Vector3.zero
				);

				var testTex = houghCube[0];
				var saveTex = new Texture2D(M, M);

				Color[] colors = new Color[M * M];

				unsafe {
					for (int index = 0; index < M * M; ++index) {
						colors[index] = testTex.Tex[index] / 255.0f * Color.white;
					}
				}

				saveTex.SetPixels(colors);

				File.WriteAllBytes("Assets/PointCloudCNN/Data/testTexture.png", saveTex.EncodeToPNG());
				AssetDatabase.Refresh();
			}

			trainNormals.Dispose();
			sampleIndices.Dispose();
			particlePositions.Dispose();
			neighbours.Dispose();
			particleNormals.Dispose();
			houghCube.Dispose();
			reconstructedNormals.Dispose();
			pointDensities.Dispose();
			meshPoints.Dispose();
			kLevels.Dispose();
			
			return pointCloudData;
		}
		// Now save out true normals & hough cube in some format that is sensible.
		// Now we have a cube of hough textures, and a normal for each particle!
	}
}
