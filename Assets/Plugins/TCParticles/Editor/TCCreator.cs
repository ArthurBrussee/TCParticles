using System;
using UnityEditor;
using UnityEngine;

public static class TCParticlesCreator
{
	private static void CreateObjectInScene(string objectName, params Type[] components) {
		var go = new GameObject();

		foreach (var component in components) {
			go.AddComponent(component);
		}

	
		go.name = objectName;
		Selection.activeGameObject = go;

		go.transform.position = SceneView.lastActiveSceneView.pivot;

		Undo.RegisterCreatedObjectUndo(go, "Create TC Particles object");
	}

	[MenuItem("GameObject/Create Other/TC Particle system")]
	private static void CreateTcSystem() {
		CreateObjectInScene("TCParticleSystem", typeof(TCParticleSystem));
	}


	[MenuItem("GameObject/Create Other/TC Particle Force")]
	private static void CreateTcForce() {

		CreateObjectInScene("TCParticleForce", typeof (TCForce));
	}

		[MenuItem("GameObject/Create Other/TC Particle Collider")]
	private static void CreateTCCollider() {

		CreateObjectInScene("TCParticleCollider", typeof(TCCollider));

	}

	[MenuItem("GameObject/Create Other/TC Particle Shape Emitter")]
	private static void CreateTCShapEmitter() {
		CreateObjectInScene("TCShapeEmitter", typeof (TCShapeEmitter));
	}
}