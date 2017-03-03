using System;
using UnityEditor;
using UnityEngine;

public static class TCParticlesCreator {
	static void CreateObjectInScene(string objectName, Type comp) {
		var go = new GameObject(objectName, comp);
		Selection.activeGameObject = go;

		if (SceneView.lastActiveSceneView != null) {
			go.transform.position = SceneView.lastActiveSceneView.pivot;
		}

		Undo.RegisterCreatedObjectUndo(go, "Create TC Particles object");
	}

	[MenuItem("GameObject/TC Particle System/System")]
	static void CreateTcSystem() {
		CreateObjectInScene("TCParticleSystem", typeof(TCParticleSystem));
	}

	[MenuItem("GameObject/TC Particle System/Force")]
	static void CreateTcForce() {
		CreateObjectInScene("TCParticleForce", typeof (TCForce));
	}

	[MenuItem("GameObject/TC Particle System/Collider")]
	static void CreateTCCollider() {
		CreateObjectInScene("TCParticleCollider", typeof(TCCollider));
	}

	[MenuItem("GameObject/TC Particle System/Shape Emitter")]
	static void CreateTCShapEmitter() {
		CreateObjectInScene("TCShapeEmitter", typeof (TCShapeEmitter));
	}
}