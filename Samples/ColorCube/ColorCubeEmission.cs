using TC;
using UnityEngine;

//Create a cube showing full HSV range.
//Assign a HSV color based on position to every particle
public class ColorCubeEmission : MonoBehaviour {
	void Awake () {
		//This uses the 'ParticleProto' API. You create some prototype particles and emit these.
		int count = 0;
		const int num = 80;

		ParticleProto[] p = new ParticleProto[num * num * num];

		for (int i = 0; i < num; ++i) {
			for (int j = 0; j < num; ++j) {
				for (int k = 0; k < num; ++k) {
					p[count].Position = new Vector3((float)i / num, (float)j / num, (float)k / num) * 2.0f - Vector3.one;
					p[count].Color = Color.HSVToRGB((float)i / num, (float)j / num, (float)k / num);
					p[count].Size = 1.0f; //Note: Multiplicative with size set in TC Particles, so particles aren't 1 unit large
					p[count].Velocity = Vector3.zero;
					++count;
				}
			}
		}

		//Submit the buffer to emit
		GetComponent<TCParticleSystem>().Emit(p);
	}
}
