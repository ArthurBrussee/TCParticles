/*MIT License

Copyright(c) 2018 Vili Volčini / viliwonka

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using DataStructures.ViliWonka.Heap;
using Unity.Collections;
using Unity.Mathematics;

namespace DataStructures.ViliWonka.KDTree {
	public static class KnnQuery {
		public struct QueryCache : IDisposable {
			public int Count;
			public KSmallestHeap Heap;
			public MinHeap MinHeap;
			public NativeArray<KdQueryNode> QueueArray;

			public void Dispose() {
				Heap.Dispose();
				MinHeap.Dispose();
				QueueArray.Dispose();
			}

			public void Reset() {
				Count = 0;
				Heap.Clear();
				MinHeap.Clear();
			}
		}

		static void PushToHeap(KDNode node, float3 tempClosestPoint, float3 queryPosition, ref QueryCache s) {
			float sqrDist = math.lengthsq(tempClosestPoint - queryPosition);

			KdQueryNode queryNode = s.QueueArray[s.Count];
			s.Count++;

			queryNode.node = node;
			queryNode.tempClosestPoint = tempClosestPoint;
			queryNode.distance = sqrDist;

			s.MinHeap.PushObj(queryNode, sqrDist);
		}

		public static QueryCache GetQueryCache(KDTree tree, int k) {
			QueryCache s;
			s.Count = 0;
			s.Heap = new KSmallestHeap(k);
			s.MinHeap = new MinHeap(k * 4);
			s.QueueArray = new NativeArray<KdQueryNode>(tree.Nodes.Length / 2, Allocator.TempJob);
			return s;
		}

		public static void KNearest(KDTree tree, int k, float3 queryPosition, NativeSlice<int> result, QueryCache s) {
			CalculateKnn(tree, k, queryPosition, ref s);

			int retCount = k + 1;
			for (int i = 1; i < retCount; i++) {
				result[i - 1] = s.Heap.PopObj();
			}
		}
		
		public static int KNearestLast(KDTree tree, int k, float3 queryPosition, QueryCache s) {
			CalculateKnn(tree, k, queryPosition, ref s);

			int last = -1;
			int retCount = k + 1;
			for (int i = 1; i < retCount; i++) {
				last = s.Heap.PopObj();
			}

			return last;
		}

		static void CalculateKnn(KDTree tree, int k, float3 queryPosition, ref QueryCache s) {
			var points = tree.Points;
			var permutation = tree.Permutation;
			var rootNode = tree.RootNode;
			var nodes = tree.Nodes;

			int queryIndex = 0; // current index at stack

			s.Reset();
			
			// Biggest Smallest Squared Radius
			float bssr = float.PositiveInfinity;
			float3 rootClosestPoint = rootNode.bounds.ClosestPoint(queryPosition);
			PushToHeap(rootNode, rootClosestPoint, queryPosition, ref s);

			// searching
			while (s.MinHeap.Count > 0) {
				KdQueryNode queryNode = s.MinHeap.PopObj();

				s.QueueArray[queryIndex] = queryNode;
				queryIndex++;

				if (queryNode.distance > bssr) {
					continue;
				}

				KDNode node = queryNode.node;

				if (!node.Leaf) {
					int partitionAxis = node.partitionAxis;
					float partitionCoord = node.partitionCoordinate;

					float3 tempClosestPoint = queryNode.tempClosestPoint;

					if (tempClosestPoint[partitionAxis] - partitionCoord < 0) {
						// we already know we are on the side of negative bound/node,
						// so we don't need to test for distance
						// push to stack for later querying
						PushToHeap(nodes[node.NegativeChildIndex], tempClosestPoint, queryPosition, ref s);

						// project the tempClosestPoint to other bound
						tempClosestPoint[partitionAxis] = partitionCoord;

						if (nodes[node.PositiveChildIndex].Count != 0) {
							PushToHeap(nodes[node.PositiveChildIndex], tempClosestPoint, queryPosition, ref s);
						}
					} else {
						// we already know we are on the side of positive bound/node,
						// so we don't need to test for distance
						// push to stack for later querying
						PushToHeap(nodes[node.PositiveChildIndex], tempClosestPoint, queryPosition, ref s);

						// project the tempClosestPoint to other bound
						tempClosestPoint[partitionAxis] = partitionCoord;

						if (nodes[node.PositiveChildIndex].Count != 0) {
							PushToHeap(nodes[node.NegativeChildIndex], tempClosestPoint, queryPosition, ref s);
						}
					}
				} else {
					// LEAF
					for (int i = node.start; i < node.end; i++) {
						int index = permutation[i];
						float sqrDist = math.lengthsq(points[index] - queryPosition);

						if (sqrDist <= bssr) {
							s.Heap.PushObj(index, sqrDist);

							if (s.Heap.Count == k) {
								bssr = s.Heap.HeadValue;
							}
						}
					}
				}
			}
		}
	}
}