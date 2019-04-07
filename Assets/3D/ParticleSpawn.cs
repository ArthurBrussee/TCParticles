using System.Collections;
using TC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

public class ParticleSpawn : MonoBehaviour {
	public ComputeShader VoxelCompute;
	public VideoClip Clip;
    public Vector3 VideoCubeSize;

	public Texture2D Image;

    public int ResX;
	public int ResY;
	public int ResZ;
	
	public string MapFunc;
	
	TCParticleSystem m_system;

	public RenderTexture m_videoCube;
	public RenderTexture m_mappedCube;
	public RenderTexture m_videoPlayTexture;

	ComputeBuffer m_indices;
	ComputeBuffer m_count;

	bool m_videoDone;
	bool m_running;

	float m_lastRefresh;

	void ClearResources() {
		// Create resources
		DestroyImmediate(m_videoCube);
		DestroyImmediate(m_mappedCube);
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

		m_videoCube = CreateCubeTex(ResX, ResY, ResZ);
		m_mappedCube = CreateCubeTex(ResX, ResY, ResZ);

		m_videoPlayTexture = new RenderTexture(m_videoCube.width, m_videoCube.height, 0);
		m_videoPlayTexture.Create();

		m_indices = new ComputeBuffer(ResX * ResY * ResZ, sizeof(uint) * 3);
		m_count = new ComputeBuffer(1, sizeof(uint));
		m_count.SetData(new uint[] { 0 });

		m_videoDone = false;
		m_running = false;
		m_system = GetComponent<TCParticleSystem>();

		ProcessImageGaussian(Image);
	}

	void SetStep(string step) {
		Debug.Log(step);
	}

	RenderTexture CreateCubeTex(int width, int height, int depth) {
		var tex = new RenderTexture(width, height, 0);
		tex.dimension = TextureDimension.Tex3D;
		tex.volumeDepth = depth;
		tex.enableRandomWrite = true;
		tex.Create();
		return tex;
	}

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

	IEnumerator ProcessVideoAsyncRoutine(VideoClip clip) {
		m_videoDone = false;

		SetStep("Creating resources");
		
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

		for (int i = 0; i <= ResZ; ++i) {
			player.StepForward();
			frameRead = false;
			while (!frameRead) { yield return null; }
		}

		player.sendFrameReadyEvents = false;
		m_videoDone = true;
	}

	IEnumerator GenerateParticles() {
		m_running = true;
		m_lastRefresh = Time.time;

		try {
			int dx = Mathf.CeilToInt(ResX / 8.0f);
			int dy = Mathf.CeilToInt(ResY / 8.0f);
			int dz = Mathf.CeilToInt(ResZ / 8.0f);

			SetStep("Clear map");

			// Clear map texture 
			{
				int kernel = VoxelCompute.FindKernel("ClearMap");
				VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
				VoxelCompute.Dispatch(kernel, dx, dy, dz);
			}

			yield return null;

			SetStep("Apply map function");

			// DoMapping
			{
				int kernel = VoxelCompute.FindKernel(MapFunc);
				VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
				VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
				VoxelCompute.Dispatch(kernel, dx, dy, dz);
			}

			yield return null;

			SetStep("Gather Indices");

			// GatherIndices
			{
				// Count up from 0
				m_count.SetData(new uint[] {0});

				int kernel = VoxelCompute.FindKernel("GatherIndices");

				VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
				VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
				VoxelCompute.SetBuffer(kernel, "_Count", m_count);
				VoxelCompute.SetBuffer(kernel, "_FrameIndices", m_indices);

				VoxelCompute.Dispatch(kernel, dx, dy, dz);
			}

			yield return null;

			SetStep("Emit particles");

			// EmitParticles
			{
				transform.localScale = new Vector3(VideoCubeSize.x / ResX, VideoCubeSize.y / ResY, VideoCubeSize.z / ResZ);
				m_system.Emitter.Size = MinMaxRandom.Constant(1.0f);

				uint[] counterValue = new uint[1];
				m_count.GetData(counterValue);
				yield return null;

				int particleCount = (int) counterValue[0];

				m_system.Clear();

				// Construct the right amount of particles
				m_system.Emit(particleCount);

				SetStep("Emitted " + particleCount + " particles");
			}

			yield return null;

			SetStep("Set positions");

			// SetPositions
			{
				int kernel = VoxelCompute.FindKernel("SetPositions");

				VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
				VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
				VoxelCompute.SetBuffer(kernel, "_FrameIndices", m_indices);

				// Now for each particle fire off a kernel that copies the right position
				m_system.Manager.DispatchExtensionKernel(VoxelCompute, kernel);
			}
		} finally {
			m_running = false;
		}
	}

	void Update() {
		if (m_videoDone && !m_running && Time.time - m_lastRefresh > 2.0f) {
			StartCoroutine(GenerateParticles());
		}
	}

	void ProcessVideo(VideoClip clip) {
		StartCoroutine(ProcessVideoAsyncRoutine(clip));
	}

	IEnumerator ProcessImageGaussianAsyncRoutine(Texture2D image) {
		m_videoDone = false;

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
			VoxelCompute.SetFloat("_GaussianSigma", 4.0f);
			VoxelCompute.Dispatch(kernel, dx, dy, 1);
			Graphics.Blit(pong, ping);


			if (i % 100 == 0) {
				yield return null;
			}
		}

		m_videoDone = true;
		StartCoroutine(GenerateParticles());
	}

	// Experiment
	void ProcessImageGaussian(Texture2D image) {
		StartCoroutine(ProcessImageGaussianAsyncRoutine(image));
	}

	void OnGUI() {
		if (GUILayout.Button("Process video")) {
			ProcessVideo(Clip);
		}

		if (GUILayout.Button("Process Gaussian")) {
			ProcessImageGaussian(Image);
		}

		if (GUILayout.Button("Update mapping")) {
			m_lastRefresh = 0.0f;
		}
	}
}