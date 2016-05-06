using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class TCEdtiorBase<T> : Editor where T : Component
{
	private Dictionary<string, SerializedProperty> properties;

	protected T[] Targets;

	//base API functions
	public SerializedProperty GetProperty(string propName) {
		return CheckProp(propName);
	}

	private void OnEnable() {
		hideFlags = HideFlags.HideAndDontSave;
		Targets = targets.Cast<T>().ToArray();

		properties = new Dictionary<string, SerializedProperty>();
		OnTCEnable();
	}

	protected virtual void OnTCEnable() {
	}

	public override void OnInspectorGUI() {
		serializedObject.Update();
		GUILayout.Space(10.0f);
		OnTCInspectorGUI();
		serializedObject.ApplyModifiedProperties();
	}

	private SerializedProperty CheckProp(string varName) {
		if (!properties.ContainsKey(varName)) {
			SerializedProperty p = serializedObject.FindProperty(varName);
			if (p != null) {
				properties.Add(varName, p);
			}
			else {
				Debug.Log("Property not found! " + varName);
				return null;
			}
		}

		return properties[varName];
	}

	private SerializedProperty CheckPropRelative(string baseName, string varName) {
		string propName = baseName + "." + varName;

		if (!properties.ContainsKey(propName)) {
			SerializedProperty b = CheckProp(baseName);

			if (b == null) {
				return null;
			}

			SerializedProperty p = b.FindPropertyRelative(varName);

			if (p != null) {
				properties.Add(propName, p);
			}
			else {
				Debug.Log("Relative property not found!" + varName);
			}
		}

		return properties[propName];
	}



	protected void EnumPopup(SerializedProperty prop, Enum selected, float width = 12.0f) {
		prop.enumValueIndex = Convert.ToInt32(EditorGUILayout.EnumPopup(selected, EditorStyles.toolbarPopup, GUILayout.Width(width)));
	}


	public void PropField(string varName, GUIContent dispName, params GUILayoutOption[] options) {
		EditorGUILayout.PropertyField(CheckProp(varName), dispName, options);
	}

	public SerializedProperty CheckBurstProp(string propName, int i) {
		string varName = "_emitter.bursts." + propName + i;

		if (!properties.ContainsKey(varName)) {
			SerializedProperty p = CheckProp("_emitter.bursts").GetArrayElementAtIndex(i).FindPropertyRelative(propName);

			if (p != null) {
				properties.Add(varName, p);
			}
			else {
				Debug.Log("Property not found! " + varName);
			}
		}

		return properties[varName];
	}

	public OpenClose GetOpenClose() {
		var o = AssetDatabase.LoadMainAssetAtPath("Assets/Plugins/TCParticles/Editor/OpenClose.asset") as OpenClose;

		if (o == null) {
			o = CreateInstance<OpenClose>();
			AssetDatabase.CreateAsset(o, "Assets/Plugins/TCParticles/Editor/OpenClose.asset");
		}

		return o;
	}


	public void ToolbarToggle(string varName, GUIContent dispName, params GUILayoutOption[] options) {
		SerializedProperty prop = CheckProp(varName);

		var content = new GUIContent(dispName);

		var position = GUILayoutUtility.GetRect(content, EditorStyles.toolbarButton);

		content = EditorGUI.BeginProperty(position, content, prop);
		EditorGUI.BeginChangeCheck();

		if (EditorGUI.showMixedValue)
			content.text += " - (mixed)";

		var newVal = GUI.Toggle(position, prop.boolValue && !EditorGUI.showMixedValue, content, EditorStyles.toolbarButton);


		if (EditorGUI.EndChangeCheck())
			prop.boolValue = newVal;


		EditorGUI.EndProperty();
	}

	public virtual void OnTCInspectorGUI() {
	}
}