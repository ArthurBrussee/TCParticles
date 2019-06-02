using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	[CustomPropertyDrawer(typeof(TCShapeEmitTag))]
	public class TCShapeEmitTagDrawer : PropertyDrawer {
		SerializedProperty m_prop;

		static List<TCShapeEmitTag> s_tags;
		static GUIContent[] s_display;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			if (s_tags == null) {
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

			s_tags.RemoveAll(tag => tag == null);
			DrawEmitTagUI(position, property, label);
		}

		void DrawEmitTagUI(Rect pos, SerializedProperty prop, GUIContent label) {
			EditorGUI.BeginProperty(pos, label, prop);

			EditorGUI.BeginChangeCheck();

			var curTag = prop.objectReferenceValue as TCShapeEmitTag;
			int curIndex;

			if (curTag == null) {
				curIndex = 0;
			} else {
				curIndex = s_tags.IndexOf(curTag) + 1;
			}

			int newIndex = EditorGUI.Popup(pos, label, curIndex, s_display);

			TCShapeEmitTag tag;

			if (newIndex == s_tags.Count + 1) {
				tag = CreateNewTag();
			} else if (newIndex != 0) {
				tag = s_tags[newIndex - 1];
			} else {
				tag = null;
			}

			if (EditorGUI.EndChangeCheck()) {
				prop.objectReferenceValue = tag;
			}
		}

		TCShapeEmitTag CreateNewTag() {
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
}