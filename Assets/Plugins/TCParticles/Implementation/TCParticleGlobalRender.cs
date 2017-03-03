using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using ParticleRenderer = TC.Internal.ParticleRenderer;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class TCParticleGlobalRender {
	static Dictionary<Camera, CommandBuffer[]> s_camToBuffer = new Dictionary<Camera, CommandBuffer[]>();

	public static float[] StretchFacs = {
		0.0f,
		1.0f,
		0.0f,
		1.0f
	};

	public static float[] TailStretchFacs = {
		0.0f,
		0.0f,
		0.0f,
		0.0f,
		1.0f,
		1.0f
	};

	public static Vector3[] Verts = {
		new Vector3(0.5f, -0.5f, 0.0f),
		new Vector3(-0.5f, -0.5f, 0.0f),
		new Vector3(0.5f, 0.5f, 0.0f),
		new Vector3(-0.5f, 0.5f, 0.0f)
	};


#if UNITY_EDITOR
	[InitializeOnLoadMethod]
#endif
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void Init() {
		Camera.onPreRender += Render;
	}
	
	private static void Render(Camera cam) {
		CommandBuffer[] cmd;
		if (!s_camToBuffer.TryGetValue(cam, out cmd)) {
			cmd = new CommandBuffer[2];
			cmd[0] = new CommandBuffer();
			cmd[1] = new CommandBuffer();

			cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, cmd[0]);
			cam.AddCommandBuffer(CameraEvent.AfterForwardAlpha, cmd[1]);

			s_camToBuffer[cam] = cmd;
		}

		cmd[0].Clear();
		cmd[1].Clear();

		int count = TCParticleSystem.All.Count;

		for (int index = 0; index < count; index++) {
			var syst = TCParticleSystem.All[index];
			var rend = syst.ParticleRenderer as ParticleRenderer;

			if (rend.Material.renderQueue == 2450) {
				//Opaque particles into before forward alpha
				rend.FillCommandBuffer(cmd[0]);
			}
			else {
				//Additive particles after forward alpha
				rend.FillCommandBuffer(cmd[1]);
			}
		}
	}
}
