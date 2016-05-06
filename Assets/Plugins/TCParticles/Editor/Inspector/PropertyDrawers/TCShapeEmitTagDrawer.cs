using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;


[CustomPropertyDrawer(typeof(TCShapeEmitTag))]
public class TCShapeEmitTagDrawer : PropertyDrawer {
	private SerializedProperty m_prop;

	private static List<TCShapeEmitTag> s_tags;
	private static GUIContent[] s_display;


	private void InitTags() {
		s_tags = new List<TCShapeEmitTag>();

		var assetPaths = AssetDatabase.FindAssets("t:TCShapeEmitTag");


		foreach (var guid in assetPaths) {
			var path = AssetDatabase.GUIDToAssetPath(guid);
			var loaded = AssetDatabase.LoadAssetAtPath(path, typeof(TCShapeEmitTag)) as TCShapeEmitTag;

			s_tags.Add(loaded);
		}

		s_display = new GUIContent[s_tags.Count + 2];


		s_display[0] = new GUIContent("None");

		for (int i = 0; i < s_tags.Count; ++i) {
			s_display[i + 1] = new GUIContent(s_tags[i].name);
		}

		s_display[s_tags.Count + 1] = new GUIContent("+New Tag");
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		//if (s_tags == null) {
		InitTags();
		//}

		s_tags.RemoveAll(tag => tag == null);


		DrawEmitTagUI(position, property, label);
	}

	private void DrawEmitTagUI(Rect pos, SerializedProperty prop, GUIContent label) {

		EditorGUI.BeginProperty(pos, label, prop);

		EditorGUI.BeginChangeCheck();

		var curTag = prop.objectReferenceValue as TCShapeEmitTag;
		int curIndex;

		if (curTag == null) {
			curIndex = 0;
		}
		else {
			curIndex = s_tags.IndexOf(curTag) + 1;
		}

		int newIndex = EditorGUI.Popup(pos, label, curIndex, s_display);

		TCShapeEmitTag tag;

		if (newIndex == s_tags.Count + 1) {
			tag = CreateNewTag();
		}
		else if (newIndex != 0) {
			tag = s_tags[newIndex - 1];
		}
		else {
			tag = null;
		}

		if (EditorGUI.EndChangeCheck()) {
			prop.objectReferenceValue = tag;
		}
	}

	private TCShapeEmitTag CreateNewTag() {
		if (!Directory.Exists("Assets/TCParticleTags")) {
			AssetDatabase.CreateFolder("Assets", "TCParticleTags");
		}

		string path = AssetDatabase.GenerateUniqueAssetPath("Assets/TCParticleTags/unnamedTag.asset");

		var tag = ScriptableObject.CreateInstance<TCShapeEmitTag>();
		s_tags.Add(tag);

		ProjectWindowUtil.CreateAsset(tag, path);

		return tag;
	}
}