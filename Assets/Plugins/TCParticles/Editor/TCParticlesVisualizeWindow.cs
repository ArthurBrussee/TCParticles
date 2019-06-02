using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	public class TCParticlesVisualizeWindow : EditorWindow {
		public bool VisualizeAll;

		[MenuItem("Window/TC Particles/Visualize window")]
		public static void ShowWindow() {
			GetWindow<TCParticlesVisualizeWindow>(false, "TC Particles visualize window");
		}

		void OnEnable() {
			EditorApplication.playModeStateChanged += v => Repaint();
		}

		void OnDisable() {
			if (!EditorApplication.isPlayingOrWillChangePlaymode) {
				var systems = FindObjectsOfType<TCParticleSystem>().Where(syst => syst.enabled).ToArray();

				foreach (var syst in systems) {
					syst.Clear();
				}
			}
		}

		void OnGUI() {
			GUI.enabled = !Application.isPlaying;

			var systems = FindObjectsOfType<TCParticleSystem>().Where(syst => syst.enabled).ToArray();

			if (systems.Length > 0) {
				VisualizeAll = true;
				foreach (var syst in systems) {
					if (!syst.DoVisualize) {
						VisualizeAll = false;
					}
				}

				EditorGUI.BeginChangeCheck();
				VisualizeAll = EditorGUILayout.Toggle("Visualize all systems", VisualizeAll);

				if (EditorGUI.EndChangeCheck()) {
					foreach (var syst in systems) {
						syst.DoVisualize = VisualizeAll;
					}
				}

				EditorGUILayout.BeginVertical("Box");

				foreach (var syst in systems) {
					if (syst == null) {
						continue;
					}

					if (EditorUtility.IsPersistent(syst)) {
						continue;
					}

					GUILayout.BeginHorizontal();

					EditorGUI.BeginChangeCheck();
					syst.DoVisualize = EditorGUILayout.Toggle(syst.name, syst.DoVisualize);

					GUILayout.Label("Time");
					EditorGUILayout.FloatField(syst.SystemTime);

					if (GUILayout.Button("↺", EditorStyles.toolbarButton, GUILayout.Width(45.0f))) {
						syst.Stop();
						syst.Play();
					}

					if (syst.IsPlaying) {
						if (GUILayout.Button("||", EditorStyles.toolbarButton, GUILayout.Width(45.0f))) {
							syst.Pause();
						}
					} else {
						if (GUILayout.Button("►", EditorStyles.toolbarButton, GUILayout.Width(45.0f))) {
							syst.Play();
						}
					}

					GUILayout.EndHorizontal();
				}

				EditorGUILayout.EndVertical();

				bool any = false;

				foreach (var syst in systems) {
					if (syst.DoVisualize && !Application.isPlaying) {
						syst.EditorUpdate();
						any = true;
					}
				}

				if (any) {
					Repaint();
					SceneView.RepaintAll();
				}
			} else {
				GUILayout.Label("Create a TC Particle system to get started!");
			}

			GUI.enabled = true;
		}

		void OnHierarchyChange() {
			Repaint();
		}
	}
}