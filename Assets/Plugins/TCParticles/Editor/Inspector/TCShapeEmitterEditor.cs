using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TCShapeEmitter), true)]
public class TCShapeEmitterEditor : TCEdtiorBase<TCShapeEmitter> {
	private TCWireframeDrawer m_drawer;

	private Mesh m_prevMesh;

	private TCShapeEmitter m_target;

	protected override void OnTCEnable() {
		m_target = target as TCShapeEmitter;

		m_drawer = new TCWireframeDrawer(m_target.ShapeData.emitMesh);
	}

	protected void OnDisable() {
		m_drawer.Release();
	}


	public override void OnTCInspectorGUI() {
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


		if (m_target.ShapeData.emitMesh == m_prevMesh) {
			return;
		}

		m_drawer.UpdateMesh(m_target.ShapeData.emitMesh);
		m_prevMesh = m_target.ShapeData.emitMesh;
	}

	void OnSceneGUI() {
		Undo.RecordObject(target, "Resize TC Particle emitter");



		var emitter = target as TCShapeEmitter;
		TCDrawFunctions.DrawEmitterShape(emitter.ShapeData, emitter.transform, m_drawer);
	}
}