using UnityEditor;

namespace TC {
	[CustomEditor(typeof(PointCloudData))]
	public sealed class PointCloudDataInspector : Editor {
		public override void OnInspectorGUI() {
			int count = ((PointCloudData)target).PointCount;
			EditorGUILayout.LabelField("Point Count", count.ToString("N0"));
		}
	}
}