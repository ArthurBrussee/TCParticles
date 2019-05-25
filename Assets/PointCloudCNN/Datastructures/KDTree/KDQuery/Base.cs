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

/*
The object used for querying. This object should be persistent - re-used for querying.
Contains internal array for pooling, so that it doesn't generate (too much) garbage.
The array never down-sizes, only up-sizes, so the more you use this object, less garbage will it make over time.

Should be used only by 1 thread,
which means each thread should have it's own KDQuery object in order for querying to be thread safe.

KDQuery can query different KDTrees.
*/

#region

using System;
using UnityEngine;

#endregion

namespace DataStructures.ViliWonka.KDTree {
	public partial class KDQuery {
		protected int count; // size of queue
		protected Heap.MinHeap minHeap; //heap for k-nearest
		protected int queryIndex; // current index at stack

		protected KdQueryNode[] queueArray; // queue array

		public KDQuery(int queryNodesContainersInitialSize = 2048) {
			queueArray = new KdQueryNode[queryNodesContainersInitialSize];
			minHeap = new Heap.MinHeap(queryNodesContainersInitialSize);
		}

		protected int LeftToProcess => count - queryIndex;

		/// <summary>
		/// Returns initialized node from stack that also acts as a pool
		/// The returned reference to node stays in stack
		/// </summary>
		/// <returns>Reference to pooled node</returns>
		KdQueryNode PushGetQueue() {
			KdQueryNode node;

			if (count < queueArray.Length) {
				node = queueArray[count];
			} else {
				// automatic resize of pool
				Array.Resize(ref queueArray, queueArray.Length * 2);
				node = queueArray[count];
			}

			count++;

			return node;
		}

		protected void PushToQueue(KDNode node, Vector3 tempClosestPoint) {
			KdQueryNode queryNode = PushGetQueue();
			queryNode.node = node;
			queryNode.tempClosestPoint = tempClosestPoint;
		}

		protected void PushToHeap(KDNode node, Vector3 tempClosestPoint, Vector3 queryPosition) {
			KdQueryNode queryNode = PushGetQueue();
			queryNode.node = node;
			queryNode.tempClosestPoint = tempClosestPoint;

			float sqrDist = Vector3.SqrMagnitude(tempClosestPoint - queryPosition);
			queryNode.distance = sqrDist;
			minHeap.PushObj(queryNode, sqrDist);
		}

		// just gets unprocessed node from stack
		// increases queryIndex
		protected KdQueryNode PopFromQueue() {
			KdQueryNode node = queueArray[queryIndex];
			queryIndex++;

			return node;
		}

		protected KdQueryNode PopFromHeap() {
			KdQueryNode heapNode = minHeap.PopObj();

			queueArray[queryIndex] = heapNode;
			queryIndex++;

			return heapNode;
		}

		protected void Reset() {
			count = 0;
			queryIndex = 0;
			minHeap.Clear();
		}
	}
}