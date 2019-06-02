using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	[CustomPropertyDrawer(typeof(MinMaxRandom))]
	public class MinMaxRandomDrawer : PropertyDrawer {
		string[] options = {"Constant", "Curve", "Random Between Two Constants", "Random Between Two Curves"};

		// Here you can define the GUI for your property drawer. Called by Unity.
		public override void OnGUI(Rect position, SerializedProperty prop, GUIContent label) {
			using (new GUILayout.HorizontalScope()) {
				EditorGUI.BeginProperty(position, label, prop);

				var modeProp = prop.FindPropertyRelative("modeProp");

				if (!modeProp.hasMultipleDifferentValues) {
					var mode = (MinMaxRandom.MinMaxMode) modeProp.enumValueIndex;

					switch (mode) {
						case MinMaxRandom.MinMaxMode.Constant:
							EditorGUILayout.PropertyField(prop.FindPropertyRelative("valueProp"), label);
							break;
						case MinMaxRandom.MinMaxMode.RandomBetween:
							EditorGUILayout.PropertyField(prop.FindPropertyRelative("minProp"), label);
							EditorGUILayout.PropertyField(prop.FindPropertyRelative("maxProp"), GUIContent.none);
							break;
						case MinMaxRandom.MinMaxMode.Curve:
							EditorGUILayout.PropertyField(prop.FindPropertyRelative("valueCurve"), label);
							break;
						case MinMaxRandom.MinMaxMode.RandomBetweenCurves:
							EditorGUILayout.PropertyField(prop.FindPropertyRelative("minCurve"), label);
							GUILayout.Space(4.0f);
							EditorGUILayout.PropertyField(prop.FindPropertyRelative("maxCurve"), GUIContent.none);
							break;
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
				int index = EditorGUILayout.Popup(modeProp.enumValueIndex, options, s, GUILayout.Width(30.0f));

				if (EditorGUI.EndChangeCheck()) {
					prop.FindPropertyRelative("modeProp").enumValueIndex = index;
				}

				EditorGUI.EndProperty();
				
				EditorGUI.EndProperty();
			}
		}
	}
}