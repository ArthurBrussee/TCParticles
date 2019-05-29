using System.Diagnostics;
using System.Linq;
using System.Text;
using DataStructures.ViliWonka.KDTree;
using TC;
using TensorFlow;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Windows;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

/// <summary>
/// Main class to handle generation of the point cloud normals / training data
/// </summary>
public static class PointCloudNormals {
	// Hyper parameters, see report for more details
	const int c_m = 33;
	const int c_kDens = 5;

	// const float Alpha = 0.95f;
	// const float Epsilon = 0.073f;
	// Could be calculated from Alpha & Epsilon above but better to just fix it here
	const int c_hypotheses = 1000;

	// Base number of k-neighbourhood to consider
	const int c_baseK = 100;

	const int c_scaleLevels = 5;

	// K levels to use at different scales
	// Since all the scale maps for 1 & 3 are also generated when generating 5 scales
	// It is easier to just read out those than to re-generate data
	// static readonly int[] KScales1 = {c_baseK};
	// static readonly int[] KScales3 = {c_baseK / 2, c_baseK, c_baseK * 2};
	static readonly int[] KScales = new int[c_scaleLevels]{c_baseK / 4, c_baseK / 2, c_baseK, c_baseK * 2, c_baseK * 4};

	static ProfilerMarker s_generateDataMarker = new ProfilerMarker("GenerateDataMarker");
	static ProfilerMarker s_queryNeighboursMarker = new ProfilerMarker("QueryNeighbours");
	static ProfilerMarker s_houghTexMarker = new ProfilerMarker("HoughTexture");
	static ProfilerMarker s_saveCloudMarker = new ProfilerMarker("SaveCloud");

	/// <summary>
	/// Hough histogram data. Uses a byte to hold accumulation value. This is fine for a low number of Hypotheses (H much smaller than M * M *255)
	/// </summary>
	public unsafe struct HoughHistogram {
		// ReSharper disable once UnassignedField.Global
		fixed byte Counts[c_m * c_m * c_scaleLevels];

		public float3x3 NormalsBasis;
		public float3x3 TexBasis;
		
		public static HoughHistogram Empty(float3x3 normalBasis, float3x3 texBasis) {
			HoughHistogram ret;
			ret.NormalsBasis = normalBasis;
			ret.TexBasis = texBasis;
			UnsafeUtility.MemClear(ret.Counts, c_m * c_m * sizeof(byte));
			return ret;
		}

		public ref byte this[int x, int y, int k] {
			get => ref Counts[x + y * c_m + k * c_m * c_scaleLevels];
		}
	}
	
	/// <summary>
	/// Estimate roughness as the determinant of the covariance matrix in the hough texture
	/// This is roughly equal to fitting a normal to the hough texture
	/// </summary>
	static float EstimateRoughness(HoughHistogram histogram, int kLevel) {
		float2 meanPos = float2.zero;
		int totalPoints = 0;

		// Count up total tex weights
		for (int y = 0; y < c_m; ++y) {
			for (int x = 0; x < c_m; ++x) {
				float2 pos = math.float2(x, y) / c_m;
				int count = histogram[x, y, kLevel];

				totalPoints += count;
				meanPos += count * pos;
			}
		}

		meanPos /= totalPoints;
		float2x2 covariance = float2x2.identity;
		
		for (int dimi = 0; dimi < 2; ++dimi) {
			for (int dimj = 0; dimj < 2; ++dimj) {
				float cov = 0;

				for (int y = 0; y < c_m; ++y) {
					for (int x = 0; x < c_m; ++x) {
						float2 pos = math.float2(x, y) / c_m;
						cov += histogram[x, y, kLevel] * (pos[dimi] - meanPos[dimi]) * (pos[dimj] - meanPos[dimj]);
					}
				}

				covariance[dimi][dimj] = cov;
			}
		}

		return math.determinant(covariance);
	}

	/// <summary>
	/// Main job for generating the hough histograms
	/// </summary>
	// [BurstCompile(CompileSynchronously = true)]
	unsafe struct HoughHistogramsJob : IJobParallelFor {
		[ReadOnly] public NativeArray<float3> Positions;
		[ReadOnly] public NativeArray<float> PointDensities;
		[ReadOnly] public NativeArray<int> Neighbours;
		[ReadOnly] public NativeArray<float3> TrueNormals;

		[NativeDisableParallelForRestriction]
		public NativeArray<HoughHistogram> HoughHistograms;

		public Random Rand;

		int PickWeightedRandomIndex(float* a, int kVal) {
			float maxVal = a[kVal - 1];
			float value = Rand.NextFloat(maxVal);

			for (int i = 0; i < kVal; i++) {
				if (value <= a[i]) {
					return i;
				}
			}

			// Should never get here...
			return -1;
		}

		public void Execute(int index) {
			int kMax = KScales[c_scaleLevels - 1];
			int neighbourBaseOffset = index * kMax;

			float totalDensity = 0.0f;

			float* cumProb = stackalloc float[kMax];
			for (int i = 0; i < kMax; ++i) {
				int neighbourIndex = Neighbours[neighbourBaseOffset + i];
				totalDensity += PointDensities[neighbourIndex];
				cumProb[i] = totalDensity;
			}

			// Create a texture and zero initialize it
			HoughHistogram histogram = HoughHistogram.Empty();

			// TODO: CReater PCA basis first!
			
			
			for (int kIndex = 0; kIndex < c_scaleLevels; ++kIndex) {
				// Pick neighbours up to kVal
				int kVal = KScales[kIndex];

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
							float3 testPos = Positions[Neighbours[neighbourBaseOffset + i]];
							cov += (testPos[dimi] - meanPos[dimi]) * (testPos[dimj] - meanPos[dimj]);
						}

						cov /= kVal;
						posCovariance[dimi][dimj] = cov;
					}
				}

				// Diagonalize the covariance matrix. This gives 3 eigen values in D
				// And axis in Q.
				// Way faster than a full blown PCA solver
				mathext.eigendecompose(posCovariance, out float3x3 eigenBasis, out float3 eigenVals);

				var pcaBasis = eigenBasis;

				// Store all 2D hypothesized normals in hough space
				float2* houghNormals = stackalloc float2[c_hypotheses];

				int hypothesesCompleted = 0;
				while (hypothesesCompleted < c_hypotheses) {
					// Pick 3 points, weighted by the local density
					int n0 = PickWeightedRandomIndex(cumProb, kVal);
					int n1 = PickWeightedRandomIndex(cumProb, kVal);
					int n2 = PickWeightedRandomIndex(cumProb, kVal);

					int pi0 = Neighbours[neighbourBaseOffset + n0];
					int pi1 = Neighbours[neighbourBaseOffset + n1];
					int pi2 = Neighbours[neighbourBaseOffset + n2];

					float3 hypNormal = math.cross(Positions[pi2] - Positions[pi0], Positions[pi1] - Positions[pi0]);

					// If we pick bad points or co-tangent points, we end up with a bad 
					// 0 length normal... can't use this, discard this hypothesis and try again
					if (math.lengthsq(hypNormal) < math.FLT_MIN_NORMAL) {
						continue;
					}

					hypNormal = math.normalize(hypNormal);

					// Normally the sign ambiguity in the normal is resolved by facing towards the 
					// eg. Lidar scanner. In this case, since these point clouds do not come from scans
					// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
					float3 trueNormal = TrueNormals[index];

					if (math.dot(trueNormal, hypNormal) < 0) {
						hypNormal = -hypNormal;
					}

					float3 pcaNormal = math.mul(pcaBasis, hypNormal);
					
					// float3 pcaNormal = math.dot(hypNormal, pcaX) * pcaX + math.dot(hypNormal, pcaY) * pcaY + math.dot(hypNormal, pcaZ) * pcaZ;
					houghNormals[hypothesesCompleted] = pcaNormal.xy;
					hypothesesCompleted++;
				}

				// Now do 2D PCA for the hough spaced normals
				float2x2 texCov = float2x2.identity;
				float2 meanTex = float2.zero;
				for (int i = 0; i < c_hypotheses; ++i) {
					meanTex += houghNormals[i];
				}

				meanTex /= c_hypotheses;

				for (int dimi = 0; dimi < 2; ++dimi) {
					for (int dimj = 0; dimj < 2; ++dimj) {
						float cov = 0.0f;
						for (int i = 0; i < c_hypotheses; ++i) {
							float2 testPos = houghNormals[i];
							cov += (testPos[dimi] - meanTex[dimi]) * (testPos[dimj] - meanTex[dimj]);
						}

						cov /= c_hypotheses;
						texCov[dimi][dimj] = cov;
					}
				}

				float3x3 texCov3 = float3x3.identity;
				texCov3[0] = math.float3(texCov[0], 0);
				texCov3[1] = math.float3(texCov[1], 0);

				mathext.eigendecompose(texCov3, out float3x3 qTex, out float3 dTex);

				float2 houghSpaceX = qTex[0].xy;
				float2 houghSpaceY = qTex[1].xy;

				// Finally accumulate into hough textures
				for (int i = 0; i < c_hypotheses; ++i) {
					float2 localHough = math.dot(houghNormals[i], houghSpaceX) * houghSpaceX +
					                    math.dot(houghNormals[i], houghSpaceY) * houghSpaceY;

					localHough = houghNormals[i];
					
					float2 uv = (localHough + 1.0f) / 2.0f;
					int2 histogramPos = math.int2(math.floor(uv * c_m));

					// When either x or y is exactly 1 the floor returns M which is out of bounds 
					// So clamp tex coordinates
					histogramPos = math.clamp(histogramPos, 0, c_m - 1);

					ref byte count = ref histogram[histogramPos.x, histogramPos.y, kIndex];
					if (count< byte.MaxValue) {
						count++;
					}
				}
			}
			
			// And write to buffer
			HoughHistograms[index] = histogram;
		}
	}

	public static PointCloudData GenerateTrainingData(NativeArray<MeshPoint> meshPoints, float sampleRate, string folder, string name, bool writeData, bool UseNeuralNet) {
		var sw = new Stopwatch();
		sw.Start();
		
		using (s_generateDataMarker.Auto()) {
			int maxK = KScales[c_scaleLevels - 1];

			var sampleIndices = new NativeList<int>(Allocator.TempJob);
			var rand = new Random(8976543);

			for (int i = 0; i < meshPoints.Length; ++i) {
				if (rand.NextFloat() <= sampleRate) {
					sampleIndices.Add(i);
				}
			}

			var truePositionsSample = new NativeArray<float3>(sampleIndices.Length, Allocator.TempJob);
			var trueNormalsSample = new NativeArray<float3>(sampleIndices.Length, Allocator.TempJob);
			var trueRoughnessSample = new NativeArray<float>(sampleIndices.Length, Allocator.TempJob);
			var trueAlbedoSample = new NativeArray<Color32>(sampleIndices.Length, Allocator.TempJob);

			for (int i = 0; i < sampleIndices.Length; ++i) {
				int index = sampleIndices[i];
				truePositionsSample[i] = meshPoints[index].Position;
				trueNormalsSample[i] = meshPoints[index].Normal;
				trueRoughnessSample[i] = meshPoints[index].Smoothness;
				trueAlbedoSample[i] = meshPoints[index].Albedo;
			}
			
			s_queryNeighboursMarker.Begin();

			// Schedule 8 jobs to query all neighbourhoods
			const int jobCount = 8;

			var neighbours = new NativeArray<int>(sampleIndices.Length * maxK, Allocator.TempJob);
			var neighbourResults = new NativeArray<int>[jobCount];

			var truePositions = new NativeArray<float3>(meshPoints.Select(p => p.Position).ToArray(), Allocator.TempJob);
			
			var tree = new KDTree(truePositions, 512);
			var handles = new NativeArray<JobHandle>(jobCount, Allocator.TempJob);
			var caches = new KnnQuery.QueryCache[jobCount];

			int scheduleRange = sampleIndices.Length / jobCount;

			for (int t = 0; t < jobCount; ++t) {
				int start = scheduleRange * t;
				int end = t == jobCount - 1 ? sampleIndices.Length : scheduleRange * (t + 1);

				neighbourResults[t] = new NativeArray<int>((end - start) * maxK, Allocator.TempJob);

				var job = new QueryNeighboursJob {
					NeighbourResult = neighbourResults[t],
					PositionsSample = truePositionsSample,
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

			// Write back results to final results array
			for (int t = 0; t < jobCount; ++t) {
				NativeArray<int>.Copy(neighbourResults[t], 0, neighbours, t * scheduleRange * maxK, neighbourResults[t].Length);
				neighbourResults[t].Dispose();
			}

			var pointDensities = new NativeArray<float>(meshPoints.Length, Allocator.TempJob);
			var densityResults = new NativeArray<float>[jobCount];

			scheduleRange = pointDensities.Length / jobCount;

			for (int t = 0; t < jobCount; ++t) {
				int start = scheduleRange * t;
				int end = t == jobCount - 1 ? pointDensities.Length : scheduleRange * (t + 1);
				
				densityResults[t] = new NativeArray<float>(end - start, Allocator.TempJob);

				var job = new QueryDensityJob {
					PointDensitiesResult = densityResults[t],
					Positions = truePositions,
					Tree = tree,
					Start = start,
					Count = end - start,
					Cache = caches[t]
				};

				handles[t] = job.Schedule();
			}

			JobHandle.CompleteAll(handles);

			// Write back results to final results array
			for (int t = 0; t < jobCount; ++t) {
				NativeArray<float>.Copy(densityResults[t], 0, pointDensities, t * scheduleRange, densityResults[t].Length);
				densityResults[t].Dispose();
			}
			
			// All done, dispose memory used for temp results
			tree.Dispose();
			handles.Dispose();
			s_queryNeighboursMarker.End();

			for (int t = 0; t < jobCount; ++t) {
				// Also dispose cache now
				caches[t].Dispose();
			}
			
			var houghTextures = new NativeArray<HoughHistogram>(sampleIndices.Length, Allocator.TempJob);

			using (s_houghTexMarker.Auto()) {
				var random = new Random(23454575);

				var createHoughTexJob = new HoughHistogramsJob {
					Positions = truePositions,
					PointDensities = pointDensities,
					
					TrueNormals = trueNormalsSample,
					Neighbours = neighbours,
					Rand = random,
					HoughHistograms = houghTextures,
				};

				createHoughTexJob.Schedule(sampleIndices.Length, 1024).Complete();
			}

			var trueRoughness = new NativeArray<float>(meshPoints.Select(p => p.Smoothness).ToArray(), Allocator.TempJob);
			float2 trueRoughnessRange = math.float2(mathext.min(trueRoughness), mathext.max(trueRoughness));
			NativeArray<float3> reconstructedNormals;
			NativeArray<float> reconstructedRoughness; 

			if (!UseNeuralNet) {		
				reconstructedNormals = EstimateNormals(houghTextures, trueNormalsSample);
				reconstructedRoughness = EstimateRoughness(houghTextures, trueRoughnessRange);
			}
			else {
				EstimatePropertiesCNN(houghTextures, trueNormalsSample, out reconstructedNormals, out reconstructedRoughness);
			}

			var reconstructionError = new NativeArray<float>(reconstructedNormals.Length, Allocator.TempJob);
			float rms = 0.0f;
			float pgp = 0;

			for (int i = 0; i < reconstructedNormals.Length; ++i) {
				float angle = math.degrees(math.acos(math.dot(reconstructedNormals[i], trueNormalsSample[i])));
				reconstructionError[i] = angle;
				rms += angle * angle;

				if (angle < 8) {
					pgp += 1.0f;
				}
			}

			pgp /= reconstructedNormals.Length;
			rms = math.sqrt(rms / reconstructedNormals.Length);

			Debug.Log($"Finished analyzing normals using {(UseNeuralNet ? "CNN" : "Max bin")}. Total RMS: {rms}, PGP: {pgp}. Time: {sw.ElapsedMilliseconds}ms");

			PointCloudData pointCloudData;
			using (s_saveCloudMarker.Auto()) {
				// Save to TC Point cloud we can render
				Directory.CreateDirectory("Assets/PointCloudCNN/Clouds/");
				string path = "Assets/PointCloudCNN/Clouds/" + name + ".asset";

				if (!File.Exists(path)) {
					pointCloudData = ScriptableObject.CreateInstance<PointCloudData>();
					AssetDatabase.CreateAsset(pointCloudData, path);
				} else {
					pointCloudData = AssetDatabase.LoadAssetAtPath<PointCloudData>(path);
				}

				Color32[] cloudAlbedoValues = new Color32[0];
				var errorGradient = new Gradient();

				// Populate the color keys at the relative time 0 and 1 (0 and 100%)
				var colorKey = new GradientColorKey[2];
				colorKey[0].color = Color.green;
				colorKey[0].time = 0.0f;
				colorKey[1].color = Color.red;
				colorKey[1].time = 1.0f;
				errorGradient.colorKeys = colorKey;

				bool showError = true;

				// Show either albedo color, or reconstruction error
				if (showError) {
					cloudAlbedoValues = reconstructionError.Select(p => {
						float val = math.saturate(math.abs(p) / 60.0f);
						Color col = errorGradient.Evaluate(val);
						return (Color32) col;
					}).ToArray();
				}

				Vector3[] cloudPoints = truePositionsSample.Select(p => new Vector3(p.x, p.y, p.z)).ToArray();
				Vector3[] cloudNormals = reconstructedNormals.Select(v => new Vector3(v.x, v.y, v.z)).ToArray();

				// Encode roughness in color alpha to use in the shader
				for (int i = 0; i < cloudAlbedoValues.Length; ++i) {
					cloudAlbedoValues[i].a = (byte) (reconstructedRoughness[i] * 255);
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
					string imagesPath = "PointCloudCNN/" + folder + "/";
					Directory.CreateDirectory(imagesPath);

					var fileName = new StringBuilder();

					// Lay out K channels next to each other in the channel
					var saveTex = new Texture2D(c_m, c_m * c_scaleLevels);
					var colors = new Color32[c_m * c_m * c_scaleLevels];

					// Write training data to disk
					for (int i = 0; i < sampleIndices.Length; ++i) {
						HoughHistogram testHistogram = houghTextures[i];

						// Normalize each channel and write to PNG
						for (int k = 0; k < c_scaleLevels; ++k) {
							int maxValue = 0;
							for (int y = 0; y < c_m; ++y) {
								for (int x = 0; x < c_m; ++x) {
									maxValue = math.max(maxValue, testHistogram[x, y, k]);
								}
							}

							int texSetOffset = c_m * c_m * k;

							for (int y = 0; y < c_m; ++y) {
								for (int x = 0; x < c_m; ++x) {
									byte level = (byte) math.clamp((int) math.round(255 * testHistogram[x, y, k] / (float) maxValue), 0, 255);
									colors[texSetOffset + x + y * c_m] = new Color32(level, level, level, 255);
								}
							}
						}

						saveTex.SetPixels32(colors);

						fileName.Clear();

						fileName.Append(imagesPath);
						fileName.Append(name);
						fileName.Append(i);
						fileName.Append("_x_");
						float2 setNorm = trueNormalsSample[i].xy;
						fileName.Append(setNorm.x.ToString("0.000"));
						fileName.Append("_y_");
						fileName.Append(setNorm.y.ToString("0.000"));
						fileName.Append("_r_");
						fileName.Append(trueRoughnessSample[i].ToString("0.000"));
						fileName.Append(".png");
						File.WriteAllBytes(fileName.ToString(), saveTex.EncodeToPNG());
					}
				}
			}

			// Release all buffers
			truePositionsSample.Dispose();
			trueNormalsSample.Dispose();
			trueRoughnessSample.Dispose();
			sampleIndices.Dispose();
			neighbours.Dispose();
			houghTextures.Dispose();
			reconstructedNormals.Dispose();
			pointDensities.Dispose();
			meshPoints.Dispose();
			trueRoughness.Dispose();
			reconstructedRoughness.Dispose();
			reconstructionError.Dispose();
			truePositions.Dispose();

			sw.Stop();
			Debug.Log("Done generating point cloud! Took: " + sw.ElapsedMilliseconds + "ms");

			return pointCloudData;
		}

		// Now save out true normals & hough cube in some format that is sensible.
		// Now we have a cube of hough textures, and a normal for each particle!
	}
	
	[BurstCompile(CompileSynchronously = true)]
	struct QueryDensityJob : IJob {
		public NativeArray<float> PointDensitiesResult;

		[ReadOnly] public KDTree Tree;
		[ReadOnly] public NativeArray<float3> Positions;

		public KnnQuery.QueryCache Cache;
		public int Start;
		public int Count;

		public void Execute() {
			for (int i = 0; i < Count; ++i) {
				int index = Start + i;
				float3 pos = Positions[index];

				// Save local density weighting as squared distance to k_dens nearest neighbour
				int kDensIndex = KnnQuery.KNearestLast(Tree, c_kDens, pos, Cache);
				PointDensitiesResult[i] = math.distancesq(pos, Tree.Points[kDensIndex]);
			}
		}
	}
	
	[BurstCompile(CompileSynchronously = true)]
	struct QueryNeighboursJob : IJob {
		public NativeArray<int> NeighbourResult;

		[ReadOnly] public KDTree Tree;
		[ReadOnly] public NativeArray<float3> PositionsSample;

		public int MaxK;

		public KnnQuery.QueryCache Cache;

		public int Start;
		public int Count;

		public void Execute() {
			for (int i = 0; i < Count; ++i) {
				int index = Start + i;
				float3 pos = PositionsSample[index];
				KnnQuery.KNearest(Tree, MaxK, pos, NeighbourResult.Slice(i * MaxK, MaxK), Cache);
			}
		}
	}


	static float3 GetNormalFromPrediction(float2 prediction, float3x3 normalsBasis, float3x3 texBasis, float3 trueNormal) {
		float normalZ = math.sqrt(math.saturate(1.0f - math.lengthsq(prediction)));
		float3 normal = math.normalize(math.float3(prediction, normalZ));

		normal = math.mul(math.transpose(normalsBasis), normal);
		
		// Normally the sign ambiguity in the normal is resolved by facing towards the 
		// eg. Lidar scanner. In this case, since these point clouds do not come from scans
		// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
		if (math.dot(normal, trueNormal) < 0) {
			normal.z *= -1;
		}

		return normal;
	}

	/// <summary>
	/// Job to estimate an array of normals using the max bin method
	/// </summary>
	[BurstCompile(CompileSynchronously = true)]
	struct EstimateNormalsJob : IJobParallelFor {
		[ReadOnly] public NativeArray<HoughHistogram> Textures;
		[ReadOnly] public NativeArray<float3> TrueNormals;

		public NativeArray<float3> Normals;

		public void Execute(int index) {
			float3 trueNormal = TrueNormals[index];
			float3 normalSum = float3.zero;
			var tex = Textures[index];

			// TODO: Count up in one histogram...
			for (int kIndex = 0; kIndex < c_scaleLevels; ++kIndex) {
				int2 maxBin = math.int2(-1, -1);
				int maxBinCount = 0;

				for (int y = 0; y < c_m; ++y) {
					for (int x = 0; x < c_m; ++x) {
						int count = tex[x, y, kIndex];

						if (count > maxBinCount) {
							maxBinCount = count;
							maxBin = math.int2(x, y);
						}
					}
				}

				// M - 1 to compensate for floor() in encoding
				// This way with M=33 we encode cardinal directions exactly!
				float2 normalUv = math.float2(maxBin) / (c_m - 1.0f);
				float2 normalXy = 2.0f * normalUv - 1.0f;

				normalSum += GetNormalFromPrediction(normalXy, tex.NormalsBasis, tex.TexBasis, trueNormal);
			}

			// Write final predicted normal
			Normals[index] = math.normalize(normalSum);
		}
	}

	/// <summary>
	/// Estimate normals for array of hough tex.
	/// </summary>
	static NativeArray<float3> EstimateNormals(NativeArray<HoughHistogram> textures, NativeArray<float3> trueNormals) {
		// First calculate normals for every bin
		var normals = new NativeArray<float3>(textures.Length, Allocator.Persistent);
		var job = new EstimateNormalsJob {
			Textures = textures,
			Normals = normals,
			TrueNormals = trueNormals
		};
		
		job.Schedule(textures.Length, 256).Complete();
		return normals;
	}
	
	static void EstimatePropertiesCNN(NativeArray<HoughHistogram> histograms, NativeArray<float3> trueNormals, out NativeArray<float3> normals, out NativeArray<float> roughness) {
		var graphData = File.ReadAllBytes("PointCloudCNN/saved_models/tf_model.pb");

		using (var graph = new TFGraph()) {
			graph.Import(graphData);
			int normalsCount = histograms.Length / KScales.Length;

			// Return arrays
			normals = new NativeArray<float3>(normalsCount, Allocator.Persistent);
			roughness = new NativeArray<float>(normalsCount, Allocator.Persistent);
			
			const int c_chunkCount = 1000;
			
			// Calculate 1000 points at a time
			// That means our imageTensor is about ~20MB!
			// Doing all points at once just crashes because the allocation is so large...
			int chunks = (int)math.ceil(normalsCount / (float)c_chunkCount);
			float[,,,] imageTensor = new float[c_chunkCount, c_m, c_m, KScales.Length];
			
			// Create network query
			var session = new TFSession(graph);

			for (int chunk = 0; chunk < chunks; ++chunk) {
				int start = chunk * 1000;
				int end = math.min(histograms.Length - 1, (chunk + 1) * 1000);

				int count = end - start;
				
				for (int tensorIndex = 0; tensorIndex < count; ++tensorIndex) {
					int i = tensorIndex + start;
					
					for (int k = 0; k < c_scaleLevels; ++k) {
						var tex = histograms[i * KScales.Length + k];

						float maxVal = 0;

						for (int x = 0; x < c_m * c_m; ++x) {
							for (int y = 0; y < c_m * c_m; ++y) {
								maxVal = math.max(maxVal, tex[x, y, k]);
							}
						}

						for (int y = 0; y < c_m; ++y) {
							for (int x = 0; x < c_m; ++x) {
								imageTensor[tensorIndex, c_m - y - 1, x, k] = tex[x, y, k] / maxVal;
							}
						}
					}
				}
				
				// Run query
				var runner = session.GetRunner();
				var inputNode = graph["input_input"][0];
				var outputNode = graph["output/BiasAdd"][0]; // Keras for some reason splits output node in a few sub-nodes. Grab final value after bias add
				runner.AddInput(inputNode, imageTensor);
				runner.Fetch(outputNode);
				var output = runner.Run();

				// Fetch the results from output as 2D tensor
				float[,] predictions = (float[,]) output[0].GetValue();
			
				for (int tensorIndex = 0; tensorIndex < count; ++tensorIndex) {
					int i = start + tensorIndex;
					var tex = histograms[i];
					
					float2 predNormal = math.float2(predictions[tensorIndex, 0], predictions[tensorIndex, 1]);
					
					normals[i] = GetNormalFromPrediction(predNormal, tex.NormalsBasis, tex.TexBasis, trueNormals[i]);
					roughness[i] = predictions[tensorIndex, 2];
				}
			}
		}
	}
	
	/// <summary>
	/// Estimate roughness for a list of hough textures
	/// </summary>
	[BurstCompile(CompileSynchronously = true)]
	struct EstimateRoughnessJob : IJobParallelFor {
		[ReadOnly] public NativeArray<HoughHistogram> Textures;

		public NativeArray<float> Roughness;
		public int ScaleLevels;

		public void Execute(int index) {
			float roughnessSum = 0.0f;
			var tex = Textures[index];
			
			for (int kIndex = 0; kIndex < ScaleLevels; ++kIndex) {
				roughnessSum += EstimateRoughness(tex, kIndex);
			}

			// Return final averaged roughness
			Roughness[index] = roughnessSum / ScaleLevels;
		}
	}

	static NativeArray<float> EstimateRoughness(NativeArray<HoughHistogram> textures, float2 roughnessScale) {
		// First calculate normals for every bin
		var roughness = new NativeArray<float>(textures.Length, Allocator.Persistent);
		var job = new EstimateRoughnessJob {
			Textures = textures,
			Roughness = roughness,
		};
		job.Schedule(textures.Length, 256).Complete();

		// Remap to specified roughness range
		float2 estimationScale = math.float2(mathext.min(roughness), mathext.max(roughness));

		for (int i = 0; i < roughness.Length; ++i) {
			float t = (roughness[i] - estimationScale.x) / (estimationScale.y - estimationScale.x);
			roughness[i] = math.lerp(roughnessScale.x, roughnessScale.y, t);
		}

		return roughness;
	}


}