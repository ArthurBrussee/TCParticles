using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	public class TCEdtiorBase<T> : Editor where T : Component {
		Dictionary<string, SerializedProperty> m_properties;
		protected T[] Targets;

		//base API functions
		protected SerializedProperty GetProperty(string propName) {
			return CheckProp(propName);
		}

		void OnEnable() {
			hideFlags = HideFlags.HideAndDontSave;
			Targets = targets.Cast<T>().ToArray();

			m_properties = new Dictionary<string, SerializedProperty>();
			OnTCEnable();
		}

		void OnDisable() {
			OnTCDisable();
		}

		protected virtual void OnTCEnable() {
		}

		protected virtual void OnTCDisable() {
		}

		public override void OnInspectorGUI() {
			serializedObject.Update();
			GUILayout.Space(10.0f);
			OnTCInspectorGUI();
			serializedObject.ApplyModifiedProperties();
		}

		SerializedProperty CheckProp(string varName) {
			if (!m_properties.ContainsKey(varName)) {
				SerializedProperty p = serializedObject.FindProperty(varName);
				if (p != null) {
					m_properties.Add(varName, p);
				} else {
					Debug.Log("Property not found! " + varName);
					return null;
				}
			}

			return m_properties[varName];
		}

		protected void EnumPopup(SerializedProperty prop, Enum selected, float width = 12.0f) {
			prop.enumValueIndex = Convert.ToInt32(
				EditorGUILayout.EnumPopup(selected, EditorStyles.toolbarPopup, GUILayout.Width(width)));
		}

		protected void PropField(string varName, GUIContent dispName) {
			EditorGUILayout.PropertyField(CheckProp(varName), dispName, null);
		}

		protected void PropField(string varName, GUIContent dispName, params GUILayoutOption[] options) {
			EditorGUILayout.PropertyField(CheckProp(varName), dispName, options);
		}

		protected SerializedProperty CheckBurstProp(string propName, int i) {
			string varName = "_emitter.bursts." + propName + i;

			if (!m_properties.ContainsKey(varName)) {
				SerializedProperty p = CheckProp("_emitter.bursts").GetArrayElementAtIndex(i).FindPropertyRelative(propName);

				if (p != null) {
					m_properties.Add(varName, p);
				} else {
					Debug.Log("Property not found! " + varName);
				}
			}

			return m_properties[varName];
		}

		protected OpenClose GetOpenClose() {
			var all = Resources.FindObjectsOfTypeAll<OpenClose>();
			var o = all.Length > 0 ? all[0] : null;

			if (o == null) {
				o = CreateInstance<OpenClose>();
				o.hideFlags = HideFlags.HideAndDontSave;
			}

			return o;
		}

		protected void ToolbarToggle(string varName, GUIContent dispName, params GUILayoutOption[] options) {
			SerializedProperty prop = CheckProp(varName);

			var content = new GUIContent(dispName);

			var position = GUILayoutUtility.GetRect(content, EditorStyles.toolbarButton);

			content = EditorGUI.BeginProperty(position, content, prop);
			EditorGUI.BeginChangeCheck();

			if (EditorGUI.showMixedValue) {
				content.text += " - (mixed)";
			}

			var newVal = GUI.Toggle(position, prop.boolValue && !EditorGUI.showMixedValue, content, EditorStyles.toolbarButton);

			if (EditorGUI.EndChangeCheck()) {
				prop.boolValue = newVal;
			}

			EditorGUI.EndProperty();
		}

		protected virtual void OnTCInspectorGUI() {}
	}
}