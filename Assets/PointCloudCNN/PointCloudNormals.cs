using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

	/// <summary>
	/// Hough histogram data. Uses a byte to hold accumulation value. This is fine for a low number of Hypotheses (H << M * M *255)
	/// </summary>
	public unsafe struct HoughTex {
		public fixed byte Tex[M * M];
	}
	
	/// <summary>
	/// Estimate normal by selecting the max bin in a hough texture
	/// </summary>
	static float3 EstimateNormal(HoughTex tex) {
		int2 maxBin = math.int2(-1, -1);
		int maxBinCount = 0;

		for (int y = 0; y < M; ++y) {
			for (int x = 0; x < M; ++x) {
				unsafe {
					int count = tex.Tex[x + y * M];

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
	
	// Job to estimate an array of normals
	[BurstCompile(CompileSynchronously = true)]
	struct EstimateNormalsJob : IJobParallelFor {
		[ReadOnly] public NativeArray<HoughTex> Textures;
		[ReadOnly] public NativeArray<float3> TrueNormals;
		
		public NativeArray<float3> Normals;
		public int ScaleLevels;

		public void Execute(int index) {
			float3 normalSum = float3.zero;
			float3 trueNormal = TrueNormals[index];

			for (int kIndex = 0; kIndex < ScaleLevels; ++kIndex) {
				int texIndex = index * ScaleLevels + kIndex;
				float3 normal = EstimateNormal(Textures[texIndex]);

				// Normally the sign ambiguity in the normal is resolved by facing towards the 
				// eg. Lidar scanner. In this case, since these point clouds do not come from scans
				// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
				if (math.dot(normal, trueNormal) < 0) {
					normal *= -1;
				}

				normalSum += normal;
			}

			// Write final normalized normal
			Normals[index] = math.normalize(normalSum);
		}
	}
	
	/// <summary>
	/// Estimate normals for array of hough tex.
	/// </summary>
	static NativeArray<float3> EstimateNormals(NativeArray<HoughTex> textures, int scaleLevels, NativeArray<float3> trueNormals) {
		// First calculate normals for every bin
		int count = textures.Length / scaleLevels;
		var normals = new NativeArray<float3>(count, Allocator.Persistent);
		var job = new EstimateNormalsJob {
			Textures = textures,
			Normals = normals,
			ScaleLevels = scaleLevels,
			TrueNormals = trueNormals
		};
		job.Schedule(count, 128).Complete();
		return normals;
	}
	
	/// <summary>
	/// Estimate roughness as the determinant of the covariance matrix in the hough texture
	/// This is roughly equal to fitting a normal to the hough texture
	/// </summary>
	static float EstimateRoughness(HoughTex tex) {
		float2 meanPos = float2.zero;
		int totalPoints = 0;

		// Count up total tex weights
		unsafe {
			for (int y = 0; y < M; ++y) {
				for (int x = 0; x < M; ++x) {
					float2 pos = math.float2(x, y) / M;

					int index = x + y * M;
					totalPoints += tex.Tex[index];
					meanPos += tex.Tex[index] * pos;
				}
			}
		}

		meanPos /= totalPoints;
		float2x2 covariance = float2x2.identity;
		unsafe {
			for (int dimi = 0; dimi < 2; ++dimi) {
				for (int dimj = 0; dimj < 2; ++dimj) {
					float cov = 0;

					for (int y = 0; y < M; ++y) {
						for (int x = 0; x < M; ++x) {
							int index = x + y * M;
							float2 pos = math.float2(x, y) / M;
							cov += tex.Tex[index] * (pos[dimi] - meanPos[dimi]) * (pos[dimj] - meanPos[dimj]);
						}
					}

					covariance[dimi][dimj] = cov;
				}
			}
		}

		return math.determinant(covariance);
	}
	
	/// <summary>
	/// Estimate roughness for a list of hough textures
	/// </summary>
	[BurstCompile(CompileSynchronously = true)]
	struct EstimateRoughnessJob : IJobParallelFor {
		[ReadOnly] public NativeArray<HoughTex> Textures;
		
		public NativeArray<float> Roughness;
		public int ScaleLevels;

		public void Execute(int index) {
			float roughnessSum = 0.0f;
			for (int kIndex = 0; kIndex < ScaleLevels; ++kIndex) {
				int texIndex = index * ScaleLevels + kIndex;
				roughnessSum += EstimateRoughness(Textures[texIndex]);
			}
			// Return final averaged roughness
			Roughness[index] = roughnessSum / ScaleLevels;
		}
	}
	
	static NativeArray<float> EstimateRoughness(NativeArray<HoughTex> textures, int scaleLevels, float2 roughnessScale) {
		// First calculate normals for every bin
		int count = textures.Length / scaleLevels;
		
		var roughness = new NativeArray<float>(count, Allocator.Persistent);
		var job = new EstimateRoughnessJob {
			Textures = textures,
			Roughness = roughness,
			ScaleLevels = scaleLevels,
		};
		job.Schedule(count, 128).Complete();

		// Remap to specified roughness range
		float2 estimationScale = math.float2(mathext.min(roughness), mathext.max(roughness));
		
		for (int i = 0; i < roughness.Length; ++i) {
			float t = (roughness[i] - estimationScale.x) / (estimationScale.y - estimationScale.x);
			roughness[i] = math.lerp(roughnessScale.x, roughnessScale.y, t);
		}
		
		return roughness;
	}

	static Color32 ToCol32(Color col) {
		return new Color32((byte) (col.r * 255.0f), (byte) (col.g * 255.0f), (byte) (col.b * 255.0f), (byte) (col.a * 255.0f));
	}

	public static PointCloudData GenerateTrainingData(GameObject go, int pointCloudCount, float noiseLevel, float sampleRate, string name, bool writeData) {
		Stopwatch sw = new Stopwatch();
		sw.Start();
		
		using (s_generateDataMarker.Auto()) {
			Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;
			Material mat = go.GetComponent<MeshRenderer>().sharedMaterial;
			
			var tex = mat.GetTexture("_MainTex") as Texture2D;
			var normalTex = mat.GetTexture("_BumpMap") as Texture2D;
			var roughnessTex = mat.GetTexture("_MetallicGlossMap") as Texture2D;

			// Get points on the mesh
			NativeArray<MeshSampler.MeshPoint> meshPoints = MeshSampler.SampleRandomPointsOnMesh(mesh, normalTex, pointCloudCount, noiseLevel);

			var truePositions = new NativeArray<float3>(meshPoints.Select(p => p.Position).ToArray(), Allocator.TempJob);
			var trueNormals = new NativeArray<float3>(meshPoints.Select(p => p.Normal).ToArray(), Allocator.Persistent);

			
			var kLevels = new NativeArray<int>(KScales5, Allocator.TempJob);
			int maxK = kLevels[kLevels.Length - 1];

			var pointDensities = new NativeArray<float>(truePositions.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var neighbours = new NativeArray<int>(truePositions.Length * maxK, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

			using (s_queryNeighboursMarker.Auto()) {
				var tree = new KDTree(truePositions, 512);

				// Schedule 8 jobs to query all neighbourhoods
				const int jobCount = 8;
				int scheduleRange = truePositions.Length / jobCount;

				var handles = new NativeArray<JobHandle>(jobCount, Allocator.TempJob);
				var caches = new KnnQuery.QueryCache[jobCount];

				var results = new NativeArray<int>[jobCount];
				var densityResults = new NativeArray<float>[jobCount];

				for (int t = 0; t < jobCount; ++t) {
					int start = scheduleRange * t;
					int end = t == jobCount - 1 ? truePositions.Length : scheduleRange * (t + 1);

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

				// Wait for all jobs to be done
				JobHandle.CompleteAll(handles);

				// Write back results to final array
				for (int t = 0; t < jobCount; ++t) {
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
			for (int i = 0; i < truePositions.Length; ++i) {
				if (rand.NextFloat() <= sampleRate) {
					sampleIndices.Add(i);
				}
			}

			var houghTextures = new NativeArray<HoughTex>(sampleIndices.Length * kLevels.Length, Allocator.TempJob);

			using (s_houghTexMarker.Auto()) {
				var random = new Random(23454575);

				var createHoughTexJob = new HoughTexturesJob {
					Positions = truePositions,
					TrueNormals = trueNormals,
					SampleIndices = sampleIndices,
					PointDensities = pointDensities,
					Neighbours = neighbours,
					Rand = random,
					HoughTextures = houghTextures,
					KLevels = kLevels
				};

				createHoughTexJob.Schedule(sampleIndices.Length, 64).Complete();
			}

			var trueRoughness = new NativeArray<float>(meshPoints.Select(p => roughnessTex.GetPixelBilinear(p.Uv.x, p.Uv.y).a).ToArray(), Allocator.TempJob);
			float2 trueRoughnessRange = math.float2(mathext.min(trueRoughness), mathext.max(trueRoughness));
			NativeArray<float> reconstructedRoughness = EstimateRoughness(houghTextures, kLevels.Length, trueRoughnessRange);
			
			var truePositionsSample = new NativeArray<float3>(sampleIndices.Length, Allocator.TempJob);
			var trueNormalsSample = new NativeArray<float3>(sampleIndices.Length, Allocator.TempJob);
			var trueRoughnessSample = new NativeArray<float>(sampleIndices.Length, Allocator.TempJob);
			var trueUvSample = new NativeArray<float2>(sampleIndices.Length, Allocator.TempJob);
			
			for (int i = 0; i < sampleIndices.Length; ++i) {
				int index = sampleIndices[i];

				trueUvSample[i] = meshPoints[index].Uv;
				truePositionsSample[i] = truePositions[index];
				trueNormalsSample[i] = trueNormals[index];
				trueRoughnessSample[i] = trueRoughness[index];
			}
			
			NativeArray<float3> reconstructedNormals = EstimateNormals(houghTextures, kLevels.Length, trueNormalsSample);
			
			// TODO: More metrics here!
			float rms = 0.0f;
			for (int i = 0; i < reconstructedNormals.Length; ++i) {
				float angle = math.acos(math.dot(reconstructedNormals[i], trueNormalsSample[i]));
				rms += angle * angle;
			}			
			rms = math.sqrt(rms / reconstructedNormals.Length);

			Debug.Log("Finished analyzing normals with max hough bin. Total RMS: " + rms);

			PointCloudData pointCloudData;
			using (s_saveCloudMarker.Auto()) {
				// Save to TC Point cloud we can render
				Directory.CreateDirectory("Assets/PointCloudCNN/Clouds/");
				string path = "Assets/PointCloudCNN/Clouds/"  + name + ".asset";

				if (!File.Exists(path)) {
					pointCloudData = ScriptableObject.CreateInstance<PointCloudData>();
					AssetDatabase.CreateAsset(pointCloudData, path);
				} else {
					pointCloudData = AssetDatabase.LoadAssetAtPath<PointCloudData>(path);
				}

				var cloudAlbedoValues = trueUvSample.Select(p => ToCol32(tex.GetPixelBilinear(p.x, p.y).linear)).ToArray();
				var cloudPoints = truePositionsSample.Select(p => new Vector3(p.x, p.y, p.z)).ToArray();
				var cloudNormals = trueNormalsSample.Select(v => new Vector3(v.x, v.y, v.z)).ToArray();
				
				// Encode roughness in color alpha to use in the shader
				for (int i = 0; i < cloudAlbedoValues.Length; ++i) {
					cloudAlbedoValues[i].a = (byte) (trueRoughness[i] * 255);
				}

				pointCloudData.Initialize(
					cloudPoints,
					cloudNormals,
					cloudAlbedoValues,
					1.0f,
					Vector3.zero,
					Vector3.zero
				);

				if (writeData) {
					string imagesPath = "PointCloudCNN/TrainingData/" + name + kLevels.Length + "/";
					Directory.CreateDirectory("PointCloudCNN/TrainingData/");
					Directory.CreateDirectory(imagesPath);
					
					StringBuilder fileName = new StringBuilder();
					
					var saveTex = new Texture2D(M, M);
					var colors = new Color32[M * M];
					
					// Write training data to disk
					for (int i = 0; i < sampleIndices.Length; ++i) {
						for (int scaleIndex = 0; scaleIndex < kLevels.Length; ++scaleIndex) {
							HoughTex testTex = houghTextures[i * kLevels.Length + scaleIndex];


							int maxValue = 0;
							for (int index = 0; index < M * M; ++index) {
								unsafe {
									maxValue = math.max(maxValue, testTex.Tex[index]);
								}
							}


							for (int index = 0; index < M * M; ++index) {
								unsafe {
									byte level = (byte)math.clamp((int)math.round(255 * testTex.Tex[index] / (float)maxValue), 0, 255);
									colors[index] = new Color32(level, level, level, 255);
								}
							}

							saveTex.SetPixels32(colors);

							fileName.Clear();

							fileName.Append(imagesPath);
							fileName.Append(i);
							fileName.Append("_k_");
							fileName.Append(scaleIndex);
							fileName.Append("_x_");
							float2 setNorm = trueNormalsSample[i].xy;
							fileName.Append(setNorm.x.ToString("0.000"));
							fileName.Append("_y_");
							fileName.Append(setNorm.y.ToString("0.000"));
							fileName.Append("_r_");
							fileName.Append(trueRoughness[i].ToString("0.000"));
							fileName.Append(".png");
							File.WriteAllBytes(fileName.ToString(), saveTex.EncodeToPNG());
						}
					}
				}
			}

			// Release all buffers
			truePositionsSample.Dispose();
			trueUvSample.Dispose();
			trueNormalsSample.Dispose();
			trueRoughnessSample.Dispose();
			sampleIndices.Dispose();
			truePositions.Dispose();
			neighbours.Dispose();
			trueNormals.Dispose();
			houghTextures.Dispose();
			reconstructedNormals.Dispose();
			pointDensities.Dispose();
			meshPoints.Dispose();
			kLevels.Dispose();
			trueRoughness.Dispose();
			reconstructedRoughness.Dispose();

			sw.Stop();
			Debug.Log("Done generating point cloud! Took: " + sw.ElapsedMilliseconds + "ms");

			return pointCloudData;
		}

		// Now save out true normals & hough cube in some format that is sensible.
		// Now we have a cube of hough textures, and a normal for each particle!
	}




	[BurstCompile(CompileSynchronously = true)]
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

	[BurstCompile(CompileSynchronously = true)]
	unsafe struct HoughTexturesJob : IJobParallelFor {
		[ReadOnly] public NativeArray<float3> Positions;
		[ReadOnly] public NativeArray<int> SampleIndices;
		[ReadOnly] public NativeArray<float> PointDensities;
		[ReadOnly] public NativeArray<int> Neighbours;
		[ReadOnly] public NativeArray<int> KLevels;

		[ReadOnly] public NativeArray<float3> TrueNormals;

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
			
			float3 trueNormal = TrueNormals[pointIndex];

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
							
							float3 testPos = Positions[ni];
							cov += (testPos[dimi] - meanPos[dimi]) * (testPos[dimj] - meanPos[dimj]);
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
					if (math.dot(trueNormal, pcaNormal) < 0) {
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
							float2 testPos = houghNormals[i];
							cov += (testPos[dimi] - meanTex[dimi]) * (testPos[dimj] - meanTex[dimj]);
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
					float2 localHough = math.dot(houghNormals[i], houghSpaceX) * houghSpaceX + 
										math.dot(houghNormals[i], houghSpaceY) * houghSpaceY;
					
					int2 texPos = math.int2(math.floor(M * (localHough + 1.0f) / 2.0f));
					
					// When either x or y is exactly 1 the floor returns M which is out of bounds 
					// So clamp tex coordinates
					texPos = math.clamp(texPos, 0, M - 1);

					int houghIndex = texPos.x + texPos.y * M;
					
					if (tex.Tex[houghIndex] < byte.MaxValue) {
						tex.Tex[houghIndex]++;
					}
				}

				// And write to buffer
				HoughTextures[rawIndex * KLevels.Length + kIndex] = tex;
			}
		}
	}

}