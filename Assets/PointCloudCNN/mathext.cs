
// Extensions for unity math class

using Unity.Collections;
using Unity.Mathematics;

public static class mathext {
	// source: http://www.melax.com/diag.html?attredirects=0
	// Adapted by Arthur Brussee to Unity Mathematics code
	// A must be a symmetric matrix.
	// returns Q and D such that 
	// Diagonal matrix D = QT * A * Q;  and  A = Q*D*QT
	public static void diagonalize(float3x3 A, out float3x3 Q, out float3x3 D) {
		float3 o = float3.zero;
		var q = new float4(0.0f, 0.0f, 0.0f, 1.0f);

		Q = float3x3.identity;
		D = float3x3.identity;

		const int maxsteps = 24; // certainly wont need that many.
		for (int i = 0; i < maxsteps; ++i) {
			// quaternion to matrix
			float sqx = q[0] * q[0];
			float sqy = q[1] * q[1];
			float sqz = q[2] * q[2];
			float sqw = q[3] * q[3];

			Q[0][0] = sqx - sqy - sqz + sqw;
			Q[1][1] = -sqx + sqy - sqz + sqw;
			Q[2][2] = -sqx - sqy + sqz + sqw;

			float tmp1 = q[0] * q[1];
			float tmp2 = q[2] * q[3];

			Q[1][0] = 2.0f * (tmp1 + tmp2);
			Q[0][1] = 2.0f * (tmp1 - tmp2);

			tmp1 = q[0] * q[2];
			tmp2 = q[1] * q[3];

			Q[2][0] = 2.0f * (tmp1 - tmp2);
			Q[0][2] = 2.0f * (tmp1 + tmp2);

			tmp1 = q[1] * q[2];
			tmp2 = q[0] * q[3];

			Q[2][1] = 2.0f * (tmp1 + tmp2);
			Q[1][2] = 2.0f * (tmp1 - tmp2);

			// AQ = A * Q
			float3x3 AQ = A * Q;

			// D = Qt * AQ
			D = math.transpose(Q) * AQ;

			o.x = D[1][2];
			o.y = D[0][2];
			o.z = D[0][1];

			float3 m = math.abs(o);

			int k0 = m[0] > m[1] && m[0] > m[2] ? 0 : m[1] > m[2] ? 1 : 2;
			int k1 = (k0 + 1) % 3;
			int k2 = (k0 + 2) % 3;

			if (o[k0] == 0.0f) {
				break; // diagonal already
			}

			float theta = (D[k2][k2] - D[k1][k1]) / (2.0f * o[k0]);
			float sgn = theta > 0.0f ? 1.0f : -1.0f;
			theta *= math.sign(theta); // make it positive
			float t = sgn / (theta + (theta < 1e6 ? math.sqrt(theta * theta + 1.0f) : theta));
			float c = 1.0f / math.sqrt(t * t + 1.0f);

			if (c == 1.0f) {
				break; // no room for improvement - reached machine precision.
			}

			float4 jr = float4.zero;

			jr[k0] = sgn * math.sqrt((1.0f - c) / 2.0f); // using 1/2 angle identity sin(a/2) = sqrt((1-cos(a))/2)  
			jr[k0] *= -1.0f; // since our quat-to-matrix convention was for v*M instead of M*v
			jr[3] = math.sqrt(1.0f - jr[k0] * jr[k0]);

			if (jr[3] == 1.0f) {
				break; // reached limits of floating point precision
			}

			q[0] = q[3] * jr[0] + q[0] * jr[3] + q[1] * jr[2] - q[2] * jr[1];
			q[1] = q[3] * jr[1] - q[0] * jr[2] + q[1] * jr[3] + q[2] * jr[0];
			q[2] = q[3] * jr[2] + q[0] * jr[1] - q[1] * jr[0] + q[2] * jr[3];
			q[3] = q[3] * jr[3] - q[0] * jr[0] - q[1] * jr[1] - q[2] * jr[2];
			q = math.normalize(q);
		}
	}
	
	public static int argmin(float a, float b, float c) {
		float minVal = math.min(a, math.min(b, c));
		int argMin = a == minVal ? 0 : (b == minVal ? 1 : 2);
		return argMin;
	}

	public static int argmax(float a, float b) {
		return a >= b ? 0 : 1;
	}
	
	public static float min(NativeArray<float> values) {
		float min = float.MaxValue;
		for (int i = 0; i < values.Length; ++i) {
			min = math.min(min, values[i]);
		}

		return min;
	}
	
	public static float max(NativeArray<float> values) {
		float max = float.MinValue;
		for (int i = 0; i < values.Length; ++i) {
			max = math.max(max, values[i]);
		}
		
		return max;
	}
}