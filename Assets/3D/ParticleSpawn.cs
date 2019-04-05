using System.Collections;
using TC;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

public class ParticleSpawn : MonoBehaviour {
	public ComputeShader VoxelCompute;
	public VideoClip Clip;
    public Vector3 VideoCubeSize;

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

	void Awake() {
		ProcessVideoAsync(Clip, ResX, ResY, ResZ, MapFunc);
	}

	uint GetCounterValue(ComputeBuffer buffer) {
		var cmd = new CommandBuffer();
		var tmpCount = new ComputeBuffer(1, sizeof(uint));
		cmd.CopyCounterValue(buffer, tmpCount, 0);
		Graphics.ExecuteCommandBuffer(cmd);
		uint[] counterValue = new uint[1];
		tmpCount.GetData(counterValue);
		tmpCount.Dispose();
		return counterValue[0];
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

	IEnumerator ProcessVideoAsyncRoutine(VideoClip clip, int width, int height, int depth, string mapKernel) {
		SetStep("Creating resources");

		m_system = GetComponent<TCParticleSystem>();

		DestroyImmediate(m_videoCube);
		DestroyImmediate(m_mappedCube);
		DestroyImmediate(m_videoPlayTexture);

		if (m_indices != null) {
			m_indices.Dispose();
		}
		
		m_videoCube = CreateCubeTex(width, height, depth);
		m_mappedCube = CreateCubeTex(width, height, depth);
		
		m_videoPlayTexture = new RenderTexture(m_videoCube.width, m_videoCube.height, 0);
		m_videoPlayTexture.Create();
		
		m_indices = new ComputeBuffer(width * height * depth, sizeof(uint) * 3);
		m_count = new ComputeBuffer(1, sizeof(uint));
		m_count.SetData(new uint[] {1});
		
		int dx = Mathf.CeilToInt(width / 8.0f);
		int dy = Mathf.CeilToInt(height / 8.0f);
		int dz = Mathf.CeilToInt(depth / 8.0f);
		
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
			int kernel = VoxelCompute.FindKernel("CopyVideoFrame");
			VoxelCompute.SetTexture(kernel, "_VideoFrame", m_videoPlayTexture);
			VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
			VoxelCompute.SetInt("_Slice", (int)idx - 1);
			VoxelCompute.Dispatch(kernel, dx, dy, 1);
			frameRead = true;
		};

		for (int i = 0; i < depth; ++i) {
			player.StepForward();
			frameRead = false;
			while (!frameRead) { yield return null; }
		}

		player.sendFrameReadyEvents = false;

		yield return null;

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
			int kernel = VoxelCompute.FindKernel(mapKernel);

			VoxelCompute.SetTexture(kernel, "_VideoTexture", m_videoCube);
			VoxelCompute.SetTexture(kernel, "_MappedTexture", m_mappedCube);
			VoxelCompute.Dispatch(kernel, dx, dy, dz);
		}

		yield return null;
		
		SetStep("Gather Indices");

		// GatherIndices
		{
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
			transform.localScale = new Vector3(VideoCubeSize.x / width, VideoCubeSize.y / height, VideoCubeSize.z / depth);
			m_system.Emitter.Size = MinMaxRandom.Constant(1.0f);
			
			uint[] counterValue = new uint[1];
			m_count.GetData(counterValue);
			yield return null;

			int particleCount = (int) counterValue[0];

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
	}

	void ProcessVideoAsync(VideoClip clip, int width, int height, int depth, string mapKernel) {
		StartCoroutine(ProcessVideoAsyncRoutine(clip, width, height, depth, mapKernel));
	}
}