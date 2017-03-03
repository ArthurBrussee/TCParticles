using TC.Internal;
using UnityEngine;

[AddComponentMenu("TC Particles/Offscreen Renderer")]
[ExecuteInEditMode]
public class TCOffscreenRenderer : MonoBehaviour {
	/// <summary>
	/// The layers which to render offscreen.
	/// </summary>
	public LayerMask offscreenLayer;

	/// <summary>
	/// The factor to downsample the offscreenbuffer with
	/// </summary>
	public int downsampleFactor = 2;


	public enum CompositeMode {
		AlphaBlend,
		Gradient,
		Distort
	}

	/// <summary>
	/// The mode in which to composite the buffer back into the scene
	/// </summary>
	public CompositeMode compositeMode;

	/// <summary>
	/// Gradient describing the colours to composite the particles with
	/// </summary>
	public Gradient compositeGradient;

	/// <summary>
	/// A global tint for the compositing gradient
	/// </summary>
	public Color tint = Color.white;

	/// <summary>
	/// The maximum value of the gradient scale
	/// </summary>
	public float gradientScale = 1.0f;

	public float distortStrength;

	Texture2D m_gradientTexture;

	Camera m_cam;

	RenderTexture m_particlesRt;
	Material m_compositeMat;
	Material m_depthCopy;

	void Start() {
		GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;

		m_cam = new GameObject("PPCam", typeof(Camera)).GetComponent<Camera>();
		m_cam.enabled = false;
		m_cam.gameObject.hideFlags = HideFlags.HideAndDontSave;
		m_cam.depthTextureMode = DepthTextureMode.None;
		m_cam.cullingMask = offscreenLayer;
		m_cam.clearFlags = CameraClearFlags.SolidColor;
		m_cam.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
		m_cam.hdr = true;
		m_cam.targetTexture = m_particlesRt;
		m_cam.renderingPath = RenderingPath.Forward;

		switch (compositeMode) {
			case CompositeMode.AlphaBlend:
				m_compositeMat = new Material(Shader.Find("Hidden/TCParticles/OffscreenComposite"));
				break;

			case CompositeMode.Gradient:
				m_compositeMat = new Material(Shader.Find("Hidden/TCParticles/OffscreenCompositeGradient"));
				m_gradientTexture = new Texture2D(64, 1, TextureFormat.RGBA32, false, true) {wrapMode = TextureWrapMode.Clamp};
				UpdateCompositeGradient();
				m_compositeMat.SetTexture("_Gradient", m_gradientTexture);
				break;

			case CompositeMode.Distort:
				m_compositeMat = new Material(Shader.Find("Hidden/TCParticles/OffscreenCompositeDistort"));

				break;
		}

		m_depthCopy = new Material(Shader.Find("Hidden/TCConvertDepth"));
		GetComponent<Camera>().cullingMask &= ~offscreenLayer;
	}

	public void UpdateCompositeGradient() {
		if (m_gradientTexture == null) {
			return;
		}

		TCHelper.TextureFromGradient(compositeGradient, m_gradientTexture, tint, 0.0f);
	}

	void BindShaderVariables() {
		//update texture, for when the shader is re-compiled
		m_compositeMat.SetTexture("_Gradient", m_gradientTexture);

		switch (compositeMode) {
			case CompositeMode.Gradient:
				m_compositeMat.SetFloat("_GradientScale", 1.0f / gradientScale);
				break;

			case CompositeMode.Distort:
				m_compositeMat.SetFloat("_PxSizeX", 1.0f / Screen.width);
				m_compositeMat.SetFloat("_PxSizeY", 1.0f / Screen.height);
				m_compositeMat.SetFloat("_DistortStrength", distortStrength);
				break;

		}

		m_cam.transform.position = transform.position;
		m_cam.transform.rotation = transform.rotation;
		m_cam.GetComponent<Camera>().fieldOfView = GetComponent<Camera>().fieldOfView;
		m_cam.nearClipPlane = GetComponent<Camera>().nearClipPlane;
		m_cam.farClipPlane = GetComponent<Camera>().farClipPlane;
	}

	void OnRenderImage(RenderTexture source, RenderTexture destination) {
		BindShaderVariables();

		if (downsampleFactor == 0) {
			Graphics.Blit(source, destination);
			return;
		}

		m_particlesRt = RenderTexture.GetTemporary(Screen.width / downsampleFactor, Screen.height / downsampleFactor, 0);
		m_particlesRt.filterMode = FilterMode.Bilinear;
		m_cam.targetTexture = m_particlesRt;

		RenderTexture depth = RenderTexture.GetTemporary(Screen.width / downsampleFactor, Screen.height / downsampleFactor, 0);

		Graphics.Blit(null, depth, m_depthCopy, 0);

		Shader.SetGlobalVector("_TCRes",
			new Vector4((float) Screen.width / downsampleFactor, (float) Screen.height / downsampleFactor, 0.0f, 0.0f));
		Shader.SetGlobalTexture("_TCDepth", depth);
		m_cam.Render();

		//Composite scene and particlesRT with depth buffer
		m_compositeMat.SetPass(0);
		m_compositeMat.SetTexture("_ParticleTex", m_particlesRt);

		Graphics.Blit(source, destination, m_compositeMat);
		RenderTexture.ReleaseTemporary(m_particlesRt);
		RenderTexture.ReleaseTemporary(depth);
	}
}