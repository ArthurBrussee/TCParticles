using UnityEngine;

public class SpringParticlesDemo : MonoBehaviour {
	// Update is called once per frame
	void Update () {
		GetComponent<SpringParticles>().DoSpring = Input.GetKey(KeyCode.Space);
	}
}
