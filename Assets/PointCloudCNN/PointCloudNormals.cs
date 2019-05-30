using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataStructures.ViliWonka.KDTree;
using TC;
using TensorFlow;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

/// <summary>
/// Main class to handle generation of the point cloud normals / training data
/// </summary>
public static class PointCloudNormals {
	// Hyper parameters, see report for more details
	const int M = 33;
	const int KDens = 5;

	// const float Alpha = 0.95f;
	// const float Epsilon = 0.073f;
	// Could be calculated from Alpha & Epsilon above but better to just fix it here
	const int Hypotheses = 1000;

	// Base number of k-neighbourhood to consider
	const int BaseK = 100;

	const int ScaleLevels = 5;

	// K levels to use at different scales
	// Since all the scale maps for 1 & 3 are also generated when generating 5 scales
	// It is easier to just read out those than to re-generate data
	// static readonly int[] KScales1 = {c_baseK};
	// static readonly int[] KScales3 = {c_baseK / 2, c_baseK, c_baseK * 2};
	static readonly int[] KScales = {BaseK / 4, BaseK / 2, BaseK, BaseK * 2, BaseK * 4};

	static ProfilerMarker s_generateDataMarker;
	static ProfilerMarker s_queryNeighboursMarker;
	static ProfilerMarker s_houghTexMarker;
	static ProfilerMarker s_saveCloudMarker;

	/// <summary>
	/// Hough histogram data. Uses a byte to hold accumulation value. This is fine for a low number of Hypotheses (H much smaller than M * M *255)
	/// </summary>
	unsafe struct HoughHistogram {
		public fixed byte Counts[M * M * ScaleLevels];

		public float3x3 NormalsBasis;
		public float3x3 TexBasis;

		public void GetScaledHisto(float[] space) {
			// Scale each layer seperately
			for (int k = 0; k < ScaleLevels; ++k) {
				// Count up total tex weights
				float maxVal = 0.0f;

				for (int index = 0; index < M * M; ++index) {
					int offsetIndex = k * M * M + index;
					maxVal = math.max(maxVal, Counts[offsetIndex]);
				}

				for (int index = 0; index < M * M; ++index) {
					int offsetIndex = k * M * M + index;
					space[offsetIndex] = Counts[offsetIndex] / maxVal;
				}
			}
		}
	}

	/// <summary>
	/// Main job for generating the hough histograms
	/// </summary>
	[BurstCompile(CompileSynchronously = true)]
	unsafe struct HoughHistogramsJob : IJobParallelFor {
		[ReadOnly] public NativeArray<float3> Positions;
		[ReadOnly] public NativeArray<float> PointDensities;
		[ReadOnly] public NativeArray<int> Neighbours;
		[ReadOnly] public NativeArray<float3> TrueNormals;

		[NativeDisableParallelForRestriction]
		public NativeArray<HoughHistogram> HoughHistograms;

		public Random Rand;

		int PickWeightedRandomVal(float* a, int kVal) {
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

		/// <summary>
		/// Try get normal from 3 points. Fails if points are co-linear
		/// </summary>
		bool TryGetNormal(int p0, int p1, int p2, out float3 normal) {
			normal = math.cross(Positions[p2] - Positions[p0], Positions[p1] - Positions[p0]);

			// If we pick bad points or co-tangent points, we end up with a bad 
			// 0 length normal... can't use this, discard this hypothesis and try again
			if (math.lengthsq(normal) < math.FLT_MIN_NORMAL) {
				return false;
			}

			// Success!
			normal = math.normalize(normal);
			return true;
		}

		// Run job, calculate multi-scale texture for a point
		public void Execute(int index) {
			int kMax = KScales[ScaleLevels - 1];
			int neighbourBaseOffset = index * kMax;

			
			// First, determine PCA basis
			// We do this for one scale - our medium scale
			const int c_kBasisIndex = 2;
			
			// Pick neighbours up to kVal
			int kVal = KScales[c_kBasisIndex];

			// Construct covariance matrix
			float3x3 posCovariance = float3x3.identity;

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

			// Eigen decompose the covariance matrix. This gives 3 eigen values in D
			// And eigen basis. Faster than a full blown PCA solver
			mathext.eigendecompose(posCovariance, out float3x3 pcaBasis, out float3 _);

			// Store all 2D hypothesized normals in hough space
			float2* houghNormals = stackalloc float2[Hypotheses];
			
			float totalDensity = 0.0f;

			// Count up probabilities
			float* cumProb = stackalloc float[kMax];
			for (int i = 0; i < kMax; ++i) {
				int neighbourIndex = Neighbours[neighbourBaseOffset + i];
				totalDensity += PointDensities[neighbourIndex];
				cumProb[i] = totalDensity;
			}
			
			int hypothesesCompleted = 0;
			while (hypothesesCompleted < Hypotheses) {
				// Pick 3 points, weighted by the local density
				int n0 = PickWeightedRandomVal(cumProb, kVal);
				int n1 = PickWeightedRandomVal(cumProb, kVal);
				int n2 = PickWeightedRandomVal(cumProb, kVal);
				
				int p0 = Neighbours[neighbourBaseOffset + n0];
				int p1 = Neighbours[neighbourBaseOffset + n1];
				int p2 = Neighbours[neighbourBaseOffset + n2];

				if (!TryGetNormal(p0, p1, p2, out float3 hypNormal)) {
					continue;
				}

				// Normally the sign ambiguity in the normal is resolved by facing towards the 
				// eg. Lidar scanner. In this case, since these point clouds do not come from scans
				// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
				if (math.dot(TrueNormals[index], hypNormal) < 0) {
					hypNormal = -hypNormal;
				}

				houghNormals[hypothesesCompleted] = math.mul(pcaBasis, hypNormal).xy;
				hypothesesCompleted++;
			}

			// Now do 2D PCA for the hough spaced normals
			float3x3 texCov = float3x3.identity;

			float2 meanTex = float2.zero;
			for (int i = 0; i < Hypotheses; ++i) {
				meanTex += houghNormals[i];
			}

			// Average 2D hough position
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
			texCov[2][2] = math.FLT_MIN_NORMAL; // ensure z has smallest eigen value, so we only rotate xy
			mathext.eigendecompose(texCov, out float3x3 pcaBasisTex, out float3 _);

			// Get a pointer to the current hough histogram
			ref HoughHistogram histogram = ref UnsafeUtilityEx.ArrayElementAsRef<HoughHistogram>(HoughHistograms.GetUnsafePtr(), index);
			
			// Setup the basis
			histogram.NormalsBasis = pcaBasis;
			histogram.TexBasis = pcaBasisTex;
			
			// Generate a texture at each scale
			for (int kIndex = 0; kIndex < ScaleLevels; ++kIndex) {
				// Pick neighbours up to kVal
				kVal = KScales[kIndex];
				hypothesesCompleted = 0;

				while (hypothesesCompleted < Hypotheses) {
					// Pick 3 points, weighted by the local density
					int n0 = PickWeightedRandomVal(cumProb, kVal);
					int n1 = PickWeightedRandomVal(cumProb, kVal);
					int n2 = PickWeightedRandomVal(cumProb, kVal);
					
					int p0 = Neighbours[neighbourBaseOffset + n0];
					int p1 = Neighbours[neighbourBaseOffset + n1];
					int p2 = Neighbours[neighbourBaseOffset + n2];

					if (!TryGetNormal(p0, p1, p2, out float3 hypNormal)) {
						continue;
					}

					hypothesesCompleted++;

					// Normally the sign ambiguity in the normal is resolved by facing towards the 
					// eg. Lidar scanner. In this case, since these point clouds do not come from scans
					// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
					if (math.dot(TrueNormals[index], hypNormal) < 0) {
						hypNormal = -hypNormal;
					}

					float2 localHough = math.mul(pcaBasisTex, math.mul(pcaBasis, hypNormal)).xy;

					float2 uv = (localHough + 1.0f) / 2.0f;
					int2 histogramPos = math.int2(math.floor(uv * M));

					// When either x or y is exactly 1 the floor returns M which is out of bounds 
					// So clamp tex coordinates
					histogramPos = math.clamp(histogramPos, 0, M - 1);

					// Now vote in the histogram
					int histoIndex = histogramPos.x + histogramPos.y * M + kIndex * M * M;
					
					// Clamp so we don't overflow (in rare cases)
					if (histogram.Counts[histoIndex] < byte.MaxValue) {
						histogram.Counts[histoIndex]++;
					}
				}
			}
		}
	}

	
	/// <summary>
	/// Generate training data and visualize as TC Cloud
	/// </summary>
	public static PointCloudData GenerateTrainingData(NativeArray<MeshPoint> meshPoints, float sampleRate, string folder, string name, bool writeData, bool useNeuralNet, bool showError) {
		// Performance measure markers
		s_generateDataMarker = new ProfilerMarker("GenerateDataMarker");
		s_queryNeighboursMarker = new ProfilerMarker("QueryNeighbours");
		s_houghTexMarker = new ProfilerMarker("HoughTexture");
		s_saveCloudMarker = new ProfilerMarker("SaveCloud");
		
		var sw = new Stopwatch();
		sw.Start();

		using (s_generateDataMarker.Auto()) {
			int maxK = KScales[ScaleLevels - 1];
			
			// Pick what particles we're going to actually calculate normals for
			var sampleIndices = new NativeList<int>(Allocator.TempJob);
			var rand = new Random(8976543);

			for (int i = 0; i < meshPoints.Length; ++i) {
				if (rand.NextFloat() <= sampleRate) {
					sampleIndices.Add(i);
				}
			}

			// Get properties of sampled particles
			var truePositionsSample = new NativeArray<float3>(sampleIndices.Length, Allocator.TempJob);
			var trueNormalsSample = new NativeArray<float3>(sampleIndices.Length, Allocator.TempJob);
			var trueSmoothnessSample = new NativeArray<float>(sampleIndices.Length, Allocator.TempJob);
			var trueAlbedoSample = new NativeArray<Color32>(sampleIndices.Length, Allocator.TempJob);

			for (int i = 0; i < sampleIndices.Length; ++i) {
				int index = sampleIndices[i];
				truePositionsSample[i] = meshPoints[index].Position;
				trueNormalsSample[i] = meshPoints[index].Normal;
				trueSmoothnessSample[i] = meshPoints[index].Smoothness;
				trueAlbedoSample[i] = meshPoints[index].Albedo;
			}

			s_queryNeighboursMarker.Begin();

			// Schedule 8 jobs to query all neighbourhoods. For each point we save the maximum number of neighbours we need.
			// Since these are sorted by distance, we also implicitly know smaller neighbourhoods
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
				caches[t] = KnnQuery.GetQueryCache(tree, maxK);

				var job = new QueryNeighboursJob {
					NeighbourResult = neighbourResults[t],
					PositionsSample = truePositionsSample,
					MaxK = maxK,
					Tree = tree,
					Start = start,
					Count = end - start,
					Cache = caches[t]
				};

				handles[t] = job.Schedule();
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

			// Wait until we have all neighbours
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


			// Now construct all hough histograms
			var houghTextures = new NativeArray<HoughHistogram>(sampleIndices.Length, Allocator.TempJob);

			using (s_houghTexMarker.Auto()) {
				var random = new Random(23454575);

				// Schedule job to create hough histograms
				var createHoughTexJob = new HoughHistogramsJob {
					Positions = truePositions,
					PointDensities = pointDensities,

					TrueNormals = trueNormalsSample,
					Neighbours = neighbours,
					Rand = random,
					HoughHistograms = houghTextures,
				};
				
				createHoughTexJob.Schedule(houghTextures.Length, 256).Complete();
			}

			// Now that we have the hough histograms,
			// we can estimate normals & roughnesses!
			
			// Normally, the user would pick a range for the smoothness to be remapped too
			// Since the scale we estimate is arbitrary
			// In this case however, we just use the actual range of the object
			var trueSmoothness = new NativeArray<float>(meshPoints.Select(p => p.Smoothness).ToArray(), Allocator.TempJob);
			float2 trueSmoothnessRange = math.float2(mathext.min(trueSmoothness), mathext.max(trueSmoothness));
			
			NativeArray<float3> reconstructedNormals;
			NativeArray<float> reconstructedRoughness;

			if (!useNeuralNet) {
				// Use classical methods
				reconstructedNormals = EstimateNormals(houghTextures, trueNormalsSample);
				reconstructedRoughness = EstimateSmoothness(houghTextures, trueSmoothnessRange);
			}
			else {
				// Pass to CNN!
				EstimatePropertiesCnn(houghTextures, trueNormalsSample, out reconstructedNormals, out reconstructedRoughness);
			}

			// Ok we have our properties -> Measure how well we did...
			
			// Measure RMS & PGP per particle
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

			string methodName = (useNeuralNet ? "CNN" : "MaxBin");
			Debug.Log($"{name} finished using {methodName}. Total RMS: {rms}, PGP: {pgp}. Time: {sw.ElapsedMilliseconds}ms");

			
			// Write CSV with results for later processing
			string[] cur = new string[0];
			string resultsFileName = "PointCloudCNN/results.csv";
			
			if (File.Exists(resultsFileName)) {
				cur = File.ReadAllLines(resultsFileName);
			}
			cur = cur.Append($"{name}, {methodName}, {rms}, {pgp}").ToArray();
			File.WriteAllLines(resultsFileName, cur);
			
			
			// TC Particles point cloud data for visualization
			PointCloudData pointCloudData = ScriptableObject.CreateInstance<PointCloudData>();
			
			using (s_saveCloudMarker.Auto()) {
				Color32[] cloudAlbedoValues;

				// Show either albedo color, or reconstruction error
				if (showError) {
					var errorGradient = new Gradient();

					// Populate the color keys at the relative time 0 and 1 (0 and 100%)
					var colorKey = new GradientColorKey[2];
					colorKey[0].color = Color.green;
					colorKey[0].time = 0.0f;
					colorKey[1].color = Color.red;
					colorKey[1].time = 1.0f;
					errorGradient.colorKeys = colorKey;
					
					cloudAlbedoValues = reconstructionError.Select(p => {
						float val = math.saturate(math.abs(p) / 60.0f);
						Color col = errorGradient.Evaluate(val);
						return (Color32) col;
					}).ToArray();
				}
				else {
					cloudAlbedoValues = trueAlbedoSample.ToArray();
				}

				Vector3[] cloudPoints = truePositionsSample.Select(p => new Vector3(p.x, p.y, p.z)).ToArray();
				Vector3[] cloudNormals = reconstructedNormals.Select(v => new Vector3(v.x, v.y, v.z)).ToArray();

				// Encode roughness in color alpha to use in the shader
				for (int i = 0; i < cloudAlbedoValues.Length; ++i) {
					cloudAlbedoValues[i].a = (byte) (reconstructedRoughness[i] * 255);
				}

				// Write data to TC Particles 
				pointCloudData.Initialize(
					cloudPoints,
					cloudNormals,
					cloudAlbedoValues,
					1.0f,
					Vector3.zero,
					Vector3.zero
				);

				// Write hough textures to disk if requested
				if (writeData) {
					string imagesPath = "PointCloudCNN/" + folder + "/";
					Directory.CreateDirectory(imagesPath);

					var fileName = new StringBuilder();

					// Lay out K channels next to each other in the channel
					var saveTex = new Texture2D(M, M * ScaleLevels);
					var colors = new Color32[M * M * ScaleLevels];

					float[] scratchSpace = new float[M * M * ScaleLevels];

					// Write training data to disk
					for (int i = 0; i < sampleIndices.Length; ++i) {
						HoughHistogram testHistogram = houghTextures[i];
						
						// Get scaled histogram
						testHistogram.GetScaledHisto(scratchSpace);

						// Write to color array
						for (int index = 0; index < ScaleLevels; ++index) {
							byte level = (byte) math.clamp((int) math.round(255 * scratchSpace[index]), 0, 255);
							colors[index] = new Color32(level, level, level, 255);
						}

						saveTex.SetPixels32(colors);


						// Write PNG to disk
						fileName.Clear();
						fileName.Append(imagesPath);
						fileName.Append(name);
						fileName.Append(i);
						fileName.Append("_x_");
						// Get ground truth normal in hough texture space 
						float2 setNorm = math.mul(testHistogram.TexBasis, math.mul(testHistogram.NormalsBasis, trueNormalsSample[i])).xy;
						fileName.Append(setNorm.x.ToString("0.000"));
						fileName.Append("_y_");
						fileName.Append(setNorm.y.ToString("0.000"));
						fileName.Append("_r_");
						fileName.Append(trueSmoothnessSample[i].ToString("0.000"));
						fileName.Append(".png");
						File.WriteAllBytes(fileName.ToString(), saveTex.EncodeToPNG());
					}
				}
			}

			// Release all memory we used in the process
			truePositionsSample.Dispose();
			trueNormalsSample.Dispose();
			trueSmoothnessSample.Dispose();
			sampleIndices.Dispose();
			neighbours.Dispose();
			houghTextures.Dispose();
			reconstructedNormals.Dispose();
			pointDensities.Dispose();
			meshPoints.Dispose();
			trueSmoothness.Dispose();
			reconstructedRoughness.Dispose();
			reconstructionError.Dispose();
			truePositions.Dispose();

			sw.Stop();
			return pointCloudData;
		}
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
				int kDensIndex = KnnQuery.KNearestLast(Tree, KDens, pos, Cache);
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

		float3 trueNormalBasis = math.mul(texBasis, math.mul(normalsBasis, trueNormal));
		
		// Normally the sign ambiguity in the normal is resolved by facing towards the 
		// eg. Lidar scanner. In this case, since these point clouds do not come from scans
		// We cheat a little and resolve the ambiguity by instead just comparing to ground-truth
		if (math.dot(normal, trueNormalBasis) < 0) {
			normal.z *= -1;
		}

		float3x3 invTex = math.transpose(texBasis);
		float3x3 invNormals = math.transpose(normalsBasis);
		normal = math.mul(invNormals, math.mul(invTex, normal));

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
			for (int kIndex = 0; kIndex < ScaleLevels; ++kIndex) {
				int2 maxBin = math.int2(-1, -1);
				int maxBinCount = 0;

				for (int y = 0; y < M; ++y) {
					for (int x = 0; x < M; ++x) {
						unsafe {
							int count = tex.Counts[x + y * M + kIndex * M * M];

							if (count > maxBinCount) {
								maxBinCount = count;
								maxBin = math.int2(x, y);
							}
						}
					}
				}

				// M - 1 to compensate for floor() in encoding
				// This way with M=33 we encode cardinal directions exactly!
				float2 normalUv = math.float2(maxBin) / (M - 1.0f);
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
	
	// <summary>
	/// Estimate roughness as the determinant of the covariance matrix in the hough texture
	/// This is roughly equal to fitting a normal to the hough texture
	/// </summary>
	static float EstimateRoughness(HoughHistogram histogram, int kLevel) {
		float2 meanPos = float2.zero;
		int totalPoints = 0;

		// Count up total tex weights
		for (int y = 0; y < M; ++y) {
			for (int x = 0; x < M; ++x) {
				float2 pos = math.float2(x, y) / M;
				
				unsafe {
					int count = histogram.Counts[x + y * M + kLevel * M * M];
					totalPoints += count;
					meanPos += count * pos;
				}
			}
		}

		// Estimate covariance
		meanPos /= totalPoints;
		float2x2 covariance = float2x2.identity;

		for (int dimi = 0; dimi < 2; ++dimi) {
			for (int dimj = 0; dimj < 2; ++dimj) {
				float cov = 0;

				for (int y = 0; y < M; ++y) {
					for (int x = 0; x < M; ++x) {
						float2 pos = math.float2(x, y) / M;
						unsafe {
							cov += histogram.Counts[x + y * M + kLevel * M * M] * (pos[dimi] - meanPos[dimi]) * (pos[dimj] - meanPos[dimj]);
						}
					}
				}

				covariance[dimi][dimj] = math.clamp(cov, 0, 1000000);
			}
		}
		
		// Anistropic roughness ~ lambda_1, lambda_2
		// Isotropic roughness we can estimate with the determinant (== lambda_1 * lambda_2)
		float det = math.determinant(covariance);
		return det;
	}

	/// <summary>
	/// Estimate smoothness for all points (job)
	/// </summary>
	[BurstCompile(CompileSynchronously = true)]
	struct EstimateRoughnessJob : IJobParallelFor {
		[ReadOnly] public NativeArray<HoughHistogram> Textures;
		public NativeArray<float> Roughness;

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

	/// <summary>
	/// Estimate smoothness for all points
	/// </summary>
	static NativeArray<float> EstimateSmoothness(NativeArray<HoughHistogram> textures, float2 smoothnessScale) {
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
			float t = 1.0f - (roughness[i] - estimationScale.x) / (estimationScale.y - estimationScale.x);
			roughness[i] = math.lerp(smoothnessScale.x, smoothnessScale.y, t);
		}

		return roughness;
	}
	

	/// <summary>
	/// Estimate normals & roughness using the trained CNN
	/// </summary>
	static void EstimatePropertiesCnn(NativeArray<HoughHistogram> histograms, NativeArray<float3> trueNormals, out NativeArray<float3> normals, out NativeArray<float> roughness) {
	
		// Construct tensofrflow graph
		var graphData = File.ReadAllBytes("PointCloudCNN/saved_models/tf_model.pb");

		using (var graph = new TFGraph()) {
			graph.Import(graphData);

			// Return arrays
			var normalsRet = new NativeArray<float3>(histograms.Length, Allocator.Persistent);
			var roughnessRet = new NativeArray<float>(histograms.Length, Allocator.Persistent);

			const int c_chunkCount = 500;

			// Calculate 1000 points at a time
			// That means our imageTensor is about ~20MB!
			// Doing all points at once just crashes because the allocation is so large...
			int chunks = (int) math.ceil(histograms.Length / (float) c_chunkCount);
			float[,,,] imageTensor = new float[c_chunkCount, M, M, KScales.Length];

			Profiler.BeginSample("Construct session");
			// Create network query
			var session = new TFSession(graph);
			Profiler.EndSample();

			for(int chunk = 0; chunk < chunks; ++chunk) {
				Profiler.BeginSample("Construct tensor");
				int start = chunk * c_chunkCount;
				int end = math.min(histograms.Length, (chunk + 1) * c_chunkCount);
				int count = end - start;

				// Write our histograms to the image tensor
				Parallel.For(0, count, tensorIndex => {
					float[] histoSpace = new float[M * M * ScaleLevels];

					int i = tensorIndex + start;
					histograms[i].GetScaledHisto(histoSpace);

					for (int k = 0; k < ScaleLevels; ++k) {
						for (int y = 0; y < M; ++y) {
							for (int x = 0; x < M; ++x) {
								imageTensor[tensorIndex, M - y - 1, x, k] = histoSpace[x + y * M + k * M * M];
							}
						}
					}
				});

				Profiler.EndSample();


				// Now run query in tensorflow
				Profiler.BeginSample("Run TF Query");
				var runner = session.GetRunner();
				var inputNode = graph["input_input"][0];
				var outputNode = graph["output/BiasAdd"][0]; // Keras for some reason splits output node in a few sub-nodes. Grab final output value after bias add
				runner.AddInput(inputNode, imageTensor);
				runner.Fetch(outputNode);
				var output = runner.Run();
				Profiler.EndSample();


				// Write to results array
				Profiler.BeginSample("Write results");
				// Fetch the results from output as 2D tensor
				float[,] predictions = (float[,]) output[0].GetValue();

				for (int tensorIndex = 0; tensorIndex < count; ++tensorIndex) {
					int i = start + tensorIndex;
					var tex = histograms[i];

					float2 predNormal = math.float2(predictions[tensorIndex, 0], predictions[tensorIndex, 1]);
					normalsRet[i] = GetNormalFromPrediction(predNormal, tex.NormalsBasis, tex.TexBasis, trueNormals[i]);
					roughnessRet[i] = predictions[tensorIndex, 2];
				}
				Profiler.EndSample();
			}

			normals = normalsRet;
			roughness = roughnessRet;
		}
	}
}