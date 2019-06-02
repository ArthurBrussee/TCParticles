using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	[CustomEditor(typeof(TCParticleSystem)), CanEditMultipleObjects]
	public class TCParticleSystemEditor : TCEdtiorBase<TCParticleSystem> {
		[SerializeField] OpenClose m_tabGroup;

		protected override void OnTCEnable() {
			m_tabGroup = GetOpenClose();
		}

		enum DampingPopup {
			Constant,
			Curve
		}

		void Space() {
			GUILayout.Space(10.0f);
		}

		// Update is called once per frame
		protected override void OnTCInspectorGUI() {
			var doSim = GetProperty("_manager.m_noSimulation");
			bool sim = doSim.hasMultipleDifferentValues || !doSim.boolValue;

			if (m_tabGroup.ToggleArea("Particle Manager", new Color(1.0f, 0.8f, 0.8f))) {
				GUI.enabled = !EditorApplication.isPlaying;

				GUILayout.Space(5.0f);

				GUILayout.BeginHorizontal();

				ToolbarToggle("_manager.looping", new GUIContent("Loop", "Looping vs One shot"));
				ToolbarToggle("_manager.playOnAwake", new GUIContent("Play on awake", "Play when instantiated or play on script event"));
				ToolbarToggle("_manager.prewarm", new GUIContent("Prewarm", "Simulate particles on first frame to prevent startup issues"));
				ToolbarToggle("_manager.m_noSimulation", new GUIContent("No Simulation", "Simulation on/off. Useful when driving particles from custom shader to save performance"));

				GUILayout.Space(10.0f);

				GUI.enabled = sim;

				if (GUILayout.Button(new GUIContent("Visualize", "Open visualize window"), EditorStyles.toolbarButton)) {
					TCParticlesVisualizeWindow.ShowWindow();
				}

				GUI.enabled = true;

				GUILayout.EndHorizontal();

				GUILayout.Space(5.0f);

				PropField("_manager._duration", new GUIContent("Manager Life", "Seconds that the system is playing / loops"));
				PropField("_manager.delay", new GUIContent("Start Delay", "Seconds before the system starts playing"));

				GUI.enabled = true;

				PropField("_manager._maxParticles", new GUIContent("Max particles", "Maximum amount of particles the system has at any point in time. Keep low to increase performance"));

				Space();

				PropField("_manager._simulationSpace",
					new GUIContent("Simulation Space",
						"The space the particles are simulated in. Local will move particles with the system, global will make them independent"));

				if (sim) {
					PropField("_emitter._inheritVelocity",
						new GUIContent("Inherit Velocity", "Factor of movement from the system to be inherited for the particles"));
				}
			}

			m_tabGroup.ToggleAreaEnd("Particle Manager");

			if (m_tabGroup.ToggleArea("Emission", new Color(0.8f, 1.0f, 0.8f))) {
				PropField("_emitter.emit",
					new GUIContent("Do Emit", "Determines whether the system should be emitting particles right now"));

				GUILayout.BeginHorizontal();

				PropField("_emitter._emissionRate",
					new GUIContent("Emission Rate", "Amount of particles to emit per second or unit"));
				PropField("_emitter.m_emissionType",
					new GUIContent("", "Determines whether particles are emitter per second or per unit"), GUILayout.Width(80.0f));

				GUILayout.EndHorizontal();

				foreach (TCParticleSystem t in targets) {
					t.Emitter.EmissionRate = Mathf.Clamp(t.Emitter.EmissionRate, 0, 5000000);
				}

				SerializedProperty bursts = GetProperty("_emitter.bursts");

				if (!bursts.hasMultipleDifferentValues) {
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label("Bursts", GUILayout.Width(80.0f));
					const float width = 51.0f;
					GUILayout.Label("Time", GUILayout.Width(width));
					GUILayout.Label("Particles", GUILayout.Width(width));
					EditorGUILayout.EndHorizontal();

					int del = -1;
					for (int i = 0; i < bursts.arraySize; ++i) {
						GUILayout.BeginHorizontal();
						GUILayout.Space(85.0f);
						EditorGUILayout.PropertyField(CheckBurstProp("time", i), new GUIContent(""), GUILayout.Width(width));
						EditorGUILayout.PropertyField(CheckBurstProp("amount", i), new GUIContent(""), GUILayout.Width(width));
						GUILayout.Space(10.0f);

						if (GUILayout.Button("", "OL Minus", GUILayout.Width(24.0f))) {
							del = i;
						}

						GUILayout.EndHorizontal();
					}

					GUILayout.BeginHorizontal();
					GUILayout.Space(103.0f + 2 * width);
					if (GUILayout.Button("", "OL Plus", GUILayout.Width(24.0f))) {
						bursts.InsertArrayElementAtIndex(bursts.arraySize);
					}

					GUILayout.EndHorizontal();

					if (del != -1) {
						bursts.DeleteArrayElementAtIndex(del);
					}

					Space();
				}

				PropField("_emitter.pes", new GUIContent("", "Emitter Shape"));
				GUILayout.Label("Shape emission", EditorStyles.boldLabel);
				PropField("_emitter.m_emitTag", new GUIContent("Emit Tag", "The tag to link with shape emitters"));
			}

			m_tabGroup.ToggleAreaEnd("Emission");

			if (m_tabGroup.ToggleArea("Particles", new Color(0.0f, 0.8f, 1.0f))) {
				if (sim) {
					PropField("_emitter._energy", new GUIContent("Lifetime", "Amount of seconds particles can be alive"));

					GUILayout.Space(10.0f);

					PropField("_emitter._speed", new GUIContent("Start Speed", "Speed particles start with when emitted"));
					PropField("_emitter._velocityOverLifetime",
						new GUIContent("Velocity over lifetime", "Velocity and direction over lifetime. Lifetime is [0...1]"));

					Space();
				}

				PropField("_emitter._size", new GUIContent("Start Size", "Size in units that particles start with"));

				if (sim) {
					PropField("_emitter._sizeOverLifetime",
						new GUIContent("Size Over Lifetime",
							"A factor [0..1] to multiply the base size with over the lifetime of the particle [0..1]"));
				}

				Space();

				PropField("_emitter._rotation", new GUIContent("Start Rotation", "The rotation a particle starts with"));

				if (sim) {
					PropField("_emitter._angularVelocity",
						new GUIContent("Angular Velocity", "Degrees per second all particles rotate with"));
				}

				if (sim) {
					GUILayout.Space(10.0f);

					GUILayout.BeginHorizontal();
					var mode = GetProperty("_particleRenderer.colourGradientMode");
					var colProp = GetProperty("_particleRenderer._colourOverLifetime");

					switch (mode.enumValueIndex) {
						case (int) ParticleColourGradientMode.OverLifetime:
							EditorGUILayout.PropertyField(colProp, new GUIContent("Color over lifetime"));
							break;
						default:
							EditorGUILayout.PropertyField(colProp, new GUIContent("Color over speed"));
							break;
					}

					EnumPopup(mode, (ParticleColourGradientMode) mode.enumValueIndex);

					GUILayout.EndHorizontal();

					if (mode.enumValueIndex != (int) ParticleColourGradientMode.OverLifetime) {
						EditorGUILayout.PropertyField(GetProperty("_particleRenderer.maxSpeed"),
							new GUIContent("Max Speed", "The speed where particles are colored like the end of the gradient"));
					}
				}
			}

			m_tabGroup.ToggleAreaEnd("Particles");

			if (sim) {
				if (m_tabGroup.ToggleArea("Forces", new Color(1.0f, 1.0f, 0.8f))) {
					PropField("_forcesManager._maxForces",
						new GUIContent("Max Forces", "The maximum number of forces that can affect this system, determined by priority"));

					var maxProp = GetProperty("_forcesManager._maxForces");

					if (!maxProp.hasMultipleDifferentValues && maxProp.intValue != 0) {
						PropField("_manager.gravityMultiplier",
							new GUIContent("Gravity Multiplier",
								"The amount of gravity (determined in physics settings) applied to the particles"));
						PropField("_emitter._constantForce",
							new GUIContent("Constant Force",
								"A force that is constantly applied to the particles, accelerating them in one direction"));
					}

					EditorGUILayout.BeginHorizontal();
					PropField("_forcesManager._forceLayers",
						new GUIContent("Force Layers", "Layers to filter what forces can affect this system"));

					if (Targets.Length == 1) {
						if (GUILayout.Button("", "OL Plus", GUILayout.Width(20.0f))) {
							Targets[0].ForceManager.BaseForces.Add(null);
						}
					}

					EditorGUILayout.EndHorizontal();

					if (Targets.Length == 1) {
						var forceManager = Targets[0].ForceManager;

						int del = -1;

						for (int i = 0; i < forceManager.BaseForces.Count; ++i) {
							GUILayout.BeginHorizontal();
							GUILayout.Space(20.0f);

							forceManager.BaseForces[i] =
								EditorGUILayout.ObjectField("Link to ", forceManager.BaseForces[i], typeof(TCForce), true) as TCForce;

							if (GUILayout.Button("", "OL Minus", GUILayout.Width(20.0f))) {
								del = i;
							}

							GUILayout.EndHorizontal();
						}

						if (del != -1) {
							forceManager.BaseForces.RemoveAt(del);
						}

						forceManager.MaxForces = Mathf.Max(forceManager.MaxForces, forceManager.BaseForces.Count);
					}

					GUILayout.BeginHorizontal();
					SerializedProperty curveProp = GetProperty("_manager.dampingIsCurve");
					EditorGUILayout.PropertyField(
						curveProp.boolValue ? GetProperty("_manager.dampingCurve") : GetProperty("_manager.damping"),
						new GUIContent("Damping"));

					curveProp.boolValue =
						(DampingPopup)
						EditorGUILayout.EnumPopup("", curveProp.boolValue ? DampingPopup.Curve : DampingPopup.Constant,
							EditorStyles.toolbarPopup,
							GUILayout.Width(15.0f)) == DampingPopup.Curve;

					GUILayout.EndHorizontal();

					PropField("_manager.MaxSpeed", new GUIContent("Max Speed", "The speed particles are clamped to. -1 for infinity"));

					PropField("_forcesManager.useBoidsFlocking",
						new GUIContent("Boids Flocking",
							"Determines whether the particles should flock. Flocking gives a firefly like behavior to the particles"));

					if (GetProperty("_forcesManager.useBoidsFlocking").boolValue) {
						PropField("_forcesManager.boidsPositionStrength",
							new GUIContent("Position Strength",
								"The strength at which particles are pushed to the average position of all particles"));
						PropField("_forcesManager.boidsVelocityStrength",
							new GUIContent("Velocity Strength",
								"The strength at which particles are forced to the average velocity of all particles"));
						PropField("_forcesManager.boidsCenterStrength",
							new GUIContent("Center Position Strength",
								"The strength at which particles are pushed to the position of the particle system"));
					}
				}

				m_tabGroup.ToggleAreaEnd("Forces");

				if (m_tabGroup.ToggleArea("Collision", new Color(1.0f, 0.8f, 1.0f))) {
					PropField("_colliderManager._maxColliders",
						new GUIContent("Max Colliders",
							"The maximum number of colliders that can affect this system, determined by priority"));

					var maxProp = GetProperty("_colliderManager._maxColliders");

					if (!maxProp.hasMultipleDifferentValues && maxProp.intValue != 0) {
						PropField("_colliderManager.overrideBounciness", new GUIContent("Override Bounciness"));

						if (GetProperty("_colliderManager.overrideBounciness").boolValue) {
							PropField("_colliderManager._bounciness",
								new GUIContent("Bounciness", "Amount that particles bounce back after an collision"));
						}

						PropField("_colliderManager.overrideStickiness", new GUIContent("Override stickiness"));

						if (GetProperty("_colliderManager.overrideStickiness").boolValue) {
							PropField("_colliderManager._stickiness",
								new GUIContent("Bounciness", "Amount that particles stick after an collision"));
						}

						PropField("_colliderManager._particleThickness", new GUIContent("Particle Thickness"));
					}

					EditorGUILayout.BeginHorizontal();
					PropField("_colliderManager._colliderLayers",
						new GUIContent("Collider Layers", "Layers to filter which colliders can affect this system"));

					if (Targets.Length == 1) {
						if (GUILayout.Button("", "OL Plus", GUILayout.Width(20.0f))) {
							Targets[0].ColliderManager.BaseColliders.Add(null);
						}
					}

					EditorGUILayout.EndHorizontal();

					if (Targets.Length == 1) {
						var baseColliders = Targets[0].ColliderManager.BaseColliders;

						int del = -1;
						for (int i = 0; i < baseColliders.Count; ++i) {
							GUILayout.BeginHorizontal();
							GUILayout.Space(20.0f);
							baseColliders[i] =
								EditorGUILayout.ObjectField("Link to ", baseColliders[i], typeof(TCCollider), true) as TCCollider;

							if (GUILayout.Button("", "OL Minus", GUILayout.Width(20.0f))) {
								del = i;
							}

							GUILayout.EndHorizontal();
						}

						if (del != -1) {
							baseColliders.RemoveAt(del);
						}
					}

					foreach (var system in Targets) {
						system.ColliderManager.MaxColliders = Mathf.Max(system.ColliderManager.MaxColliders,
							system.ColliderManager.BaseColliders.Count);
					}
				}

				m_tabGroup.ToggleAreaEnd("Collision");
			}

			if (m_tabGroup.ToggleArea("Renderer", new Color(0.8f, 1.0f, 1.0f))) {
				PropField("_particleRenderer._material", new GUIContent("Material"));
				PropField("_particleRenderer._renderMode", new GUIContent("Render Mode"));

				var shapeProp = GetProperty("_emitter.pes.shape");
				if (!shapeProp.hasMultipleDifferentValues && (EmitShapes) shapeProp.enumValueIndex == EmitShapes.PointCloud) {
					PropField("_particleRenderer.pointCloudNormals", new GUIContent("Point Cloud Normals"));
				}

				var renderMode = (GeometryRenderMode) GetProperty("_particleRenderer._renderMode").enumValueIndex;

				switch (renderMode) {
					case GeometryRenderMode.Mesh:
						PropField("_particleRenderer._mesh",
							new GUIContent("Mesh", "The mesh that is used for the particles to render with. Keep as low poly as possible"));
						break;
					case GeometryRenderMode.TailStretchBillboard:
					case GeometryRenderMode.StretchedBillboard:
						PropField("_particleRenderer._lengthScale",
							new GUIContent("Length Scale", "Factor that determines the length when particles has no velocity"));
						PropField("_particleRenderer._speedScale",
							new GUIContent("Speed Scale", "Factor that determines how much a particle should stretch when it has velocity"));
						if (renderMode == GeometryRenderMode.TailStretchBillboard) {
							PropField("_particleRenderer.TailUv",
								new GUIContent("Tail UV", "The UV Coordinate of the vertex where the particles begins to stretch"));
						}

						break;
				}

				if (renderMode == GeometryRenderMode.Billboard || renderMode == GeometryRenderMode.StretchedBillboard ||
				    renderMode == GeometryRenderMode.TailStretchBillboard) {
					PropField("_particleRenderer.isPixelSize",
						new GUIContent("Is Pixel Size", "Determines whether the particle size is in units or screen pixels"));

					if (sim) {
						PropField("_particleRenderer.spriteSheetAnimation",
							new GUIContent("Sprite Sheet", "Enables sprite sheet animation"));
					}
				}

				if (sim) {
					if (GetProperty("_particleRenderer.spriteSheetAnimation").boolValue) {
						PropField("_particleRenderer.spriteSheetColumns",
							new GUIContent("Sheet Columns", "Nr. of columns in the sprite sheet"));
						PropField("_particleRenderer.spriteSheetRows", new GUIContent("Sheet rows", "Nr. of rows in the sprite sheet"));
						PropField("_particleRenderer.spriteSheetCycles",
							new GUIContent("Sheet cycles", "Amount of cycles a particle should do in it's lifetime"));

						PropField("_particleRenderer.spriteSheetBasePlaySpeed",
							new GUIContent("Base Play Speed", "Base speed sprite sheet animation plays at. Useful for particles that life infinitely"));

						PropField("_particleRenderer.spriteSheetRandomStart",
							new GUIContent("Random Start", "Should the sprite sheet start playing at a random point in the sprite sheet?"));
					}
				}

				PropField("_particleRenderer.glow", new GUIContent("Glow Strength", "Glow strength of the particles"));
				PropField("_particleRenderer._useFrustumCulling",
					new GUIContent("Frustum culling", "Determines whether the system should be frustum culled"));

				//If supported...

				PropField("_particleRenderer.CastShadows", new GUIContent("Cast Shadows", "Should this system cast shadows"));
				PropField("_particleRenderer.ReceiveShadows", new GUIContent("Receive Shadows", "Should particles receive shadows"));

				if (GetProperty("_particleRenderer._useFrustumCulling").boolValue) {
					SerializedProperty boundsProp = GetProperty("_particleRenderer._bounds");
					boundsProp.boundsValue = EditorGUILayout.BoundsField(boundsProp.boundsValue);

					PropField("_particleRenderer.culledSimulationMode",
						new GUIContent("Culled Mode", "Determines how particles are treated when they are culled"));

					if (GetProperty("_particleRenderer.culledSimulationMode").enumValueIndex ==
					    (int) CulledSimulationMode.SlowSimulation) {
						PropField("_particleRenderer.cullSimulationDelta",
							new GUIContent("Culled Delta", "Sets the frequency the particles are update when they are culled"));
					}
				}
			}

			m_tabGroup.ToggleAreaEnd("Renderer");

			if (sim) {
				foreach (var o in targets) {
					var t = (TCParticleSystem) o;
					t.ParticleRenderer.UpdateColourOverLifetime();
					t.Emitter.UpdateSizeOverLifetime();
				}
			}

			serializedObject.ApplyModifiedProperties();

			if (GUI.changed) {
				EditorUtility.SetDirty(m_tabGroup);
			}
		}

		void OnSceneGUI() {
			var system = target as TCParticleSystem;
			if (system == null) {
				return;
			}

			var emitter = system.Emitter;
			if (emitter == null) {
				return;
			}

			var transform = system.transform;
			Undo.RecordObject(target, "Resize TC Particle emitter");
			TCDrawFunctions.DrawEmitterShape(emitter.ShapeData, transform);
		}

		[DrawGizmo(GizmoType.InSelectionHierarchy)]
		static void RenderGizmo(TCParticleSystem syst, GizmoType type) {
			if (syst.ParticleRenderer == null) {
				return;
			}

			if (syst.ParticleRenderer.UseFrustumCulling) {
				Gizmos.DrawWireCube(syst.transform.position + syst.ParticleRenderer.Bounds.center,
					syst.ParticleRenderer.Bounds.extents);
			}
		}
	}
}