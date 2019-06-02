using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	[CustomPropertyDrawer(typeof(Vector3Curve))]
	public class Vector3CurveDrawer : PropertyDrawer {
		Rect rect;
		GUIContent[] options = {new GUIContent("constant"), new GUIContent("curve")};

		Rect GetRect(float width) {
			Rect ret = new Rect(rect.x, rect.y, width, 16.0f);
			rect.x += width;
			return ret;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			return 32.0f;
		}

		// Here you can define the GUI for your property drawer. Called by Unity.
		public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {
			EditorGUI.BeginProperty(position, label, prop);
			position.x += 5.0f;
			rect = position;
			bool constant = prop.FindPropertyRelative("isConstant").boolValue;

			GUI.Label(GetRect(200.0f), label);

			rect.y += 16.0f;
			rect.x = 40.0f;

			float oldWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 12.0f;

			if (!constant) {
				EditorGUI.PropertyField(GetRect(80.0f), prop.FindPropertyRelative("xCurve"), new GUIContent("X"));
				EditorGUI.PropertyField(GetRect(80.0f), prop.FindPropertyRelative("yCurve"), new GUIContent("Y"));
				EditorGUI.PropertyField(GetRect(80.0f), prop.FindPropertyRelative("zCurve"), new GUIContent("Z"));
			} else {
				EditorGUI.PropertyField(GetRect(80.0f), prop.FindPropertyRelative("x"), new GUIContent("X"));
				EditorGUI.PropertyField(GetRect(80.0f), prop.FindPropertyRelative("y"), new GUIContent("Y"));
				EditorGUI.PropertyField(GetRect(80.0f), prop.FindPropertyRelative("z"), new GUIContent("Z"));
			}

			EditorGUIUtility.labelWidth = oldWidth;

			int c = constant ? 0 : 1;
			GUIStyle s = EditorStyles.toolbarPopup;
			s.contentOffset = new Vector2(100.0f, 100.0f);
			s.fixedWidth = 12.0f;

			c = EditorGUI.Popup(GetRect(80.0f), c, options, s);
			prop.FindPropertyRelative("isConstant").boolValue = c == 0;

			EditorGUI.EndProperty();
		}
	}
}