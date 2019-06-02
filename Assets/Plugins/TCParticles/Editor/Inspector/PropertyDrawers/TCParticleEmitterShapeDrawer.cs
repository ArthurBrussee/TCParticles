using TC.Internal;
using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	[CustomPropertyDrawer(typeof(ParticleEmitterShape))]
	public class TCParticleEmitterShapeDrawer : PropertyDrawer {
		SerializedProperty m_prop;

		SerializedProperty GetProperty(string property) {
			return m_prop.FindPropertyRelative(property);
		}

		void PropField(string property) {
			var prop = GetProperty(property);

			if (prop == null) {
				Debug.LogError(property);
				return;
			}

			EditorGUILayout.PropertyField(prop, true);
		}

		void PropField(string property, Rect rect) {
			var prop = GetProperty(property);

			if (prop == null) {
				Debug.LogError(property);
				return;
			}

			EditorGUI.PropertyField(rect, prop, true);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			m_prop = property;

			PropField("shape", position);

			EmitShapes s = (EmitShapes) GetProperty("shape").enumValueIndex;

			switch (s) {
				case EmitShapes.HemiSphere:
				case EmitShapes.Sphere:
					PropField("radius");
					break;
				case EmitShapes.Box:
					PropField("cubeSize");
					break;
				case EmitShapes.Cone:
					PropField("coneRadius");
					PropField("coneHeight");
					PropField("coneAngle");
					break;
				case EmitShapes.Ring:
					PropField("ringRadius");
					PropField("ringOuterRadius");
					break;
				case EmitShapes.Line:
					PropField("lineLength");
					PropField("lineRadius");
					break;

				case EmitShapes.Mesh:
					PropField("emitMesh");
					PropField("spawnOnMeshSurface");

					if (GetProperty("spawnOnMeshSurface").boolValue) {
						PropField("normalizeArea");
					}

					PropField("texture");

					if (GetProperty("texture").objectReferenceValue != null) {
						PropField("uvChannel");
					}

					break;
				case EmitShapes.PointCloud:
					PropField("pointCloud");
					break;
			}

			PropField("startDirectionType");
			int type = GetProperty("startDirectionType").enumValueIndex;

			if (type == (int) StartDirection.Vector) {
				PropField("startDirectionVector");
				PropField("startDirectionRandomAngle");
			}
		}
	}
}