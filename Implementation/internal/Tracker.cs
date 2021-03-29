using System.Collections.Generic;

namespace TC.Internal {
	public interface ITracked {
		int Index { get; set; }
	}

	public abstract class Tracker<T> where T : class, ITracked {
		public static List<T> All = new List<T>();

		public static void Register(T item) {
			item.Index = All.Count;
			All.Add(item);
		}

		public static void Deregister(T item) {
			if (item.Index == -1) {
				return;
			}
			//Update indices
			All[All.Count - 1].Index = item.Index;
			All[item.Index] = All[All.Count - 1];
			All.RemoveAt(All.Count - 1);
		}
	}
}