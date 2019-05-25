// Based on: https://www.alanzucconi.com/2015/09/16/how-to-sample-from-a-gaussian-distribution/
// Adapted to work with Unity random classes
using Unity.Mathematics;

public static class RandomExt {
	public static float NextGaussian(this ref Random rand) {
		float2 v;
		float s;
		do {
			v = 2.0f * rand.NextFloat2() - 1.0f;
			s = math.lengthsq(v);
		} while (s >= 1.0f || s == 0.0f);

		s = math.sqrt(-2.0f * math.log(s) / s);
		return v.x * s;
	}

	public static float3 NextGaussianSphere(this ref Random rand, float sigma) {
		var ret = new float3(
			rand.NextGaussian() * sigma,
			rand.NextGaussian() * sigma,
			rand.NextGaussian() * sigma
		);

		return ret;
	}
}