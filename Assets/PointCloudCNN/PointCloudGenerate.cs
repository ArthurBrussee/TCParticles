using TC;
using UnityEngine;

/// <summary>
/// Interface for point cloud mesh
/// </summary>
public class PointCloudGenerate : MonoBehaviour {
	public GameObject Tester;

	public int PointCount = 10000;

	public string CloudName = "TestCloud";
	public string Folder = "TrainingData";
	public bool WriteData;

	public bool ShowErrors;
	
	public bool UseCNN;
	
	public float NoiseLevel = 0.01f;
	public float SampleRate = 1.0f;

	// Start is called before the first frame update
	void Start() {
		Mesh mesh = Tester.GetComponent<MeshFilter>().sharedMesh;
		Material mat = Tester.GetComponent<MeshRenderer>().sharedMaterial;

		var tex = mat.GetTexture("_MainTex") as Texture2D;
		var smoothnessTex = mat.GetTexture("_MetallicGlossMap") as Texture2D;
		var normalTex = mat.GetTexture("_BumpMap") as Texture2D;

		if (tex == null) {
			tex = Texture2D.whiteTexture;
		}

		if (smoothnessTex == null) {
			smoothnessTex = Texture2D.whiteTexture;
		}

		// Get points on the mesh
		var meshPoints = MeshSampler.SampleRandomPointsOnMesh(mesh, tex, smoothnessTex, normalTex, PointCount, NoiseLevel);
		var pointCloudData = PointCloudNormals.GenerateTrainingData(meshPoints, SampleRate, Folder, CloudName, WriteData, UseCNN, ShowErrors);
		
		var system = GetComponent<TCParticleSystem>();
		system.Emitter.PointCloud = pointCloudData;
		system.Emitter.Emit(pointCloudData.PointCount);
		GetComponent<MeshRenderer>().enabled = false;
	}
}