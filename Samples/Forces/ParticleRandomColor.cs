using UnityEngine;
using TC;


public class ParticleRandomColor : MonoBehaviour  {
	ColorHSV col;
	TCParticleSystem syst;
	
	void Start ()
	{
		col = new ColorHSV {A = 100.0f, H = Random.value * 360.0f, S = Random.value * 100.0f, V = Random.value * 100.0f};
		syst = GetComponent<TCParticleSystem>();

		syst.ParticleRenderer.Material = Instantiate(syst.ParticleRenderer.Material);
		syst.ParticleRenderer.Material.SetColor("_Color", col.ToColor());
	}
	
	void Update ()
	{

		col.H += Mathf.PerlinNoise(Time.time, 0.0f) * 0.25f;
		col.S += Mathf.PerlinNoise(Time.time, 1.0f) * 0.1f;
		col.V += Mathf.PerlinNoise(Time.time, 2.0f) * 0.1f;

		syst.ParticleRenderer.Material.SetColor("_Color", col.ToColor());
	}
}
