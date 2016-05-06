using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TC.Internal {
	[Serializable]
	public class ParticleRenderer : ParticleComponent, TCParticleRenderer {
		[SerializeField] private Material _material;
		public Material Material {
			get { return _material; }
			set { _material = value; }
		}

		private Material m_cacheMaterial;

		[SerializeField] private GeometryRenderMode _renderMode;

		public GeometryRenderMode RenderMode {
			get { return _renderMode; }
			set { _renderMode = value; }
		}

		private GeometryRenderMode prevRenderMode;

		[SerializeField] private float _lengthScale = 1.0f;

		public float LengthScale {
			get { return _lengthScale; }
			set { _lengthScale = value; }
		}

		[SerializeField] private float _speedScale = 0.25f;

		public float SpeedScale {
			get { return _speedScale; }
			set { _speedScale = value; }
		}

		[SerializeField] private Mesh _mesh;

		public Mesh Mesh {
			get { return _mesh; }
			set {
				_mesh = value;
				BuildMeshBuffer();
			}
		}

		[Range(0.0f, 1.0f)] public float TailUv = 0.5f;
		private float m_prevTailUv;

		#region color

		public ParticleColourGradientMode colourGradientMode;

		[SerializeField] private Gradient _colourOverLifetime = new Gradient();

		public Gradient ColourOverLifetime {
			get { return _colourOverLifetime; }
			set {
				_colourOverLifetime = value;
				UpdateColourOverLifetime();
			}
		}
		public float maxSpeed = 1.0f;
		public float glow;



		private Texture2D colourOverLifetimeTexture;
		private Color TintColor {
			get { return _material.HasProperty("_TintColor") ? _material.GetColor("_TintColor") : Color.white; }
		}
		private Color curTintColor = Color.white;
		#endregion

		#region frustumCulling

		[SerializeField] private Bounds _bounds;

		public Bounds Bounds {
			get { return _bounds; }
			set { _bounds = value; }
		}

		[SerializeField] private bool _useFrustumCulling;

		public bool UseFrustumCulling {
			get { return _useFrustumCulling; }
			set { _useFrustumCulling = value; }
		}

		public CulledSimulationMode culledSimulationMode;

		[Range(0.02f, 0.75f)] public float cullSimulationDelta;


		public bool isPixelSize;
		public bool spriteSheetAnimation;

		public float spriteSheetColumns;
		public float spriteSheetRows;
		public float spriteSheetCycles = 1.0f;

		[NonSerialized] public bool isVisible = true;



		#endregion

		private ComputeBuffer m_meshPointsBuffer;
		private ComputeBuffer m_normalsBuffer;
		private ComputeBuffer m_uvsBuffer;

		private ComputeBuffer m_stretchBufferTail;
		private ComputeBuffer m_bufferPointsTail;
		private ComputeBuffer m_stretchBuffer;
		private ComputeBuffer m_bufferPoints;

		private int m_meshCount;

		private Shader m_prevShader;
		private Mesh m_prevMesh;
		private bool m_useNormals;


		private static readonly string[] s_keywords = new string[3];


		// Use this for initialization
		public override void Initialize() {
			//Deal with normals buffer state
			if (_material == null) {
				_material = Resources.Load("TCMaterialDefault") as Material;
			}

			m_prevShader = _material.shader;
			m_prevMesh = Mesh;
			m_cacheMaterial = new Material(m_prevShader);


			BuildBuffer();

			colourOverLifetimeTexture = new Texture2D(64, 1, TextureFormat.RGBA32, false, true)
			{wrapMode = TextureWrapMode.Clamp, anisoLevel = 0};

			if (!Application.isPlaying) {
				colourOverLifetimeTexture.hideFlags = HideFlags.HideAndDontSave;
			}

			UpdateColourOverLifetime();

			//Check if shader is a supported TC Particles shader
			if (Application.isEditor) {
				if (_material.GetTag("TCParticles", false).ToLower() != "true") {
					Debug.Log(
						"Set material is using a shader not fit for TCParticles! Please select a shader from the TCParticles menu. (Or did you not turn on DX11 in player settings?)");
				}
			}
		}

		public void UpdateColourOverLifetime() {
			if (colourOverLifetimeTexture == null) {
				return;
			}
			TCHelper.TextureFromGradient(ColourOverLifetime, colourOverLifetimeTexture, TintColor, -1.0f);
		}




		private void BuildBillboardBuffer() {
			if (m_bufferPoints != null) {
				m_bufferPoints.Release();
			}

			m_bufferPoints = new ComputeBuffer(4, 12);
			m_bufferPoints.SetData(TCParticleGlobalRender.Verts);

			if (m_stretchBuffer != null) {
				m_stretchBuffer.Release();
			}
			m_stretchBuffer = new ComputeBuffer(4, 4);
			m_stretchBuffer.SetData(TCParticleGlobalRender.StretchFacs);

			if (RenderMode == GeometryRenderMode.TailStretchBillboard) {
				if (m_bufferPointsTail != null) {
					m_bufferPointsTail.Release();
				}
				if (m_stretchBufferTail != null) {
					m_stretchBufferTail.Release();
				}

				m_bufferPointsTail = new ComputeBuffer(6, 12);
				m_stretchBufferTail = new ComputeBuffer(6, 4);

				var tailVerts = new Vector3[6];

				//Build quad of 4 triangles
				tailVerts[0] = new Vector3(0.5f, -0.5f, 0.0f);
				tailVerts[1] = new Vector3(0.5f, 0.5f, 0.0f);
				tailVerts[2] = new Vector3(TailUv - 0.5f, -0.5f, 0.0f);
				tailVerts[3] = new Vector3(TailUv - 0.5f, 0.5f, 0.0f);
				tailVerts[4] = new Vector3(-0.5f, -0.5f, 0.0f);
				tailVerts[5] = new Vector3(-0.5f, 0.5f, 0.0f);
				m_bufferPointsTail.SetData(tailVerts);
				m_stretchBufferTail.SetData(TCParticleGlobalRender.TailStretchFacs);
			}
		}

		private void BuildBuffer() {
			if (RenderMode == GeometryRenderMode.Mesh) {
				BuildMeshBuffer();
			}
			else {
				BuildBillboardBuffer();
			}
		}

		private void ReleaseBuffers() {
			Release(ref m_meshPointsBuffer);
			Release(ref m_normalsBuffer);
			Release(ref m_stretchBuffer);
			Release(ref m_stretchBufferTail);
			Release(ref m_bufferPoints);
			Release(ref m_bufferPointsTail);
		}

		private void BuildMeshBuffer() {
			if (Mesh == null) {
				return;
			}

			ReleaseBuffers();
			m_useNormals = _material.GetTag("TC_NORMALS", false, "") != "";

			var pointList = Mesh.GetTriangles(0);
			m_meshCount = pointList.Length;

			if (m_meshPointsBuffer != null) {
				m_meshPointsBuffer.Release();
			}


			m_meshPointsBuffer = new ComputeBuffer(m_meshCount, 12);


			if (m_uvsBuffer != null) {
				m_uvsBuffer.Release();
			}

			m_uvsBuffer = new ComputeBuffer(m_meshCount, 8);

			if (m_useNormals) {
				if (m_normalsBuffer != null) {
					m_normalsBuffer.Release();
				}


				m_normalsBuffer = new ComputeBuffer(m_meshCount, 12);
			}

			var setVertices = new Vector3[m_meshCount];
			var meshVertices = Mesh.vertices;
			var setUvs = new Vector2[m_meshCount];
			var meshUvs = Mesh.uv;

			Vector3[] setNormals = null;
			Vector3[] meshNormals = null;

			if (m_useNormals) {
				setNormals = new Vector3[m_meshCount];
				meshNormals = Mesh.normals;
			}

			for (int i = 0; i < m_meshCount; ++i) {
				setVertices[i] = meshVertices[pointList[i]];
				setUvs[i] = meshUvs[pointList[i]];

				if (m_useNormals) {
					setNormals[i] = meshNormals[pointList[i]];
				}
			}

			m_meshPointsBuffer.SetData(setVertices);
			m_uvsBuffer.SetData(setUvs);


			if (m_normalsBuffer != null) {
				m_normalsBuffer.SetData(setNormals);
			}
		}



		private void UpdateVisibility() {
			//TODO: Seems to be bugged? Submit bug report to unity
			Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.current);
			isVisible = GeometryUtility.TestPlanesAABB(planes, Bounds);
		}

		private bool ShouldRender() {
			if (!SystemComp.enabled || (Camera.current.cullingMask & 1 << SystemComp.gameObject.layer) <= 0) {
				return false;
			}

			isVisible = true;
			if (Manager.ParticleCount == 0) {
				return false;
			}
			if (!UseFrustumCulling) {
				return true;
			}

			UpdateVisibility();
			return isVisible;
		}

		private void UpdateTailUv() {
			if (m_prevTailUv == TailUv || RenderMode != GeometryRenderMode.TailStretchBillboard) {
				return;
			}

			BuildBillboardBuffer();
			m_prevTailUv = TailUv;
		}

		private static int s_colTex = -1;
		private static int s_lifetimeTex = -1;
		private static int s_matrixM = -1;
		private static int s_matrixV = -1;
		private static int s_maxParticles = -1;
		private static int s_maxSpeed = -1;
		private static int s_colourSpeed = -1;
		private static int s_glow = -1;
		private static int s_pixelMult = -1;

		private static int s_speedScale = -1;
		private static int s_lengthScale = -1;
		private static int s_billboardFlip = -1;
		private static int s_bufferOffset = -1;

		private void InitProperties() {
			if (s_colTex == -1) {
				s_colTex = Shader.PropertyToID("_ColTex");
				s_lifetimeTex = Shader.PropertyToID("_LifetimeTex");
				s_matrixM = Shader.PropertyToID("TC_MATRIX_M");
				s_matrixV = Shader.PropertyToID("TC_MATRIX_V");
				s_maxParticles = Shader.PropertyToID("maxParticles");
				s_maxSpeed = Shader.PropertyToID("_MaxSpeed");
				s_colourSpeed = Shader.PropertyToID("_TCColourSpeed");
				s_glow = Shader.PropertyToID("_Glow");
				s_pixelMult = Shader.PropertyToID("_PixelMult");
				s_speedScale = Shader.PropertyToID("speedScale");
				s_lengthScale = Shader.PropertyToID("lengthScale");
				s_billboardFlip = Shader.PropertyToID("_BillboardFlip");
				s_bufferOffset = Shader.PropertyToID("bufferOffset");

			}
		}

		private void RenderPass(int pass, CommandBuffer cmd) {
			int num = Manager.ParticleCount;
			if (num == 0) {
				return;
			}

			m_cacheMaterial.SetFloat(s_bufferOffset, PartEmitter.Offset);

			switch (RenderMode) {
				case GeometryRenderMode.Billboard:
				case GeometryRenderMode.StretchedBillboard:
					cmd.DrawProcedural(Matrix4x4.identity, m_cacheMaterial, pass, MeshTopology.Triangles + 1, 4, num);
					break;


				case GeometryRenderMode.TailStretchBillboard:
					cmd.DrawProcedural(Matrix4x4.identity, m_cacheMaterial, pass, MeshTopology.Triangles + 1, 6, num);
					break;


				case GeometryRenderMode.Mesh:
					cmd.DrawProcedural(Matrix4x4.identity, m_cacheMaterial, pass, MeshTopology.Triangles, m_meshCount, num);
					break;

				case GeometryRenderMode.Point:
					cmd.DrawProcedural(Matrix4x4.identity, m_cacheMaterial, pass, MeshTopology.Points, 1, num);
					break;
			}
		}

		public void FillCommandBuffer(CommandBuffer cmd) {
			if (!ShouldRender()) {
				return;
			}

			InitProperties();
			m_cacheMaterial.mainTexture = _material.mainTexture;
			if (_material.HasProperty("_Cutoff")) {
				m_cacheMaterial.SetFloat("_Cutoff", _material.GetFloat("_Cutoff"));
			}

			bool life = colourGradientMode == ParticleColourGradientMode.OverLifetime;

			switch (RenderMode) {
				case GeometryRenderMode.Billboard:
					s_keywords[0] = "TC_BILLBOARD";
					break;

				case GeometryRenderMode.StretchedBillboard:
				case GeometryRenderMode.TailStretchBillboard:
					s_keywords[0] = "TC_BILLBOARD_STRETCHED";
					break;

				case GeometryRenderMode.Mesh:
					s_keywords[0] = "TC_MESH";

					break;
			}

			s_keywords[1] = life ? "TC_COLOUR_LIFETIME" : "TC_COLOUR_SPEED";
			s_keywords[2] = spriteSheetAnimation ? "TC_UV_SPRITE_ANIM" : "TC_UV_NORMAL";

			m_cacheMaterial.shaderKeywords = s_keywords;

			if (TintColor != curTintColor) {
				curTintColor = TintColor;
				UpdateColourOverLifetime();
			}

			m_cacheMaterial.SetBuffer("particles", Manager.particles);
			m_cacheMaterial.SetTexture(s_colTex, colourOverLifetimeTexture);

			if (PartEmitter.DoSizeOverLifetime) {
				m_cacheMaterial.SetTexture(s_lifetimeTex, PartEmitter.SizeOverLifetimeTexture);
			}

			UpdateTailUv();

			//Update render state
			if (prevRenderMode != RenderMode) {
				BuildBuffer();
				prevRenderMode = RenderMode;
			}
			else if (RenderMode == GeometryRenderMode.Mesh && (Mesh != m_prevMesh || _material.shader != m_prevShader)) {
				m_prevMesh = Mesh;
				m_prevShader = _material.shader;
				BuildBuffer();
			}

			Matrix4x4 m = Matrix4x4.identity;
			switch (Manager.SimulationSpace) {
				case Space.Local:
					m = Matrix4x4.TRS(Transform.position, Transform.rotation, Vector3.one);
					break;

				case Space.Parent:
					m = Matrix4x4.TRS(ParentPosition, Transform.parent.rotation, Vector3.one);
					break;

				case Space.LocalWithScale:
					m = Matrix4x4.TRS(Transform.position, Transform.rotation, Transform.localScale);
					break;
			}

			Matrix4x4 v = Camera.current.worldToCameraMatrix;
			m_cacheMaterial.SetMatrix(s_matrixM, m);
			m_cacheMaterial.SetMatrix(s_matrixV, v);
			m_cacheMaterial.SetFloat(s_maxParticles, Manager.MaxParticles);

			if (!life) {
				m_cacheMaterial.SetFloat(s_maxSpeed, 1.0f / maxSpeed);
				m_cacheMaterial.SetFloat(s_colourSpeed, 1.0f);
			}
			else {
				m_cacheMaterial.SetFloat(s_colourSpeed, 0.0f);
			}

			float gl = glow + 1.0f;
			m_cacheMaterial.SetVector(s_glow, new Vector4(gl, gl, gl, 1.0f));


			ComputeBuffer setPointsBuffer = null;

			switch (RenderMode) {
				case GeometryRenderMode.Point:
				case GeometryRenderMode.Billboard:
				case GeometryRenderMode.StretchedBillboard:
					setPointsBuffer = m_bufferPoints;
					break;

				case GeometryRenderMode.TailStretchBillboard:
					setPointsBuffer = m_bufferPointsTail;
					break;

				case GeometryRenderMode.Mesh:
					setPointsBuffer = m_meshPointsBuffer;
					break;
			}

			m_cacheMaterial.SetBuffer("bufPoints", setPointsBuffer);
	
			if (RenderMode == GeometryRenderMode.Mesh) {
				if (m_useNormals) {
					m_cacheMaterial.SetBuffer("bufNormals", m_normalsBuffer);
				}
			}

			if (isPixelSize) {
				var pixelMult = Mathf.Max(1.0f / Screen.width, 1.0f / Screen.height);
				if (Camera.current.orthographic) {
					pixelMult *= Camera.current.orthographicSize * 2.0f;
				}

				m_cacheMaterial.SetVector(s_pixelMult, new Vector4(pixelMult, Camera.current.orthographic ? 0.0f : 1.0f));
			}
			else {
				m_cacheMaterial.SetVector(s_pixelMult, new Vector4(1.0f, 0.0f));
			}

			if (spriteSheetAnimation) {
				m_cacheMaterial.SetVector("_SpriteAnimSize",
					new Vector4(spriteSheetColumns, spriteSheetRows, 1.0f / spriteSheetColumns, 1.0f / spriteSheetRows));
				m_cacheMaterial.SetFloat("_Cycles", spriteSheetCycles);
			}


			switch (RenderMode) {
				case GeometryRenderMode.StretchedBillboard:
					m_cacheMaterial.SetBuffer("stretchBuffer", m_stretchBuffer);
					m_cacheMaterial.SetFloat(s_speedScale, SpeedScale);
					m_cacheMaterial.SetFloat(s_lengthScale, LengthScale - 1.0f);
					m_cacheMaterial.SetFloat(s_billboardFlip, 1.0f);
					break;

				case GeometryRenderMode.TailStretchBillboard:
					m_cacheMaterial.SetBuffer("stretchBuffer", m_stretchBufferTail);
					m_cacheMaterial.SetFloat(s_speedScale, SpeedScale);
					m_cacheMaterial.SetFloat(s_lengthScale, LengthScale - 1.0f);
					m_cacheMaterial.SetFloat(s_billboardFlip, -1.0f);
					break;


				case GeometryRenderMode.Mesh:
					m_cacheMaterial.SetBuffer("uvs", m_uvsBuffer);
					break;
			}


			RenderPass(0, cmd);

			if (m_cacheMaterial.passCount > 1) {
				RenderPass(1, cmd);
			}
		}

		public override void OnDestroy() {
			ReleaseBuffers();
			Object.DestroyImmediate(colourOverLifetimeTexture);
			Object.DestroyImmediate(m_cacheMaterial);
		}

		public void Update() {
			Target = null;
		}
	}
}