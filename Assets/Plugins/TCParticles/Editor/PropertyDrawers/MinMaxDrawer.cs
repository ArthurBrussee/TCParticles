using TC.Internal;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof (MinMax))]
public class MinMaxDrawer : PropertyDrawer
{
	private Rect rect;
	private string[] options = {"Constant", "Between"};

	private Rect GetRect(float width)
	{
		var ret = new Rect(rect.x, rect.y, width, rect.height);
		rect.x += width;
		return ret;
	}

	// Here you can define the GUI for your property drawer. Called by Unity.
	public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {
		GUILayout.BeginHorizontal();
		EditorGUI.BeginProperty(position, label, prop);
	
		rect = position;

		var modeProp = prop.FindPropertyRelative("modeProp");

		if (!modeProp.hasMultipleDifferentValues) {


			var mode = (MinMax.MinMaxMode) prop.FindPropertyRelative("modeProp").enumValueIndex;

			if (mode == MinMax.MinMaxMode.Constant)
				EditorGUILayout.PropertyField(prop.FindPropertyRelative("valueProp"), label);
			else if (mode == MinMax.MinMaxMode.Between) {
				EditorGUILayout.PropertyField(prop.FindPropertyRelative("minProp"), label);
				EditorGUILayout.PropertyField(prop.FindPropertyRelative("maxProp"), new GUIContent(""));
			}
		} else {

			GUILayout.Label(label);

			GUILayout.Label("--");
		}

		GUIStyle s = EditorStyles.toolbarPopup;
		s.contentOffset = new Vector2(100.0f, 100.0f);
		s.fixedWidth = 12.0f;

		EditorGUI.BeginProperty(new Rect(0, 0, 0, 0), label, modeProp);

		
		
		EditorGUI.BeginChangeCheck();
		int index =EditorGUILayout.Popup( modeProp.enumValueIndex, options, s, GUILayout.Width(25.0f));
		if (EditorGUI.EndChangeCheck()) {
			prop.FindPropertyRelative("modeProp").enumValueIndex = index;
		}



		EditorGUI.EndProperty();
		EditorGUI.EndProperty();

		GUILayout.EndHorizontal();
	}
}