using TC;
using UnityEngine;

public class PointCloudNormalsTest : MonoBehaviour {
	public GameObject Tester;

	public int PointCount = 10000;

	// Start is called before the first frame update
    void Start() {
	    var pointCloudData = PointCloudNormals.GenerateTrainingData(Tester, PointCount, 1.0f, "TestCloud");
	    var system = GetComponent<TCParticleSystem>();
	    system.Emitter.PointCloud = pointCloudData;
	    system.Emitter.Emit(pointCloudData.PointCount);
    }
}
