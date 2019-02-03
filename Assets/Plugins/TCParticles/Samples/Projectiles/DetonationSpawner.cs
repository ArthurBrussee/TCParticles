using UnityEngine;

class DetonationSpawner : MonoBehaviour {
#pragma warning disable CS0649
	public GameObject myFusion;
	public GameObject myFusion2;
#pragma warning restore CS0649

	void Update() {
		if (Input.GetMouseButtonUp(0)) {
			Instantiate(myFusion, Vector3.zero, Random.rotation);
		}

		if (Input.GetMouseButtonUp(1)) {
			Instantiate(myFusion2, Vector3.zero, Random.rotation);
		}
	}
}