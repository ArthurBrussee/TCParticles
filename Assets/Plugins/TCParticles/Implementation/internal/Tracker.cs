using System;
using UnityEngine;

namespace TC.Internal {
	public interface ITracked {
		int Index { get; set; }
	}

	public class Tracker<T> where T : ITracked {
		public static T[] All = new T[16];
		public static int Count;

		public static void Register(T item) {
			if (Count >= All.Length) {
				Array.Resize(ref All, All.Length * 2);
			}

			All[Count] = item;
			item.Index = Count;
			++Count;
		}

		public static void Deregister(T item) {
			if (item.Index == -1) {
				return;
			}

			//Update indices
			All[Count - 1].Index = item.Index;
			All[item.Index] = All[Count - 1];
			--Count;
		}
	}
}