using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	[CustomEditor(typeof(TCForce)), CanEditMultipleObjects]
	public class TCForceEditor : TCEdtiorBase<TCForce> {
		TCForce m_forceTarget;
		List<TCForce> m_forces;
		TCForce m_primeForce;

		OpenClose m_openClose;
		TCNoiseForceVisualize m_forceVisualize;

		protected override void OnTCEnable() {
			m_forceTarget = target as TCForce;

			if (m_forceTarget != null) {
				m_forces = new List<TCForce>(m_forceTarget.GetComponents<TCForce>());
			}

			m_primeForce = m_forces[0];
			m_forces.RemoveAt(0);

			m_openClose = GetOpenClose();
			m_forceVisualize = new TCNoiseForceVisualize(m_forceTarget);
		}

		protected override void OnTCDisable() {
			m_forceVisualize.Release();
		}

		// Update is called once per frame

		protected override void OnTCInspectorGUI() {
			int shape = GetProperty("shape").enumValueIndex;

			if (m_forceTarget != m_primeForce) {
				GetProperty("globalShape").boolValue = false;
			}

			if (m_openClose.ToggleArea("TC Force", new Color(1.0f, 0.8f, 0.8f))) {
				PropField("power", new GUIContent("Power"));

				if (shape != (int) ForceShape.Box && shape != (int) ForceShape.Constant) {
					PropField("attenuationType", new GUIContent("Attenuation Type", "Determines how this attenuation is calculated"));
					PropField("_attenuation",
						new GUIContent("Attenuation", "Determines the rate of falloff of the power from the center"));
				}

				if (m_forceTarget == m_primeForce) {
					PropField("_inheritVelocity",
						new GUIContent("Inherit Velocity",
							"Factor of velocity to carry over to the particle when it is inside the volume"));
				}
			}

			m_openClose.ToggleAreaEnd("TC Force");

			if (!m_primeForce.IsGlobalShape || m_forceTarget == m_primeForce) {
				if (m_openClose.ToggleArea("Force Shape", new Color(0.8f, 0.8f, 1.0f))) {
					if (m_forceTarget == m_primeForce && m_forces.Count > 0) {
						GUILayout.BeginHorizontal();
						EditorGUILayout.PropertyField(GetProperty("shape"), new GUIContent("shape"), GUILayout.MinWidth(180.0f));
						GUILayout.Label("Global", GUILayout.Width(40.0f));
						GUI.enabled = !EditorApplication.isPlaying;
						EditorGUILayout.PropertyField(GetProperty("globalShape"), new GUIContent(""), GUILayout.Width(25.0f));
						GUI.enabled = true;
						GUILayout.Space(5.0f);
						GUILayout.EndHorizontal();
					} else {
						PropField("shape", new GUIContent("Shape"));
					}

					if (shape != (int) ForceShape.Box && shape != (int) ForceShape.Constant) {
						PropField("radius", new GUIContent("Radius"));
					}

					if (shape == (int) ForceShape.Capsule) {
						PropField("height", new GUIContent("Height", "Height of the capsule"));
					} else if (shape == (int) ForceShape.Box) {
						PropField("boxSize", new GUIContent("Box Size"));
					} else if (shape == (int) ForceShape.Disc) {
						PropField("discHeight", new GUIContent("Disch Height"));
						PropField("discRounding", new GUIContent("Disc Rounding"));
						PropField("discType", new GUIContent("Disc Type"));
					}
				}

				m_openClose.ToggleAreaEnd("Force Shape");
			}

			int type = GetProperty("forceType").enumValueIndex;

			if (m_openClose.ToggleArea("Force Type", new Color(0.8f, 1.0f, 0.8f))) {
				GUI.changed = false;
				PropField("forceType",
					new GUIContent("Type",
						"Determines the type of the force, different types have different effects on the particles"));
				if (GUI.changed) {
					SceneView.RepaintAll();
				}

				switch (type) {
					case (int) ForceType.Vortex:
						PropField("vortexAxis",
							new GUIContent("Vortex Axis", "The axis that the force makes the particles rotate around"));
						PropField("inwardForce",
							new GUIContent("Inward Force",
								"Force, proportional to rotation speed, that pulls the particles towards the axis"));
						break;

					case (int) ForceType.Vector:
						PropField("forceDirection", new GUIContent("Force Direction"));
						PropField("forceDirectionSpace", new GUIContent("Force Direction Space"));
						break;

					case (int) ForceType.Turbulence:
						PropField("resolution", new GUIContent("Resolution", "Resolution to bake the noise"));

						PropField("noiseType", new GUIContent("Noise Type"));
						PropField("frequency",
							new GUIContent("Frequency", "Frequency of the noise, the higher the smaller the noise details become"));
						PropField("lacunarity", new GUIContent("Lacunarity"));
						PropField("persistence", new GUIContent("Persistence"));
						PropField("octaveCount", new GUIContent("Octave Count"));
						PropField("noiseOffset", new GUIContent("Offset", "Spatial offset to scroll the noise (useful for animation)"));
						break;
				}

				if (type == (int) ForceType.Turbulence || type == (int) ForceType.TurbulenceTexture) {
					PropField("noiseExtents", new GUIContent("Noise Extents", "The space in which the noise should be repeated once"));
					PropField("smoothness",
						new GUIContent("Smoothness", "A low smoothness gives harsher lines in the particle movement"));

					GUI.changed = false;

					m_forceVisualize.PreviewMode =
						(TCNoiseForceVisualize.PreviewModeEnum) EditorGUILayout.EnumPopup("Preview mode", m_forceVisualize.PreviewMode);

					EditorGUI.BeginChangeCheck();

					float slice = EditorGUILayout.Slider("Preview slice", m_forceVisualize.PreviewSlice, 0.0f, 1.0f);

					if (EditorGUI.EndChangeCheck()) {
						Undo.RecordObject(this, "Change preview slice");
						m_forceVisualize.PreviewSlice = slice;
					}

					if (GUI.changed) {
						SceneView.RepaintAll();
					}
				}

				if (type == (int) ForceType.TurbulenceTexture) {
					PropField("forceTexture", new GUIContent("Force Texture", "The baked 3D force texture"));
				}
			}

			m_openClose.ToggleAreaEnd("Force Type");
		}

		public void OnSceneGUI() {
			var f = target as TCForce;

			if (m_primeForce.IsGlobalShape && f != m_primeForce || f == null || f.radius == null) {
				return;
			}

			switch (f.shape) {
				case ForceShape.Sphere:
					f.radius.Max = TCDrawFunctions.RadiusHandle(f.transform, f.radius.Max);

					if (!f.radius.IsConstant) {
						f.radius.Min = TCDrawFunctions.RadiusHandle(f.transform, f.radius.Min);
					}

					break;
				case ForceShape.Box:
					f.boxSize = TCDrawFunctions.CubeHandle(f.transform, f.boxSize);
					break;

				case ForceShape.Capsule:
					float r = f.radius.IsConstant ? f.radius.Value : f.radius.Max;
					Vector2 c = TCDrawFunctions.CapsuleHandle(f.transform, r, f.height);

					if (f.radius.IsConstant) {
						f.radius.Value = c.x;
					} else {
						f.radius.Max = c.x;
					}

					f.height = c.y;
					break;

				case ForceShape.Disc:
					float rmin = f.radius.IsConstant ? 0.0f : f.radius.Min;
					float rmax = f.radius.Max;
					TCDrawFunctions.DiscHandle(f.transform, rmin, rmax, f.discHeight, f.discRounding, (int) f.discType, out rmin,
						out rmax,
						out float round);
					f.discRounding = round;
					f.radius.Min = rmin;
					f.radius.Max = rmax;
					break;

				case ForceShape.Hemisphere:

					f.radius.Max = TCDrawFunctions.HemisphereHandle(f.transform, f.radius.Max);

					break;
			}

			if (f.forceType == ForceType.Turbulence || f.forceType == ForceType.TurbulenceTexture) {
				m_forceVisualize.DrawTurbulencePreview();
			}
		}
	}
}