using System.Collections.Generic;

public static class ListExtension
{
	public static bool RemoveUnordered<T>(this List<T> list, int index) {
		if (index < 0 || index >= list.Count) {
			return false;
		}

		list[index] = list[list.Count - 1];
		list.RemoveAt(list.Count - 1);

		return true;
	}

	public static bool RemoveUnordered<T>(this List<T> list, T item) {
		int index = list.IndexOf(item);
		return RemoveUnordered(list, index);
	}
}
