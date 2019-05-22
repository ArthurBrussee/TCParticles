using System;
using System.Diagnostics;
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
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

// Based on: https://www.alanzucconi.com/2015/09/16/how-to-sample-from-a-gaussian-distribution/
// Adapted to work with Unity random classes
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
		var ret = new float3(
			rand.NextGaussian() * sigma,
			rand.NextGaussian() * sigma,
			rand.NextGaussian() * sigma
		);

		return ret;
	}
}

// Extensions for unity math class
public static class mathext {
	// source: http://www.melax.com/diag.html?attredirects=0
	// Adapted by Arthur Brussee to Unity Mathematics code
	// A must be a symmetric matrix.
	// returns Q and D such that 
	// Diagonal matrix D = QT * A * Q;  and  A = Q*D*QT
	public static void diagonalize(float3x3 A, out float3x3 Q, out float3x3 D) {
		float3 o = float3.zero;
		var q = new float4(0.0f, 0.0f, 0.0f, 1.0f);

		Q = float3x3.identity;
		D = float3x3.identity;

		const int maxsteps = 24; // certainly wont need that many.
		for (int i = 0; i < maxsteps; ++i) {
			// quaternion to matrix
			float sqx = q[0] * q[0];
			float sqy = q[1] * q[1];
			float sqz = q[2] * q[2];
			float sqw = q[3] * q[3];

			Q[0][0] = sqx - sqy - sqz + sqw;
			Q[1][1] = -sqx + sqy - sqz + sqw;
			Q[2][2] = -sqx - sqy + sqz + sqw;

			float tmp1 = q[0] * q[1];
			float tmp2 = q[2] * q[3];

			Q[1][0] = 2.0f * (tmp1 + tmp2);
			Q[0][1] = 2.0f * (tmp1 - tmp2);

			tmp1 = q[0] * q[2];
			tmp2 = q[1] * q[3];

			Q[2][0] = 2.0f * (tmp1 - tmp2);
			Q[0][2] = 2.0f * (tmp1 + tmp2);

			tmp1 = q[1] * q[2];
			tmp2 = q[0] * q[3];

			Q[2][1] = 2.0f * (tmp1 + tmp2);
			Q[1][2] = 2.0f * (tmp1 - tmp2);

			// AQ = A * Q
			float3x3 AQ = A * Q;

			// D = Qt * AQ
			D = math.transpose(Q) * AQ;

			o.x = D[1][2];
			o.y = D[0][2];
			o.z = D[0][1];

			float3 m = math.abs(o);

			int k0 = m[0] > m[1] && m[0] > m[2] ? 0 : m[1] > m[2] ? 1 : 2;
			int k1 = (k0 + 1) % 3;
			int k2 = (k0 + 2) % 3;

			if (o[k0] == 0.0f) {
				break; // diagonal already
			}

			float theta = (D[k2][k2] - D[k1][k1]) / (2.0f * o[k0]);
			float sgn = theta > 0.0f ? 1.0f : -1.0f;
			theta *= math.sign(theta); // make it positive
			float t = sgn / (theta + (theta < 1e6 ? math.sqrt(theta * theta + 1.0f) : theta));
			float c = 1.0f / math.sqrt(t * t + 1.0f);

			if (c == 1.0f) {
				break; // no room for improvement - reached machine precision.
			}

			float4 jr = float4.zero;

			jr[k0] = sgn * math.sqrt((1.0f - c) / 2.0f); // using 1/2 angle identity sin(a/2) = sqrt((1-cos(a))/2)  
			jr[k0] *= -1.0f; // since our quat-to-matrix convention was for v*M instead of M*v
			jr[3] = math.sqrt(1.0f - jr[k0] * jr[k0]);

			if (jr[3] == 1.0f) {
				break; // reached limits of floating point precision
			}

			q[0] = q[3] * jr[0] + q[0] * jr[3] + q[1] * jr[2] - q[2] * jr[1];
			q[1] = q[3] * jr[1] - q[0] * jr[2] + q[1] * jr[3] + q[2] * jr[0];
			q[2] = q[3] * jr[2] + q[0] * jr[1] - q[1] * jr[0] + q[2] * jr[3];
			q[3] = q[3] * jr[3] - q[0] * jr[0] - q[1] * jr[1] - q[2] * jr[2];
			q = math.normalize(q);
		}
	}
	
	
	public static int argmin(float a, float b, float c) {
		float minVal = math.min(a, math.min(b, c));
		int argMin = a == minVal ? 0 : (b == minVal ? 1 : 2);
		return argMin;
	}

	public static int argmax(float a, float b) {
		return a > b ? 0 : 1;
	}
}

public static class MeshSampler {
	static ProfilerMarker s_sampleMeshMarker = new ProfilerMarker("SampleMeshPoints");

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

	public struct MeshPoint {
		public float3 Position;
		public float3 Normal;
		public float2 Uv;
	}

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
}

public static class PointCloudNormals {
	// Hyper parameters
	const int M = 33;

	const int BaseK = 100;
	const int KDens = 5;

	// const float Alpha = 0.95f;
	// const float Epsilon = 0.073f;
	// Could be calculated from Alpha & Epsilon above but better to just fix it here
	const int Hypotheses = 1000;

	static readonly int[] KScales1 = {BaseK};
	static readonly int[] KScales3 = {BaseK / 2, BaseK, BaseK * 2};
	static readonly int[] KScales5 = {BaseK / 4, BaseK / 2, BaseK, BaseK * 2, BaseK * 4};

	static ProfilerMarker s_generateDataMarker = new ProfilerMarker("GenerateDataMarker");
	static ProfilerMarker s_queryNeighboursMarker = new ProfilerMarker("QueryNeighbours");
	static ProfilerMarker s_houghTexMarker = new ProfilerMarker("HoughTexture");
	static ProfilerMarker s_saveCloudMarker = new ProfilerMarker("SaveCloud");

	public static float3 CalculateNormalWithMaxBin(HoughTex textures) {
		int2 maxBin = math.int2(-1, -1);
		int maxBinCount = 0;

		for (int y = 0; y < M; ++y) {
			for (int x = 0; x < M; ++x) {
				unsafe {
					int count = textures.Tex[x + y * M];

					if (count > maxBinCount) {
						maxBinCount = count;
						maxBin =  math.int2(x, y);
					}
				}
			}
		}

		float2 normalXy = math.float2(maxBin) * 2.0f / M - 1.0f;
		float normalZ = 1.0f - math.lengthsq(normalXy);
		return math.normalize(math.float3(normalXy, normalZ));
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

		job.Schedule(normalCount, 128).Complete();
		return normals;
	}

	static Color32 ToCol32(Color col) {
		return new Color32((byte) (col.r * 255.0f), (byte) (col.g * 255.0f), (byte) (col.b * 255.0f), (byte) (col.a * 255.0f));
	}

	public static PointCloudData GenerateTrainingData(GameObject go, int pointCloudCount, float sampleRate, string name) {
		Stopwatch sw = new Stopwatch();
		sw.Start();
		
		using (s_generateDataMarker.Auto()) {
			Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
			var tex = go.GetComponent<MeshRenderer>().sharedMaterial.mainTexture as Texture2D;

			NativeArray<MeshSampler.MeshPoint> meshPoints = MeshSampler.SampleRandomPointsOnMesh(mesh, pointCloudCount, 0.02f);

			var particlePositions = new NativeArray<float3>(meshPoints.Select(p => p.Position).ToArray(), Allocator.TempJob);
			var particleNormals = new NativeArray<float3>(meshPoints.Select(p => p.Normal).ToArray(), Allocator.Persistent);

			var kLevels = new NativeArray<int>(KScales5, Allocator.TempJob);

			int maxK = kLevels[kLevels.Length - 1];

			var pointDensities = new NativeArray<float>(particlePositions.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var neighbours = new NativeArray<int>(particlePositions.Length * maxK, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

			using (s_queryNeighboursMarker.Auto()) {
				var tree = new KDTree(particlePositions, 256);

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

			var sampleIndices = new NativeList<int>(Allocator.TempJob);
			var rand = new Random(8976543);
			for (int i = 0; i < particlePositions.Length; ++i) {
				if (rand.NextFloat() <= sampleRate) {
					sampleIndices.Add(i);
				}
			}

			var houghCube = new NativeArray<HoughTex>(sampleIndices.Length * kLevels.Length, Allocator.TempJob);

			using (s_houghTexMarker.Auto()) {
				var random = new Random(23454575);

				var createHoughTexJob = new HoughTexturesJob {
					Positions = particlePositions,
					SampleIndices = sampleIndices,
					PointDensities = pointDensities,
					Neighbours = neighbours,
					Rand = random,
					HoughTextures = houghCube,
					KLevels = kLevels
				};

				createHoughTexJob.Schedule(sampleIndices.Length, 64).Complete();
			}

			var trainNormals = new NativeArray<float3>(sampleIndices.Length, Allocator.Persistent);
			for (int i = 0; i < sampleIndices.Length; ++i) {
				trainNormals[i] = particleNormals[sampleIndices[i]];
			}

			NativeArray<float3> reconstructedNormals = CalculateNormalsWithMaxBin(houghCube, kLevels.Length, trainNormals);
			
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
				string path = "Assets/PointCloudCNN/Data/" + name + "/tcCloud.asset";

				if (!File.Exists(path)) {
					pointCloudData = ScriptableObject.CreateInstance<PointCloudData>();
					AssetDatabase.CreateAsset(pointCloudData, path);
				} else {
					pointCloudData = AssetDatabase.LoadAssetAtPath<PointCloudData>(path);
				}

				pointCloudData.Initialize(
					meshPoints.Select(p => new Vector3(p.Position.x, p.Position.y, p.Position.z)).ToArray(),
					reconstructedNormals.Select(v => new Vector3(v.x, v.y, v.z)).ToArray(),
					meshPoints.Select(p => ToCol32(tex.GetPixelBilinear(p.Uv.x, p.Uv.y).linear)).ToArray(),
					1.0f,
					Vector3.zero,
					Vector3.zero
				);

				HoughTex testTex = houghCube[0];
				var saveTex = new Texture2D(M, M);

				var colors = new Color[M * M];

				unsafe {
					for (int index = 0; index < M * M; ++index) {
						colors[index] = testTex.Tex[index] / 64.0f * Color.white;
					}
				}
				
				// What we 

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

			sw.Stop();
			Debug.Log("Done generating point cloud! Took: " + sw.ElapsedMilliseconds + "ms");

			return pointCloudData;
		}

		// Now save out true normals & hough cube in some format that is sensible.
		// Now we have a cube of hough textures, and a normal for each particle!
	}

	public unsafe struct HoughTex {
		public fixed int Tex[M * M];
	}
	
	[BurstCompile]
	struct CalculateNormalsWithMaxBinJob : IJobParallelFor {
		[ReadOnly] public NativeArray<HoughTex> Textures;
		[ReadOnly] public NativeArray<float3> TrueNormals;
		
		public NativeArray<float3> Normals;
		public int ScaleLevels;

		public void Execute(int index) {
			float3 normalSum = float3.zero;
			float3 trueNormal = TrueNormals[index];

			for (int kIndex = 0; kIndex < ScaleLevels; ++kIndex) {
				int texIndex = index * ScaleLevels + kIndex;
				float3 normal = CalculateNormalWithMaxBin(Textures[texIndex]);

				// Normally the sign ambiguity in the normal is resolved by facing towards the 
				// eg. Lidar scanner. In this case, since these point clouds do not come from scans
				// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
				if (math.dot(normal, trueNormal) < 0) {
					normal *= -1;
				}

				normalSum += normal;
				break;
			}

			Normals[index] = math.normalize(normalSum);
		}
	}

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

			throw new Exception();
		}
		
		public void Execute(int rawIndex) {
			int pointIndex = SampleIndices[rawIndex];
			int kMax = KLevels[KLevels.Length - 1];

			int neighbourBaseOffset = pointIndex * kMax;
			float* cumProb = stackalloc float[kMax];

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

				// Construct covariance matrix
				float3x3 posCovariance = float3x3.identity;
				
				// Calculate average position
				float3 meanPos = float3.zero;
				for (int i = 0; i < kVal; ++i) {
					meanPos += Positions[Neighbours[neighbourBaseOffset + i]];
				}
				meanPos /= kVal;

				for (int dimi = 0; dimi < 3; ++dimi) {
					for (int dimj = 0; dimj < 3; ++dimj) {
						float cov = 0.0f;
						for (int i = 0; i < kVal; ++i) {
							int ni = Neighbours[neighbourBaseOffset + i];
							cov += (Positions[ni][dimi] - meanPos[dimi]) * (Positions[ni][dimj] - meanPos[dimj]);
						}
						cov /= kVal;
						
						posCovariance[dimi][dimj] = cov;
					}
				}

				// Diagonalize the covariance matrix. This gives 3 eigen values in D
				// And axis in Q.
				// Way faster than a full blown PCA solver
				mathext.diagonalize(posCovariance, out float3x3 Q, out float3x3 D);
				int minPosDir = mathext.argmin(D[0][0], D[1][1], D[2][2]);

				float3 pcaZ = Q[minPosDir];
				float3 pcaY = Q[(minPosDir + 1) % 3];
				float3 pcaX = Q[(minPosDir + 2) % 3];

				// Store all 2D hypothesized normals in hough space
				float2* houghNormals = stackalloc float2[Hypotheses];
				
				for (int i = 0; i < Hypotheses; ++i) {
					// Pick 3 unique points, weighted by the local density
					int n0, n1, n2;
					do {
						// TODO: This freaks out for some reason :/
						n0 = PickWeightedRandomIndex(cumProb, kVal);
						n1 = PickWeightedRandomIndex(cumProb, kVal);
						n2 = PickWeightedRandomIndex(cumProb, kVal);
					} while (n0 == n1 || n0 == n2 || n1 == n2);

					int pi0 = Neighbours[neighbourBaseOffset + n0];
					int pi1 = Neighbours[neighbourBaseOffset + n1];
					int pi2 = Neighbours[neighbourBaseOffset + n2];

					float3 hypNormal = math.normalizesafe(math.cross(Positions[pi2] - Positions[pi0], Positions[pi1] - Positions[pi0]));
					float3 pcaNormal = math.dot(hypNormal, pcaX) * pcaX + math.dot(hypNormal, pcaY) * pcaY + math.dot(hypNormal, pcaZ) * pcaZ;

					// Normally the sign ambiguity in the normal is resolved by facing towards the 
					// eg. Lidar scanner. In this case, since these point clouds do not come from scans
					// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
					if (pcaNormal.z < 0) {
						pcaNormal *= -1;
					}

					houghNormals[i] = pcaNormal.xy;
				}
				
				// Now do 2D PCA for the hough spaced normals
				float2x2 texCov = float2x2.identity;
				float2 meanTex = float2.zero;
				for (int i = 0; i < Hypotheses; ++i) {
					meanTex += houghNormals[i];
				}
				meanTex /= Hypotheses;
				
				for (int dimi = 0; dimi < 2; ++dimi) {
					for (int dimj = 0; dimj < 2; ++dimj) {
						float cov = 0.0f;
						for (int i = 0; i < Hypotheses; ++i) {
							cov += (houghNormals[i][dimi] - meanTex[dimi]) * (houghNormals[i][dimj] - meanTex[dimj]);
						}
						cov /= Hypotheses;
						texCov[dimi][dimj] = cov;
					}
				}

				float3x3 texCov3 = float3x3.identity;
				texCov3[0] = math.float3(texCov[0], 0);
				texCov3[1] = math.float3(texCov[1], 0);

				mathext.diagonalize(texCov3, out float3x3 QTex, out float3x3 DTex);
				int maxDim = mathext.argmax(DTex[0][0], DTex[1][1]);

				float2 houghSpaceX = QTex[maxDim].xy;
				float2 houghSpaceY = QTex[(maxDim + 1) % 2].xy;
				
				// Finally accumulate into hough textures
				for (int i = 0; i < Hypotheses; ++i) {
					float2 localHough = math.dot(houghNormals[i], houghSpaceX) * houghSpaceX + math.dot(houghNormals[i], houghSpaceY) * houghSpaceY;
					int2 texPos = math.int2(math.floor(M * (localHough + 1.0f) / 2.0f));
					
					// When either x or y is exactly 1 the floor returns M which is out of bounds 
					// So clamp tex coordinates
					texPos = math.clamp(texPos, 0, M - 1);

					int houghIndex = texPos.x + texPos.y * M;
					tex.Tex[houghIndex]++;
				}

				// And write to buffer
				HoughTextures[rawIndex * KLevels.Length + kIndex] = tex;
			}
		}
	}

}