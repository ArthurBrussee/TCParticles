using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace DataStructures.ViliWonka.KDTree {
	public struct KDTree {
		[ReadOnly] public NativeArray<float3> Points;
		[ReadOnly] public NativeArray<KDNode> Nodes;
		[ReadOnly] public NativeArray<int> Permutation;

		public int Count;

		int kdNodesCount;
		int maxPointsPerLeafNode;
		int m_rootNodeIndex;

		public KDNode RootNode => Nodes[m_rootNodeIndex];
		
		public KDTree(NativeArray<float3> points, int maxPointsPerLeafNode = 16) {
			int nodeCountEstimate = 4 * (int)math.ceil(points.Length / (float) maxPointsPerLeafNode) + 1;
			Nodes = new NativeArray<KDNode>(nodeCountEstimate, Allocator.Persistent);
			
			
			Permutation = new NativeArray<int>(points.Length, Allocator.Persistent);
			Points = points;

			Count = points.Length;
			kdNodesCount = 0;
			m_rootNodeIndex = -1;

			this.maxPointsPerLeafNode = maxPointsPerLeafNode;

			for (int i = 0; i < Count; i++) {
				Permutation[i] = i;
			}

			kdNodesCount = 0;
			m_rootNodeIndex = GetKdNode(MakeBounds(), 0, Count);
			SplitNode(m_rootNodeIndex);
		}
	
		public void Release() {
			Permutation.Dispose();
			Nodes.Dispose();
		}

		public void SetCount(int newSize) {
			Count = newSize;

			// upsize internal arrays
			if (Count > Points.Length) {
				var newPoints = new NativeArray<float3>(newSize, Allocator.Persistent);
				NativeArray<float3>.Copy(Points, newPoints, Points.Length);
				Points = newPoints;
				
				var newPerm = new NativeArray<int>(newSize, Allocator.Persistent);
				NativeArray<int>.Copy(Permutation, newPerm, newPerm.Length);
				Permutation = newPerm;
			}
		}
		
		int GetKdNode(KDBounds bounds, int start, int end) {
			KDNode node;
			node.bounds = bounds;
			node.start = start;
			node.end = end;
			node.Index = kdNodesCount;
			node.partitionAxis = -1;
			node.partitionCoordinate = 0.0f;
			node.PositiveChildIndex = -1;
			node.NegativeChildIndex = -1;
			Nodes[kdNodesCount] = node;
			kdNodesCount++;
			return kdNodesCount - 1;
		}

		/// <summary>
		/// For calculating root node bounds
		/// </summary>
		/// <returns>Boundary of all Vector3 points</returns>
		KDBounds MakeBounds() {
			var max = new float3(float.MinValue, float.MinValue, float.MinValue);
			var min = new float3(float.MaxValue, float.MaxValue, float.MaxValue);

			int even = Count & ~1; // calculate even Length

			// min, max calculations
			// 3n/2 calculations instead of 2n
			for (int i0 = 0; i0 < even; i0 += 2) {
				int i1 = i0 + 1;

				// X Coords
				if (Points[i0].x > Points[i1].x) {
					// i0 is bigger, i1 is smaller
					if (Points[i1].x < min.x) {
						min.x = Points[i1].x;
					}

					if (Points[i0].x > max.x) {
						max.x = Points[i0].x;
					}
				} else {
					// i1 is smaller, i0 is bigger
					if (Points[i0].x < min.x) {
						min.x = Points[i0].x;
					}

					if (Points[i1].x > max.x) {
						max.x = Points[i1].x;
					}
				}

				// Y Coords
				if (Points[i0].y > Points[i1].y) {
					// i0 is bigger, i1 is smaller
					if (Points[i1].y < min.y) {
						min.y = Points[i1].y;
					}

					if (Points[i0].y > max.y) {
						max.y = Points[i0].y;
					}
				} else {
					// i1 is smaller, i0 is bigger
					if (Points[i0].y < min.y) {
						min.y = Points[i0].y;
					}

					if (Points[i1].y > max.y) {
						max.y = Points[i1].y;
					}
				}

				// Z Coords
				if (Points[i0].z > Points[i1].z) {
					// i0 is bigger, i1 is smaller
					if (Points[i1].z < min.z) {
						min.z = Points[i1].z;
					}

					if (Points[i0].z > max.z) {
						max.z = Points[i0].z;
					}
				} else {
					// i1 is smaller, i0 is bigger
					if (Points[i0].z < min.z) {
						min.z = Points[i0].z;
					}

					if (Points[i1].z > max.z) {
						max.z = Points[i1].z;
					}
				}
			}

			// if array was odd, calculate also min/max for the last element
			if (even != Count) {
				// X
				if (min.x > Points[even].x) {
					min.x = Points[even].x;
				}

				if (max.x < Points[even].x) {
					max.x = Points[even].x;
				}

				// Y
				if (min.y > Points[even].y) {
					min.y = Points[even].y;
				}

				if (max.y < Points[even].y) {
					max.y = Points[even].y;
				}

				// Z
				if (min.z > Points[even].z) {
					min.z = Points[even].z;
				}

				if (max.z < Points[even].z) {
					max.z = Points[even].z;
				}
			}

			var b = new KDBounds();
			b.min = min;
			b.max = max;
			return b;
		}

		/// <summary>
		/// Recursive splitting procedure
		/// </summary>
		/// <param name="parent">This is where root node goes</param>
		/// <param name="depth"></param>
		///
		void SplitNode(int parentIndex) {
			var parent = Nodes[parentIndex];

			// center of bounding box
			KDBounds parentBounds = parent.bounds;
			Vector3 parentBoundsSize = parentBounds.size;

			// Find axis where bounds are largest
			int splitAxis = 0;
			float axisSize = parentBoundsSize.x;

			if (axisSize < parentBoundsSize.y) {
				splitAxis = 1;
				axisSize = parentBoundsSize.y;
			}

			if (axisSize < parentBoundsSize.z) {
				splitAxis = 2;
			}

			// Our axis min-max bounds
			float boundsStart = parentBounds.min[splitAxis];
			float boundsEnd = parentBounds.max[splitAxis];

			// Calculate the spliting coords
			float splitPivot = CalculatePivot(parent.start, parent.end, boundsStart, boundsEnd, splitAxis);

			parent.partitionAxis = splitAxis;
			parent.partitionCoordinate = splitPivot;

			// 'Spliting' array to two subarrays
			int splittingIndex = Partition(parent.start, parent.end, splitPivot, splitAxis);

			// Negative / Left node
			float3 negMax = parentBounds.max;
			negMax[splitAxis] = splitPivot;

			var bounds = parentBounds;
			bounds.max = negMax;
			int negNodeIndex = GetKdNode(bounds, parent.start, splittingIndex);

			// Positive / Right node
			float3 posMin = parentBounds.min;
			posMin[splitAxis] = splitPivot;

			bounds = parentBounds;
			bounds.min = posMin;
			int posNodeIndex = GetKdNode(bounds, splittingIndex, parent.end);

			parent.NegativeChildIndex = negNodeIndex;
			parent.PositiveChildIndex = posNodeIndex;

			// Write back!
			Nodes[parentIndex] = parent;

			// Constraint function deciding if split should be continued
			if (Nodes[negNodeIndex].Count > maxPointsPerLeafNode) {
				SplitNode(negNodeIndex);
			}

			if (Nodes[posNodeIndex].Count > maxPointsPerLeafNode) {
				SplitNode(posNodeIndex);
			}
		}

		/// <summary>
		/// Sliding midpoint splitting pivot calculation
		/// 1. First splits node to two equal parts (midPoint)
		/// 2. Checks if elements are in both sides of splitted bounds
		/// 3a. If they are, just return midPoint
		/// 3b. If they are not, then points are only on left or right bound.
		/// 4. Move the splitting pivot so that it shrinks part with points completely (calculate min or max dependent) and return.
		/// </summary>
		/// <param name="start"></param>
		/// <param name="end"></param>
		/// <param name="boundsStart"></param>
		/// <param name="boundsEnd"></param>
		/// <param name="axis"></param>
		/// <returns></returns>
		float CalculatePivot(int start, int end, float boundsStart, float boundsEnd, int axis) {
			//! sliding midpoint rule
			float midPoint = (boundsStart + boundsEnd) / 2f;

			bool negative = false;
			bool positive = false;

			float negMax = float.MinValue;
			float posMin = float.MaxValue;

			// this for loop section is used both for sorted and unsorted data
			for (int i = start; i < end; i++) {
				if (Points[Permutation[i]][axis] < midPoint) {
					negative = true;
				} else {
					positive = true;
				}

				if (negative && positive) {
					return midPoint;
				}
			}

			if (negative) {
				for (int i = start; i < end; i++) {
					if (negMax < Points[Permutation[i]][axis]) {
						negMax = Points[Permutation[i]][axis];
					}
				}

				return negMax;
			}

			for (int i = start; i < end; i++) {
				if (posMin > Points[Permutation[i]][axis]) {
					posMin = Points[Permutation[i]][axis];
				}
			}

			return posMin;
		}

		/// <summary>
		/// Similar to Hoare partitioning algorithm (used in Quick Sort)
		/// Modification: pivot is not left-most element but is instead argument of function
		/// Calculates splitting index and partially sorts elements (swaps them until they are on correct side - depending on pivot)
		/// Complexity: O(n)
		/// </summary>
		/// <param name="start">Start index</param>
		/// <param name="end">End index</param>
		/// <param name="partitionPivot">Pivot that decides boundary between left and right</param>
		/// <param name="axis">Axis of this pivoting</param>
		/// <returns>
		/// Returns splitting index that subdivides array into 2 smaller arrays
		/// left = [start, pivot),
		/// right = [pivot, end)
		/// </returns>
		int Partition(int start, int end, float partitionPivot, int axis) {
			// note: increasing right pointer is actually decreasing!
			int LP = start - 1; // left pointer (negative side)
			int RP = end; // right pointer (positive side)

			int temp; // temporary var for swapping permutation indexes

			while (true) {
				do {
					// move from left to the right until "out of bounds" value is found
					LP++;
				} while (LP < RP && Points[Permutation[LP]][axis] < partitionPivot);

				do {
					// move from right to the left until "out of bounds" value found
					RP--;
				} while (LP < RP && Points[Permutation[RP]][axis] >= partitionPivot);

				if (LP < RP) {
					// swap
					temp = Permutation[LP];
					Permutation[LP] = Permutation[RP];
					Permutation[RP] = temp;
				} else {
					return LP;
				}
			}
		}
	}
}