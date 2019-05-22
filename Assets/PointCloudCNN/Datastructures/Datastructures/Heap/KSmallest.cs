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

using Unity.Collections;

namespace DataStructures.ViliWonka.Heap {
	// array start at index 1
	// generic version
	public struct KSmallestHeap{
		NativeArray<int> objs; //objects
		NativeArray<float> heap;

		public int Count;
		int maxSize;

		public bool Full => maxSize == Count;
		public float HeadValue => heap[1];
		
		public KSmallestHeap(int maxEntries) {
			maxSize = maxEntries;
			heap = new NativeArray<float>(maxEntries + 1, Allocator.TempJob);
			objs = new NativeArray<int>(maxEntries + 1, Allocator.TempJob);
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

		// bubble down, MaxHeap version
		void BubbleDownMax(int index) {
			int L = Left(index);
			int R = Right(index);

			// bubbling down, 2 kids
			while (R <= Count) {
				// if heap property is violated between index and Left child
				if (heap[index] < heap[L]) {
					if (heap[L] < heap[R]) {
						Swap(index, R); // left has bigger priority
						index = R;
					} else {
						Swap(index, L); // right has bigger priority
						index = L;
					}
				} else {
					// if heap property is violated between index and R
					if (heap[index] < heap[R]) {
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
			if (L <= Count && heap[index] < heap[L]) {
				Swap(index, L);
			}
		}

		// bubble up, MaxHeap version
		void BubbleUpMax(int index) {
			int P = Parent(index);

			//swap, until Heap property isn't violated anymore
			while (P > 0 && heap[P] < heap[index]) {
				Swap(P, index);

				index = P;
				P = Parent(index);
			}
		}

		// bubble down, MinHeap version
		void Swap(int A, int B) {
			float tempHeap = heap[A];
			int tempObjs = objs[A];

			heap[A] = heap[B];
			objs[A] = objs[B];

			heap[B] = tempHeap;
			objs[B] = tempObjs;
		}

		public void PushObj(int obj, float h) {
			// if heap full
			if (Count == maxSize) {
				// if Heads priority is smaller than input priority, then ignore that item
				if (HeadValue < h) {
				} else {
					heap[1] = h; // remove top element
					objs[1] = obj;
					BubbleDownMax(1); // bubble it down
				}
			} else {
				Count++;
				heap[Count] = h;
				objs[Count] = obj;
				BubbleUpMax(Count);
			}
		}

		public int PopObj() {
			int result = objs[1];
			heap[1] = heap[Count];
			objs[1] = objs[Count];
			Count--;
			BubbleDownMax(1);
			return result;
		}

		public int PopObj(ref float heapValue) {
			heapValue = heap[1];
			return PopObj();
		}

		public void Dispose() {
			heap.Dispose();
			objs.Dispose();
		}
	}
}