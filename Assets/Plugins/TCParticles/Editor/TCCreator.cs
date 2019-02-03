using System;
using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	public static class TCParticlesCreator {
		static void CreateObjectInScene(string objectName, Type comp) {
			var go = new GameObject(objectName, comp);
			Selection.activeGameObject = go;

			if (SceneView.lastActiveSceneView != null) {
				go.transform.position = SceneView.lastActiveSceneView.pivot;
			}

			Undo.RegisterCreatedObjectUndo(go, "Create TC Particles object");
		}

		[MenuItem("GameObject/Effects/TC System", false, 10)]
		static void CreateTcSystem(MenuCommand menuCommand) {
			CreateObjectInScene("TCParticleSystem", typeof(TCParticleSystem));
		}

		[MenuItem("GameObject/Effects/TC Force", false, 10)]
		static void CreateTcForce(MenuCommand menuCommand) {
			CreateObjectInScene("TCParticleForce", typeof(TCForce));
		}

		[MenuItem("GameObject/Effects/TC Collider", false, 10)]
		static void CreateTCCollider(MenuCommand menuCommand) {
			CreateObjectInScene("TCParticleCollider", typeof(TCCollider));
		}

		[MenuItem("GameObject/Effects/TC Shape Emitter", false, 10)]
		static void CreateTCShapEmitter(MenuCommand menuCommand) {
			CreateObjectInScene("TCShapeEmitter", typeof(TCShapeEmitter));
		}
	}
}