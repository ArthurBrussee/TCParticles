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

using DataStructures.ViliWonka.KDTree;
using Unity.Collections;

namespace DataStructures.ViliWonka.Heap {
	// generic version
	public struct MinHeap {
		NativeArray<float> heap;
		NativeArray<KdQueryNode> objs; // objects
		public int Count;
		
		public KdQueryNode HeadHeapObject => objs[1];

		public MinHeap(int maxNodes) {
			objs = new NativeArray<KdQueryNode>(maxNodes + 1, Allocator.TempJob);
			heap = new NativeArray<float>(maxNodes + 1, Allocator.TempJob);
			Count = 0;
		}
		
		public void Clear() {
			Count = 0;
		}

		int Parent(int index) {
			return index >> 1;
		}

		int Left(int index) {
			return index << 1;
		}

		int Right(int index) {
			return (index << 1) | 1;
		}

		void BubbleDownMin(int index) {
			int L = Left(index);
			int R = Right(index);

			// bubbling down, 2 kids
			while (R <= Count) {
				// if heap property is violated between index and Left child
				if (heap[index] > heap[L]) {
					if (heap[L] > heap[R]) {
						Swap(index, R); // right has smaller priority
						index = R;
					} else {
						Swap(index, L); // left has smaller priority
						index = L;
					}
				} else {
					// if heap property is violated between index and R
					if (heap[index] > heap[R]) {
						Swap(index, R);
						index = R;
					} else {
						index = L;
						L = Left(index);
						break;
					}
				}

				L = Left(index);
				R = Right(index);
			}

			// only left & last children available to test and swap
			if (L <= Count && heap[index] > heap[L]) {
				Swap(index, L);
			}
		}

		void BubbleUpMin(int index) {
			int P = Parent(index);

			//swap, until Heap property isn't violated anymore
			while (P > 0 && heap[P] > heap[index]) {
				Swap(P, index);

				index = P;
				P = Parent(index);
			}
		}

		void Swap(int A, int B) {
			float tempHeap = heap[A];
			KdQueryNode tempObjs = objs[A];

			heap[A] = heap[B];
			objs[A] = objs[B];

			heap[B] = tempHeap;
			objs[B] = tempObjs;
		}

		public void PushObj(KdQueryNode obj, float h) {
			Count++;
			heap[Count] = h;
			objs[Count] = obj;
			BubbleUpMin(Count);
		}

		public KdQueryNode PopObj() {
			KdQueryNode result = objs[1];

			heap[1] = heap[Count];
			objs[1] = objs[Count];

			objs[Count] = default;
			Count--;

			if (Count != 0) {
				BubbleDownMin(1);
			}

			return result;
		}

		public void Dispose() {
			heap.Dispose();
			objs.Dispose();
		}
	}
}