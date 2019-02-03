using UnityEngine;
using TC;

public class EmissionFade : MonoBehaviour {

	TCParticleSystem system;
	float startEmission;
	float startTime;

	public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0.0f, 1.0f, 1.0f, 0.0f);


	// Use this for initialization
	void Start () {
		system = GetComponent<TCParticleSystem>();
		startEmission = system.EmissionRate;
	}
	
	// Update is called once per frame
	void Update () {
		system.EmissionRate = startEmission * fadeCurve.Evaluate(system.Lifespan);
		if (system.Lifespan > fadeCurve[fadeCurve.length - 1].time) {
			Destroy(gameObject, 0.5f);
		}
	}
}
