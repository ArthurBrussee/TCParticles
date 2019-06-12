using UnityEngine;
using TC;

public class PointCloudEnableSim : MonoBehaviour {
	// Update is called once per frame
	void Update () {
		if (Input.GetKeyDown(KeyCode.Space)) {
			var ps = FindObjectsOfType<TCParticleSystem>();

			foreach (var p in ps) {
				p.Manager.NoSimulation = !p.Manager.NoSimulation;
			}
		}
	}
}