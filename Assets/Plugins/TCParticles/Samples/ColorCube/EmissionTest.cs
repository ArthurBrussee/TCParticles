using TC;
using UnityEngine;

public class EmissionTest : MonoBehaviour {
	void Start () {
		int count = 0;
		const int num = 80;

		ParticleProto[] p = new ParticleProto[num * num * num];

		for (int i = 0; i < num; ++i) {
			for (int j = 0; j < num; ++j) {
				for (int k = 0; k < num; ++k) {
					p[count].Position = new Vector3((float)i / num, (float)j / num, (float)k / num) * 2.0f - Vector3.one;
					p[count].Color = Color.HSVToRGB((float)i / num, (float)j / num, (float)k / num);
					p[count].Size = 1.0f;
					p[count].Velocity = Vector3.zero;
					++count;
				}
			}
		}

		GetComponent<TCParticleSystem>().Emitter.Emit(p);
	}
}
