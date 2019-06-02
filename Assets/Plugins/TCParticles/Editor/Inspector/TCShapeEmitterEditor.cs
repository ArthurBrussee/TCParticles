using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	[CustomEditor(typeof(TCShapeEmitter), true), CanEditMultipleObjects]
	public class TCShapeEmitterEditor : TCEdtiorBase<TCShapeEmitter> {
		TCShapeEmitter m_target;

		protected override void OnTCEnable() {
			m_target = target as TCShapeEmitter;
		}

		protected override void OnTCInspectorGUI() {
			PropField("ShapeData", new GUIContent("Shape", "The shape of the emitter"));

			GUILayout.Space(20.0f);

			PropField("m_tag", new GUIContent("Tag", "The tag to link with particle systems"));

			var tagProp = GetProperty("m_tag");

			if (!tagProp.hasMultipleDifferentValues && tagProp.objectReferenceValue == null) {
				EditorGUILayout.HelpBox("Shape emitter must have a tag", MessageType.Warning);
			}

			EditorGUILayout.BeginHorizontal();
			PropField("EmissionRate", new GUIContent("Emission Rate", "Amount of particles to emit per second or unit"));
			PropField("EmissionType", new GUIContent("", "Determines whether particles are emitter per second or per unit"),
				GUILayout.Width(80.0f));
			EditorGUILayout.EndHorizontal();

			PropField("Emit", new GUIContent("Do Emit", "Toggles emitting on this shape emitter"));
		}

		void OnSceneGUI() {
			Undo.RecordObject(target, "Resize TC Particle emitter");
			var emitter = target as TCShapeEmitter;
			TCDrawFunctions.DrawEmitterShape(emitter.ShapeData, emitter.transform);
		}
	}
}