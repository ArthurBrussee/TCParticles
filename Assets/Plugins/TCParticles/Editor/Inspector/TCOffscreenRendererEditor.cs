using UnityEditor;
using UnityEngine;

[CustomEditor(typeof (TCOffscreenRenderer))]
public class TCOffscreenRendererEditor : TCEdtiorBase<TCOffscreenRenderer>
{

	// Update is called once per frame
	public override void OnTCInspectorGUI()
	{
		PropField("offscreenLayer", new GUIContent("Offscreen Layer"));
		PropField("downsampleFactor", new GUIContent("Downsample factor", "The higher, the more performance"));
		PropField("compositeMode", new GUIContent("Composite Mode"));

		if (!serializedObject.isEditingMultipleObjects)
		{
			switch ((TCOffscreenRenderer.CompositeMode) GetProperty("compositeMode").enumValueIndex)
			{
				case TCOffscreenRenderer.CompositeMode.Gradient:
					PropField("compositeGradient", new GUIContent("Composite Gradient"));
					PropField("tint", new GUIContent("Tint"));
					PropField("gradientScale", new GUIContent("Gradient Scale"));
					break;

				case TCOffscreenRenderer.CompositeMode.Distort:
					PropField("distortStrength", new GUIContent("Distort Strength"));
					break;
			}
		}


		if (EditorApplication.isPlaying)
		{
			foreach (TCOffscreenRenderer t in targets)
			{
				t.UpdateCompositeGradient();
			}
		}
		EditorGUILayout.HelpBox("This feature is still experimental! For more info, refer to the manual", MessageType.Warning);
	}
}