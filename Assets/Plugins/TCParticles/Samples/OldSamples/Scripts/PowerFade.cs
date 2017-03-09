using UnityEngine;
using System.Collections;

public class PowerFade : MonoBehaviour
{

	public AnimationCurve animationCurve = new AnimationCurve();
	public float duration = 5.0f;

	float startPower;
	float startTime;

	TCForce force;

	// Use this for initialization
	void Start()
	{
		force = GetComponent<TCForce>();
		startPower = force.power;
		startTime = Time.time;
	}

	// Update is called once per frame
	void Update()
	{
		if (force != null)
			force.power = animationCurve.Evaluate(Mathf.Min(1.0f, (Time.time - startTime) / duration)) * startPower;
	}
}
