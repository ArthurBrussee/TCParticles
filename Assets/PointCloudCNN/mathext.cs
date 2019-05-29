using Unity.Collections;
using Unity.Mathematics;

public static class mathext {
	// Eigen decomposition code for symmetric 3x3 matrices, copied from the public
	// domain Java Matrix library JAMA
	static float hypot2(float x, float y) {
		return math.length(math.float2(x, y));
	}

	// Symmetric Householder reduction to tridiagonal form.
	static void tred2(ref float3x3 V, ref float3 d, ref float3 e) {
		//  This is derived from the Algol procedures tred2 by
		//  Bowdler, Martin, Reinsch, and Wilkinson, Handbook for
		//  Auto. Comp., Vol.ii-Linear Algebra, and the corresponding
		//  Fortran subroutine in EISPACK.
		for (int j = 0; j < 3; j++) {
			d[j] = V[3 - 1][j];
		}

		// Householder reduction to tridiagonal form.
		for (int i = 3 - 1; i > 0; i--) {
			// Scale to avoid under/overflow.
			float scale = 0.0f;
			float h = 0.0f;
			
			for (int k = 0; k < i; k++) {
				scale = scale + math.abs(d[k]);
			}

			if (scale == 0.0f) {
				e[i] = d[i - 1];
				for (int j = 0; j < i; j++) {
					d[j] = V[i - 1][j];
					V[i][j] = 0.0f;
					V[j][i] = 0.0f;
				}
			}
			else {
				// Generate Householder vector.

				for (int k = 0; k < i; k++) {
					d[k] /= scale;
					h += d[k] * d[k];
				}

				float f = d[i - 1];
				float g = math.sqrt(h);
				if (f > 0) {
					g = -g;
				}

				e[i] = scale * g;
				h = h - f * g;
				d[i - 1] = f - g;
				for (int j = 0; j < i; j++) {
					e[j] = 0.0f;
				}

				// Apply similarity transformation to remaining columns.
				for (int j = 0; j < i; j++) {
					f = d[j];
					V[j][i] = f;
					g = e[j] + V[j][j] * f;
					for (int k = j + 1; k <= i - 1; k++) {
						g += V[k][j] * d[k];
						e[k] += V[k][j] * f;
					}

					e[j] = g;
				}

				f = 0.0f;
				for (int j = 0; j < i; j++) {
					e[j] /= h;
					f += e[j] * d[j];
				}

				float hh = f / (h + h);
				for (int j = 0; j < i; j++) {
					e[j] -= hh * d[j];
				}

				for (int j = 0; j < i; j++) {
					f = d[j];
					g = e[j];
					for (int k = j; k <= i - 1; k++) {
						V[k][j] -= f * e[k] + g * d[k];
					}

					d[j] = V[i - 1][j];
					V[i][j] = 0.0f;
				}
			}

			d[i] = h;
		}

		// Accumulate transformations.
		for (int i = 0; i < 3 - 1; i++) {
			V[3 - 1][i] = V[i][i];
			V[i][i] = 1.0f;
			float h = d[i + 1];
			
			if (h != 0.0f) {
				for (int k = 0; k <= i; k++) {
					d[k] = V[k][i + 1] / h;
				}

				for (int j = 0; j <= i; j++) {
					float g = 0.0f;
					for (int k = 0; k <= i; k++) {
						g += V[k][i + 1] * V[k][j];
					}

					for (int k = 0; k <= i; k++) {
						V[k][j] -= g * d[k];
					}
				}
			}

			for (int k = 0; k <= i; k++) {
				V[k][i + 1] = 0.0f;
			}
		}

		for (int j = 0; j < 3; j++) {
			d[j] = V[3 - 1][j];
			V[3 - 1][j] = 0.0f;
		}

		V[3 - 1][3 - 1] = 1.0f;
		e[0] = 0.0f;
	}

	// Symmetric tridiagonal QL algorithm.
	static void tql2(ref float3x3 V, ref float3 d, ref float3 e) {
		//  This is derived from the Algol procedures tql2, by
		//  Bowdler, Martin, Reinsch, and Wilkinson, Handbook for
		//  Auto. Comp., Vol.ii-Linear Algebra, and the corresponding
		//  Fortran subroutine in EISPACK.
		for (int i = 1; i < 3; i++) {
			e[i - 1] = e[i];
		}

		e[3 - 1] = 0.0f;

		float f = 0.0f;
		float tst1 = 0.0f;
		float eps = 2e-18f;
		
		for (int l = 0; l < 3; l++) {
			// Find small subdiagonal element
			tst1 = math.max(tst1, math.abs(d[l]) + math.abs(e[l]));
			int m = l;
			
			while (m < 3) {
				if (math.abs(e[m]) <= eps * tst1) {
					break;
				}
				m++;
			}

			// If m == l, d[l] is an eigenvalue,
			// otherwise, iterate.
			if (m > l) {
				int iter = 0;
				do {
					iter = iter + 1; // (Could check iteration count here.)

					// Compute implicit shift
					float g = d[l];
					float p = (d[l + 1] - g) / (2.0f * e[l]);
					float r = hypot2(p, 1.0f);
					
					if (p < 0) {
						r = -r;
					}

					d[l] = e[l] / (p + r);
					d[l + 1] = e[l] * (p + r);
					float dl1 = d[l + 1];
					float h = g - d[l];
					for (int i = l + 2; i < 3; i++) {
						d[i] -= h;
					}

					f = f + h;

					// Implicit QL transformation.
					p = d[m];
					float c = 1.0f;
					float c2 = c;
					float c3 = c;
					float el1 = e[l + 1];
					float s = 0.0f;
					float s2 = 0.0f;
					
					for (int i = m - 1; i >= l; i--) {
						c3 = c2;
						c2 = c;
						s2 = s;
						g = c * e[i];
						h = c * p;
						r = hypot2(p, e[i]);
						e[i + 1] = s * r;
						s = e[i] / r;
						c = p / r;
						p = c * d[i] - s * g;
						d[i + 1] = h + s * (c * g + s * d[i]);

						// Accumulate transformation.
						for (int k = 0; k < 3; k++) {
							h = V[k][i + 1];
							V[k][i + 1] = s * V[k][i] + c * h;
							V[k][i] = c * V[k][i] - s * h;
						}
					}

					p = -s * s2 * c3 * el1 * e[l] / dl1;
					e[l] = s * p;
					d[l] = c * p;

					// Check for convergence.
				} while (math.abs(e[l]) > eps * tst1);
			}

			d[l] = d[l] + f;
			e[l] = 0.0f;
		}

		// Sort eigenvalues and corresponding vectors.
		for (int i = 0; i < 3 - 1; i++) {
			int k = i;
			float p = d[i];
			
			for (int j = i + 1; j < 3; j++) {
				if (d[j] > p) {
					k = j;
					p = d[j];
				}
			}

			if (k != i) {
				d[k] = d[i];
				d[i] = p;
				
				for (int j = 0; j < 3; j++) {
					p = V[j][i];
					V[j][i] = V[j][k];
					V[j][k] = p;
				}
			}
		}
	}

	public static void eigendecompose(float3x3 A, out float3x3 V, out float3 d) {
		V = float3x3.identity;
		d = float3.zero;
		
		float3 e = float3.zero;
		
		for (int i = 0; i < 3; i++) {
			for (int j = 0; j < 3; j++) {
				V[i][j] = A[i][j];
			}
		}
		
		tred2(ref V, ref d, ref e);
		tql2(ref V, ref d, ref e);
	}

	/*
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
			jr[k0] *= -1.0f;                             // since our quat-to-matrix convention was for v*M instead of M*v
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
*/
	
	public static int argmin(float a, float b, float c) {
		float minVal = math.min(a, math.min(b, c));
		int argMin = a == minVal ? 0 : b == minVal ? 1 : 2;
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