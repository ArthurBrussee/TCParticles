using TC;
using TC.Internal;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ParticleEmitterShape))]
public class TCParticleEmitterShapeDrawer : PropertyDrawer {
	private SerializedProperty m_prop;



	SerializedProperty GetProperty(string property) {
		return m_prop.FindPropertyRelative(property);
	}


	void PropField(string property) {
		var prop = GetProperty(property);

		EditorGUILayout.PropertyField(prop, true);
	}

	void PropField(string property, Rect rect) {
		var prop = GetProperty(property);

		EditorGUI.PropertyField(rect, prop, true);
	}

	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
		m_prop = property;

		PropField("shape", position);

		int s = GetProperty("shape").enumValueIndex;

		switch (s) {
			case (int) EmitShapes.HemiSphere:
			case (int) EmitShapes.Sphere:
				PropField("radius");
				break;
			case (int) EmitShapes.Box:
				PropField("cubeSize");
				break;
			case (int) EmitShapes.Cone:
				PropField("coneRadius");
				PropField("coneHeight");
				PropField("coneAngle");
				break;
			case (int) EmitShapes.Ring:
				PropField("ringRadius");
				PropField("ringOuterRadius");
				break;
			case (int) EmitShapes.Line:
				PropField("lineLength");
				PropField("lineRadius");
				break;
			case (int) EmitShapes.Mesh:
				PropField("emitMesh");
				PropField("spawnOnMeshSurface");
				break;
		}


		PropField("startDirectionType");

		int type = GetProperty("startDirectionType").enumValueIndex;


		if (type == (int) StartDirectionType.Vector) {
			PropField("startDirectionVector");
			PropField("startDirectionRandomAngle");
		}
	}
}