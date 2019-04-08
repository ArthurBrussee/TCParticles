using System.Collections;
using System.Collections.Generic;
using TC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

// Main class that sets up the particle positions
public class ParticleSpawn : MonoBehaviour {
	// Types of maps we can use
	enum Map {
		Full,
		Slice,
		Sobel,
		ColDif,
		Luminance
	}

	// Name of maps in VoxelCompute.compute
	Dictionary<Map, string> MapsName = new Dictionary<Map, string> {
		{Map.Full, "FullMap" },
		{Map.Slice, "SliceMap" },
		{Map.Sobel, "SobelMap" },
		{Map.ColDif, "ColDifMap" },
		{Map.Luminance, "LuminanceMap" },
	};

	// Main shader used to apply mappings
	public ComputeShader VoxelCompute;

	// Different input clips/images
	public VideoClip Shibuya;
	public VideoClip Pendulum;
	public VideoClip Stoplight;
	public VideoClip LondonEye;
	public Texture2D GaussImage;

	// Size of the cube
	public Vector3 VideoCubeSize;

	// Resolution of the cube
    public int ResX;
	public int ResY;
	public int ResZ;
	
	// Current mapping function
	public string MapFunc;

	// Current threshold to cull voxels
	float m_alphaThreshold = 0.5f;

	// Blur mapping as a second step?
	bool m_postProcessMapGaussian;

	// Particle system used to draw the voxels
	TCParticleSystem m_system;

	// Main cube holding video data
	RenderTexture m_videoCube;
	// Cubes holding mapped data
	RenderTexture m_mappedCube;
	RenderTexture m_mappedCubePong;

	// Temp texture for video playback
	RenderTexture m_videoPlayTexture;

	// Indices of viable voxels
	ComputeBuffer m_indices;

	// Total voxel counter
	ComputeBuffer m_count;

	bool m_doneProcessing;
	float m_refreshCountdown = 0.1f;
	bool m_showUI = true;

	string m_curStep = "---";

	// Settings for some maps
	class SliceSettings {
		public float Period = 80.0f;
		public float Phase = 0.0f;
		public Vector3 Normal = new Vector3(0, 0, 1);
	}

	class ColDifSettings {
		public float Enhance = 1.0f;
	}

	SliceSettings m_sliceSettings = new SliceSettings();
	ColDifSettings m_colDifSettings = new ColDifSettings();

	void ClearResources() {
		// Create resources
		DestroyImmediate(m_videoCube);
		DestroyImmediate(m_mappedCube);
		DestroyImmediate(m_mappedCubePong);
		DestroyImmediate(m_videoPlayTexture);
		
		if (m_indices != null) {
			m_indices.Dispose();
		}
	}

	void OnDestroy() {
		ClearResources();
	}

	void Awake() {
		ClearResources();


		// Create buffers to hold the videos
		m_videoCube = CreateCubeTex(ResX, ResY, ResZ);
		m_mappedCube = CreateCubeTex(ResX, ResY, ResZ);
		m_mappedCubePong = CreateCubeTex(ResX, ResY, ResZ);

		// Create other resources
		m_videoPlayTexture = new RenderTexture(m_videoCube.width, m_videoCube.height, 0);
		m_videoPlayTexture.Create();

		m_indices = new ComputeBuffer(ResX * ResY * ResZ, sizeof(uint) * 3);
		m_count = new ComputeBuffer(1, sizeof(uint));
		m_count.SetData(new uint[] { 0 });

		m_doneProcessing = false;
		m_system = GetComponent<TCParticleSystem>();

		// Process some video to start with
		ProcessVideo(Pendulum);
	}

	void SetStep(string step) {
		Debug.Log(step);
		m_curStep = step;
	}

	RenderTexture CreateCubeTex(int width, int height, int depth) {
		var tex = new RenderTexture(width, height, 0);
		tex.dimension = TextureDimension.Tex3D;
		tex.volumeDepth = depth;
		tex.enableRandomWrite = true;
		tex.Create();
		return tex;
	}

	// Copy one texture into a slice of the cube
	void CopyTextureIntoVideoCube(Texture tex, int slice) {
		int kernel = VoxelCompute.FindKernel("CopyVideoFrame");

		VoxelCompute.SetTexture(kernel, "_VideoFrame", tex);
		VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
		VoxelCompute.SetInt("_Slice", slice);
		
		int dx = Mathf.CeilToInt(ResX / 8.0f);
		int dy = Mathf.CeilToInt(ResY / 8.0f);
		
		VoxelCompute.Dispatch(kernel, dx, dy, 1);

		VoxelCompute.SetTexture(kernel, "_VideoFrame", Texture2D.whiteTexture);
	}

	// Create video cube frame by frame
	IEnumerator ProcessVideoAsyncRoutine(VideoClip clip) {
		m_doneProcessing = false;

		// Create an object to play the video
		var go = new GameObject();
		var player = go.AddComponent<VideoPlayer>();

		SetStep("Preparing video");
		player.clip = clip;
		player.targetTexture = m_videoPlayTexture;
		player.renderMode = VideoRenderMode.RenderTexture;
		player.playOnAwake = false;
		player.sendFrameReadyEvents = true;
		player.Prepare();

		while (!player.isPrepared) {
			yield return null;
		}

		SetStep("Copying video");

		bool frameRead = false;

		player.frameReady += (vid, idx) => {
			Debug.Log("Frame ready! " + idx);
			CopyTextureIntoVideoCube(m_videoPlayTexture, (int)idx - 1);
			frameRead = true;
		};

		// Now read frame by frame
		for (int i = 0; i <= ResZ; ++i) {
			player.StepForward();
			frameRead = false;

			// Wait until we get the callback that the frame is done
			while (!frameRead) { yield return null; }
		}

		player.sendFrameReadyEvents = false;
		m_doneProcessing = true;

		DestroyImmediate(go);
	}

	// Load in an iteratively blurred image as a 'video' into the videocube
	IEnumerator ProcessImageGaussianAsyncRoutine(Texture2D image) {
		m_doneProcessing = false;

		var ping = new RenderTexture(ResX, ResY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
		ping.Create();

		var pong = new RenderTexture(ResX, ResY, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
		pong.enableRandomWrite = true;
		pong.Create();

		// Copy & downsize image
		Graphics.Blit(image, ping);
		RenderTexture.active = null;

		int kernel = VoxelCompute.FindKernel("GaussianBlur");

		int dx = Mathf.CeilToInt(ResX / 8);
		int dy = Mathf.CeilToInt(ResY / 8);

		for (int i = 0; i < ResZ; ++i) {
			CopyTextureIntoVideoCube(ping, i);

			VoxelCompute.SetTexture(kernel, "_GaussianTextureIn", ping);
			VoxelCompute.SetTexture(kernel, "_GaussianTextureOut", pong);
			VoxelCompute.SetFloat("_GaussIterSigma", 4.0f);
			VoxelCompute.Dispatch(kernel, dx, dy, 1);
			Graphics.Blit(pong, ping);

			if (i % 100 == 0) {
				yield return null;
			}
		}

		m_doneProcessing = true;
		StartCoroutine(GenerateParticles());
	}


	// Main routine to apply a map and generate the particles
	IEnumerator GenerateParticles() {
		int dx = Mathf.CeilToInt(ResX / 8.0f);
		int dy = Mathf.CeilToInt(ResY / 8.0f);
		int dz = Mathf.CeilToInt(ResZ / 8.0f);

		SetStep("Clear map");
		yield return null;

		// Clear map texture 
		{
			int kernel = VoxelCompute.FindKernel("ClearMap");
			VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
			VoxelCompute.Dispatch(kernel, dx, dy, dz);
		}


		SetStep("Apply map function");
		yield return null;

		// DoMapping: Calculates mapped texture cube
		{
			int kernel = VoxelCompute.FindKernel(MapFunc);

			// Set extra settings if needed
			if (MapFunc == MapsName[Map.Slice]) {
				VoxelCompute.SetFloat("_SlicePeriod", m_sliceSettings.Period);
				VoxelCompute.SetFloat("_SlicePhase", m_sliceSettings.Phase);
				VoxelCompute.SetVector("_SliceNormal", m_sliceSettings.Normal);
			}

			if (MapFunc == MapsName[Map.ColDif]) {
				VoxelCompute.SetFloat("_ColDifEnhance", m_colDifSettings.Enhance);
			}

			// Bind textures
			VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
			VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);

			// Now do mapping
			VoxelCompute.Dispatch(kernel, dx, dy, dz);
		}

		if (m_postProcessMapGaussian) {
			SetStep("Gauss Blur mapping");
			yield return null;

			int kernel = VoxelCompute.FindKernel("GaussianConv3");
			VoxelCompute.SetFloat("_GaussConvMapSigma", 0.9f);

			// Apply two 3D gaussian convolutions.
			VoxelCompute.SetTexture(kernel, "_VideoTexture", m_mappedCube);
			VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCubePong);
			VoxelCompute.Dispatch(kernel, dx, dy, dz);
			yield return null;

			VoxelCompute.SetTexture(kernel, "_VideoTexture", m_mappedCubePong);
			VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
			VoxelCompute.Dispatch(kernel, dx, dy, dz);
		}

		SetStep("Gather Indices");
		yield return null;

		// GatherIndices: Finds all viable voxels
		{
			// Count up from 0
			m_count.SetData(new uint[] { 0 });

			int kernel = VoxelCompute.FindKernel("GatherIndices");

			VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
			VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
			VoxelCompute.SetBuffer(kernel, "_Count", m_count);
			VoxelCompute.SetBuffer(kernel, "_FrameIndices", m_indices);
			VoxelCompute.SetFloat("_AlphaThreshold", m_alphaThreshold);
			VoxelCompute.Dispatch(kernel, dx, dy, dz);
		}

		SetStep("Emit particles");
		yield return null;

		// EmitParticles: Now create the particles and set their positions
		{
			transform.localScale = new Vector3(VideoCubeSize.x / ResX, VideoCubeSize.y / ResY, VideoCubeSize.z / ResZ);
			m_system.Emitter.Size = MinMaxRandom.Constant(1.0f);

			uint[] counterValue = new uint[1];
			m_count.GetData(counterValue);

			int particleCount = (int)counterValue[0];

			if (particleCount > m_system.MaxParticles) {
				SetStep("Error! Too many particles");
				yield break;
			}

			yield return null;

			// Clear out current particles
			m_system.Clear();

			// Construct the right amount of particles
			m_system.Emit(particleCount);
			SetStep("Emitted " + particleCount + " particles");
		}

		yield return null;

		SetStep("Set positions");
		yield return null;

		// SetPositions: Move voxels to right position in video
		{
			int kernel = VoxelCompute.FindKernel("SetPositions");

			VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
			VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
			VoxelCompute.SetBuffer(kernel, "_FrameIndices", m_indices);

			// Now for each particle fire off a kernel that copies the right position
			m_system.Manager.DispatchExtensionKernel(VoxelCompute, kernel);
		}

		SetStep("Done");
	}

	void Update() {
		// Update particles if need be
		if (m_doneProcessing && m_refreshCountdown > 0.0f) {
			m_refreshCountdown -= Time.deltaTime;

			if (m_refreshCountdown < 0.0f) {
				StartCoroutine(GenerateParticles());
			}
		}
	}

	void ProcessVideo(VideoClip clip) {
		StartCoroutine(ProcessVideoAsyncRoutine(clip));
	}

	void ProcessImageGaussian(Texture2D image) {
		StartCoroutine(ProcessImageGaussianAsyncRoutine(image));
	}


	// Simple slider GUI
	float Slider(string label, float value, float leftValue, float rightValue) {
		using (new GUILayout.HorizontalScope()) {
			GUILayout.Label(label);
			return GUILayout.HorizontalSlider(value, leftValue, rightValue, GUILayout.Width(90.0f));
		}
	}

	// Draw main UI
	void OnGUI() {
		m_showUI = GUILayout.Toggle(m_showUI, "UI");

		if (!m_showUI) {
			return;
		}
		
		using (new GUILayout.HorizontalScope()) {
			using (new GUILayout.VerticalScope("Box")) {
				// Presets
				if (GUILayout.Button("Load Shibuya Video")) {
					ProcessVideo(Shibuya);
				}

				if (GUILayout.Button("Load Pendulum Video")) {
					ProcessVideo(Pendulum);
				}

				if (GUILayout.Button("Load Traffic Light Video")) {
					ProcessVideo(Stoplight);
				}

				if (GUILayout.Button("Load London Eye Video")) {
					ProcessVideo(LondonEye);
				}

				if (GUILayout.Button("Load Gaussian Process")) {
					ProcessImageGaussian(GaussImage);
				}
			}

			using (new GUILayout.VerticalScope("Box")) {
				if (GUILayout.Button("Full Map")) {
					MapFunc = MapsName[Map.Full];
				}

				if (GUILayout.Button("Slice Map")) {
					MapFunc = MapsName[Map.Slice];
				}

				if (GUILayout.Button("Sobel Map")) {
					MapFunc = MapsName[Map.Sobel];
				}

				if (GUILayout.Button("Temporal Dif Map")) {
					MapFunc = MapsName[Map.ColDif];
				}

				if (GUILayout.Button("Threshold Map")) {
					MapFunc = MapsName[Map.Luminance];
				}
			}

			using (new GUILayout.VerticalScope("Box")) {
				GUILayout.Label("Map settings");

				m_postProcessMapGaussian = GUILayout.Toggle(m_postProcessMapGaussian, new GUIContent("Gaussian Blur Map"));

				m_alphaThreshold = Slider("Threshold", m_alphaThreshold, 0.0f, 1.0f);

				GUILayout.Space(10.0f);

				if (MapFunc == MapsName[Map.ColDif]) {
					m_colDifSettings.Enhance = Slider("Dif Enhance", m_colDifSettings.Enhance, 1.0f, 50.0f);
				}

				if (MapFunc == MapsName[Map.Slice]) {
					m_sliceSettings.Period = Slider("Slice Period", m_sliceSettings.Period, 20.0f, 150.0f);
					m_sliceSettings.Phase = Slider("Slice Phase", m_sliceSettings.Phase, -100.0f, 100.0f);

					using (new GUILayout.HorizontalScope()) {
						m_sliceSettings.Normal.x = Slider("Slice Nx", m_sliceSettings.Normal.x, -1.0f, 1.0f);
						m_sliceSettings.Normal.y = Slider("Slice Ny", m_sliceSettings.Normal.y, -1.0f, 1.0f);
						m_sliceSettings.Normal.z = Slider("Slice Nz", m_sliceSettings.Normal.z, -1.0f, 1.0f);
					}
				}
			}

			GUILayout.Label("Press RMB or CTRL to rotate camera.   Working on: " + m_curStep);

			if (GUI.changed) {
				m_refreshCountdown = 0.1f;
			}
		}
	}
}