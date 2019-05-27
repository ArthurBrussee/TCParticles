using TC;
using UnityEngine;

public class PointCloudGenerate : MonoBehaviour {
	public GameObject Tester;

	public int PointCount = 10000;

	public string CloudName = "TestCloud";
	public bool WriteData;

	public float NoiseLevel = 0.01f;
	public float SampleRate = 1.0f;

	// Start is called before the first frame update
	void Start() {
		var pointCloudData = PointCloudNormals.GenerateTrainingData(Tester, PointCount, NoiseLevel, SampleRate, CloudName, WriteData);
		var system = GetComponent<TCParticleSystem>();
		system.Emitter.PointCloud = pointCloudData;
		system.Emitter.Emit(pointCloudData.PointCount);

		GetComponent<MeshRenderer>().enabled = false;
	}
}