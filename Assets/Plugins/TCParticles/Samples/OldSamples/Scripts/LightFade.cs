using UnityEngine;
using System.Collections;

public class LightFade : MonoBehaviour {

	public AnimationCurve animationCurve = new AnimationCurve();
	public float duration = 5.0f;

	float startIntensity;
	float startTime;

	// Use this for initialization
	void Start () {
		startIntensity = GetComponent<Light>().intensity;
		startTime = Time.time;
	}
	
	// Update is called once per frame
	void Update () {
		if (GetComponent<Light>() != null)			
			GetComponent<Light>().intensity = animationCurve.Evaluate(Mathf.Min(1.0f, (Time.time - startTime) / duration)) * startIntensity;
	}
}
