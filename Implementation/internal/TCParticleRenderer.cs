using System;
using System.ComponentModel;
using System.Linq;
using TC.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TC {
	/// <summary>
	/// Class containing rendering settings for the TC Particle system
	/// </summary>
	[Serializable]
	public class ParticleRenderer : ParticleComponent {
		[SerializeField] Material _material;

		/// <summary>
		/// Current material used by the renderer
		/// </summary>
		public Material Material {
			get => _material;
			set => _material = value;
		}

		[SerializeField] GeometryRenderMode _renderMode;

		/// <summary>
		/// Render mode for the particles
		/// </summary>
		public GeometryRenderMode RenderMode {
			get => _renderMode;
			set => _renderMode = value;
		}

		GeometryRenderMode m_prevRenderMode;

		[SerializeField] float _lengthScale = 1.0f;

		/// <summary>
		/// Factor to strech out particles when <see cref="RenderMode"/> is set to (Tail)StretchedBilloard
		/// </summary>
		public float LengthScale {
			get => _lengthScale;
			set => _lengthScale = value;
		}

		[SerializeField] float _speedScale = 0.25f;

		/// <summary>
		/// Factor to strech out particles when <see cref="RenderMode"/> is set to (Tail)StretchedBilloard bassed on the particles speed
		/// </summary>
		public float SpeedScale {
			get => _speedScale;
			set => _speedScale = value;
		}

		[SerializeField] Mesh _mesh;

		/// <summary>
		/// Mesh used when <see cref="RenderMode"/> is set to Mesh
		/// </summary>
		public Mesh Mesh {
			get => _mesh;
			set {
				_mesh = value;
				BuildBuffer();
			}
		}

		/// <summary>
		/// Uv point at which to stretch when <see cref="RenderMode"/> is set to TailStretchedBillboard
		/// </summary>
		[Range(0.0f, 1.0f)] public float TailUv = 0.5f;

		float m_prevTailUv;

		/// <summary>
		/// Gradient mode used for the color over lifetime/speed gradient
		/// </summary>
		public ParticleColourGradientMode colourGradientMode;

		[SerializeField] Gradient _colourOverLifetime = new Gradient();

		/// <summary>
		/// Current gradient, used for color over speed/lifetime base on <see cref="colourGradientMode" />
		/// </summary>
		public Gradient ColorOverLifetime {
			get => _colourOverLifetime;
			set {
				_colourOverLifetime = value;
				UpdateColourOverLifetime();
			}
		}

		/// <summary>
		/// Scale of the gradient when <see cref="colourGradientMode" /> is set to colour over speed
		/// </summary>
		public float maxSpeed = 1.0f;

		/// <summary>
		/// Brightness of the particles
		/// </summary>
		public float glow;

		Texture2D colorOverLifetimeTexture;
		Color TintColor => _material.HasProperty("_TintColor") ? _material.GetColor("_TintColor") : Color.white;

		Color curTintColor = Color.white;
		[SerializeField] Bounds _bounds;

		/// <summary>
		/// Renderer bounds used for frustum culling when <see cref="UseFrustumCulling"/> is on.
		/// </summary>
		public Bounds Bounds {
			get => _bounds;
			set => _bounds = value;
		}

		[SerializeField] bool _useFrustumCulling;

		/// <summary>
		/// Whether to use frustum culling
		/// </summary>
		public bool UseFrustumCulling {
			get => _useFrustumCulling;
			set => _useFrustumCulling = value;
		}

		/// <summary>
		/// Should this system cast shadows, when using a supported shader
		/// </summary>
		public ShadowCastingMode CastShadows;

		/// <summary>
		/// Should this system receive shadows, when using a supported shader
		/// </summary>
		public bool ReceiveShadows;

		/// <summary>
		/// Mode the system goes in when it is frustum culled
		/// </summary>
		public CulledSimulationMode culledSimulationMode;

		/// <summary>
		/// Delta time to use when <see cref="culledSimulationMode"/> is set to SlowSimulation.
		/// </summary>
		[Range(0.02f, 0.75f)] public float cullSimulationDelta;

		/// <summary>
		/// Bind the point cloud data Normals as a buffer. WARNING: These normals are directly from the ply, and not stored in the particles.
		/// </summary>
		public bool pointCloudNormals;

		/// <summary>
		/// Is the size specified for the particles in pixels
		/// </summary>
		public bool isPixelSize;

		/// <summary>
		/// Use sprite sheet animation for this particle system
		/// </summary>
		public bool spriteSheetAnimation;

		/// <summary>
		/// nr. Of columns on animation sprite sheet
		/// </summary>
		public float spriteSheetColumns;

		/// <summary>
		/// Nr. of rows on animation sprite sheet
		/// </summary>
		public float spriteSheetRows;

		/// <summary>
		/// Nr. of times the particle goes through the sprite sheet in one lifetime.
		/// </summary>
		public float spriteSheetCycles = 1.0f;

		/// <summary>
		/// Do particles start at a random point in the sprite sheet?
		/// </summary>
		public bool spriteSheetRandomStart;

		/// <summary>
		/// Speed at which the system always plays sprite sheets. Useful for particles with infinite lifetime
		/// </summary>
		public float spriteSheetBasePlaySpeed;

		/// <summary>
		/// Was this particle renderer visible last frame
		/// </summary>
		public bool IsVisible { get; private set; }

		uint[] m_argsData = new uint[5];
		ComputeBuffer m_argsBuffer;

		ComputeBuffer m_customNormalsBuffer;

		Mesh m_tailStretchMesh;
		Mesh m_prevRenderModeMesh;

		Mesh m_particleMesh;
		Material m_cacheMaterial;

		static Plane[] s_planes = new Plane[6];

		public delegate void OnSetMaterialCB(Material mat);

		public OnSetMaterialCB OnSetMaterial;

		public bool DoRender = true;

		[EditorBrowsable(EditorBrowsableState.Never)]
		protected override void Initialize() {
			if (_material == null) {
				_material = Resources.Load("TCMaterialDefault") as Material;
			}

			Debug.Assert(_material != null, "TCMaterialDefault must exist in your TCParticles installation!");
			
			m_cacheMaterial = new Material(_material.shader);
			m_prevRenderModeMesh = Mesh;
			m_argsBuffer = new ComputeBuffer(5, 4, ComputeBufferType.IndirectArguments);

			BuildBuffer();

			colorOverLifetimeTexture = new Texture2D(128, 1, TextureFormat.RGBA32, false, true)
				{wrapMode = TextureWrapMode.Clamp, anisoLevel = 0};
			colorOverLifetimeTexture.hideFlags = HideFlags.HideAndDontSave;

			UpdateColourOverLifetime();
		}

		static Color[] s_colours = new Color[c_dimSize];
		const int c_dimSize = 128;

		//Prodcues a texture of resolution of dimx1 encoding a gradient
		public static void TextureFromGradient(Gradient gradient, Texture2D toSet, Color tint) {
			if (toSet == null) {
				return;
			}

			for (int i = 0; i < c_dimSize; ++i) {
				s_colours[i] = gradient.Evaluate(i / (c_dimSize - 1.0f)) * tint;
			}

			toSet.SetPixels(s_colours);
			toSet.Apply();
		}

		/// <summary>
		/// Update the colour over lifetime texture. Should be called whenever changing the colour over lifetime gradient
		/// </summary>
		public void UpdateColourOverLifetime() {
			TextureFromGradient(ColorOverLifetime, colorOverLifetimeTexture, TintColor);
		}

		void BuildBuffer() {
			if (RenderMode == GeometryRenderMode.TailStretchBillboard) {
				if (m_tailStretchMesh == null) {
					m_tailStretchMesh = new Mesh();
					m_tailStretchMesh.hideFlags = HideFlags.HideAndDontSave;
				}

				var tailVerts = new[] {
					new Vector3(0.5f, -0.5f, 0.0f),
					new Vector3(0.5f, 0.5f, 0.0f),

					new Vector3(TailUv - 0.5f, -0.5f, 0.0f),
					new Vector3(TailUv - 0.5f, 0.5f, 0.0f),

					new Vector3(-0.5f, -0.5f, 0.0f),
					new Vector3(-0.5f, 0.5f, 0.0f)
				};

				m_tailStretchMesh.vertices = tailVerts;
				m_tailStretchMesh.uv = tailVerts.Select(v => (Vector2) v + new Vector2(0.5f, 0.5f)).ToArray();
				m_tailStretchMesh.uv2 = new[] {Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, Vector2.one};
				m_tailStretchMesh.triangles = new[] {0, 1, 2, 2, 1, 3, 4, 2, 3, 4, 3, 5}.Reverse().ToArray();
			}
		}

		void ReleaseBuffers() {
			Release(ref m_argsBuffer);
			Release(ref m_customNormalsBuffer);
		}

		void UpdateTailUv() {
			if (RenderMode != GeometryRenderMode.TailStretchBillboard || Mathf.Approximately(m_prevTailUv, TailUv)) {
				return;
			}

			BuildBuffer();
			m_prevTailUv = TailUv;
		}

		internal void Update() {
			//Reset flag for next frame
			IsVisible = false;
		}

		void ToggleKeyword(string keyword, bool enabled) {
			if (enabled) {
				m_cacheMaterial.EnableKeyword(keyword);
			} else {
				m_cacheMaterial.DisableKeyword(keyword);
			}
		}

		void ChooseKeyword(string keyword0, string keyword1, string keyword2, int index) {
			ToggleKeyword(keyword0, index == 0);
			ToggleKeyword(keyword1, index == 1);
			ToggleKeyword(keyword2, index == 2);
		}

		internal void SetupDrawCall(Camera cam) {
			if (UseFrustumCulling) {
				GeometryUtility.CalculateFrustumPlanes(cam.projectionMatrix * cam.worldToCameraMatrix, s_planes);
				IsVisible |= GeometryUtility.TestPlanesAABB(s_planes, Bounds);
			}

			if (!SystemComp.enabled || SystemComp.ParticleCount == 0 || !DoRender || cam.cameraType == CameraType.Preview) {
				return;
			}

			if (m_cacheMaterial.shader != _material.shader) {
				m_cacheMaterial = new Material(_material.shader);
			}

			m_cacheMaterial.CopyPropertiesFromMaterial(_material);

			int index = -1;

			switch (RenderMode) {
				case GeometryRenderMode.Billboard:
					index = 0;
					break;

				case GeometryRenderMode.StretchedBillboard:
				case GeometryRenderMode.TailStretchBillboard:
					index = 1;
					break;

				case GeometryRenderMode.Mesh:
					index = 2;
					break;
			}

			ChooseKeyword("TC_BILLBOARD", "TC_BILLBOARD_STRETCHED", "TC_MESH", index);

			m_cacheMaterial.SetInt("_UvSpriteAnimation", spriteSheetAnimation ? 1 : 0);

			//enable sprite uv animation keyword
			if (spriteSheetAnimation) {
				m_cacheMaterial.SetVector(SID._SpriteAnimSize, new Vector4(spriteSheetColumns, spriteSheetRows, 1.0f / spriteSheetColumns, 1.0f / spriteSheetRows));
				m_cacheMaterial.SetFloat(SID._Cycles, spriteSheetCycles);
				m_cacheMaterial.SetFloat(SID._SpriteSheetBaseSpeed, spriteSheetBasePlaySpeed);
				m_cacheMaterial.SetFloat(SID._SpriteSheetRandomStart, spriteSheetRandomStart ? 1.0f : 0.0f);
			}

			Color tint = TintColor;

			if (tint != curTintColor) {
				curTintColor = tint;
				UpdateColourOverLifetime();
			}

			m_cacheMaterial.SetBuffer("particlesRead", Manager.GetParticlesBuffer());

			var cloud = Emitter.PointCloud;

			bool usePointCloudNormals = pointCloudNormals &&
			                            cloud != null &&
			                            cloud.Normals != null &&
			                            Emitter.Shape == EmitShapes.PointCloud;

			if (usePointCloudNormals) {
				if (m_customNormalsBuffer == null || m_customNormalsBuffer.count != cloud.PointCount) {
					m_customNormalsBuffer = new ComputeBuffer(cloud.PointCount, 12);
					m_customNormalsBuffer.SetData(cloud.Normals);
				}

				m_cacheMaterial.SetBuffer("_CustomNormalsBuffer", m_customNormalsBuffer);
			}

			ToggleKeyword("TC_CUSTOM_NORMAL_ORIENT", usePointCloudNormals);

			m_cacheMaterial.SetTexture(SID._ColTex, colorOverLifetimeTexture);

			if (Emitter.DoSizeOverLifetime) {
				m_cacheMaterial.SetTexture(SID._LifetimeTexture, Emitter.LifetimeTexture);
			}

			UpdateTailUv();

			//Update render state
			if (m_prevRenderMode != RenderMode) {
				BuildBuffer();
				m_prevRenderMode = RenderMode;
			} else if (RenderMode == GeometryRenderMode.Mesh && Mesh != m_prevRenderModeMesh) {
				m_prevRenderModeMesh = Mesh;
				BuildBuffer();
			}

			Matrix4x4 m;
			Transform systTransform = SystemComp.transform;

			switch (Manager.SimulationSpace) {
				case Space.Local:
					m = Matrix4x4.TRS(systTransform.position, systTransform.rotation, Vector3.one);
					break;

				case Space.Parent:
					m = Matrix4x4.TRS(ParentPosition, systTransform.parent.rotation, Vector3.one);
					break;

				case Space.LocalWithScale:
					m = Matrix4x4.TRS(systTransform.position, systTransform.rotation, systTransform.localScale);
					break;

				default:
					m = Matrix4x4.identity;
					break;
			}

			m_cacheMaterial.SetMatrix(SID.TC_MATRIX_M, m);
			m_cacheMaterial.SetMatrix(SID.TC_MATRIX_M_INV, m.inverse);
			m_cacheMaterial.SetFloat(SID._MaxSpeed, 1.0f / maxSpeed);

			bool colorOverLife = colourGradientMode == ParticleColourGradientMode.OverLifetime;
			m_cacheMaterial.SetFloat(SID._ColorSpeedLerp, colorOverLife ? 0 : 1);

			float gl = glow + 1.0f;
			m_cacheMaterial.SetVector(SID._Glow, new Vector4(gl, gl, gl, 1.0f));
			m_cacheMaterial.SetVector(SID._LifeMinMax, new Vector4(Emitter.Lifetime.Min, Emitter.Lifetime.Max));

			switch (RenderMode) {
				case GeometryRenderMode.Billboard:
				case GeometryRenderMode.StretchedBillboard:
					m_cacheMaterial.SetFloat(SID._SpeedScale, SpeedScale);
					m_cacheMaterial.SetFloat(SID._LengthScale, LengthScale - 1.0f);
					m_particleMesh = TCParticleGlobalManager.GlobalQuad;
					break;

				case GeometryRenderMode.TailStretchBillboard:
					m_cacheMaterial.SetFloat(SID._SpeedScale, SpeedScale);
					m_cacheMaterial.SetFloat(SID._LengthScale, LengthScale - 1.0f);
					m_particleMesh = m_tailStretchMesh;
					break;

				case GeometryRenderMode.Mesh:
					m_particleMesh = _mesh;
					break;
			}

			m_cacheMaterial.SetInt(SID._BufferOffset, Emitter.Offset);
			m_cacheMaterial.SetInt(SID._MaxParticles, Manager.MaxParticles);

			if (OnSetMaterial != null) {
				OnSetMaterial(m_cacheMaterial);
			}

			if (m_particleMesh == null) {
				return;
			}

			m_argsData[0] = m_particleMesh.GetIndexCount(0);
			m_argsData[1] = (uint) SystemComp.ParticleCount;
			m_argsData[2] = 0;
			m_argsData[3] = 0;
			m_argsData[4] = 0;
			m_argsBuffer.SetData(m_argsData);

			//Setup proper size mult depending on camera ortho size
			if (isPixelSize) {
				var pixelMult = Mathf.Max(1.0f / Screen.width, 1.0f / Screen.height);
				if (cam.orthographic) {
					pixelMult *= cam.orthographicSize * 2.0f;
				}

				m_cacheMaterial.SetVector(SID._PixelMult, new Vector4(pixelMult, cam.orthographic ? 0.0f : 1.0f));
			} else {
				m_cacheMaterial.SetVector(SID._PixelMult, new Vector4(1.0f, 0.0f));
			}

			//Setup DrawM
			Bounds bounds = UseFrustumCulling ? _bounds : new Bounds(Vector3.zero, Vector3.one * 100000);
			var layer = SystemComp.gameObject.layer;
			Graphics.DrawMeshInstancedIndirect(m_particleMesh, 0, m_cacheMaterial, bounds, m_argsBuffer, 0, null, CastShadows, ReceiveShadows, layer, cam);
		}

		internal void OnDestroy() {
			ReleaseBuffers();
			Object.DestroyImmediate(colorOverLifetimeTexture);
		}
	}
}