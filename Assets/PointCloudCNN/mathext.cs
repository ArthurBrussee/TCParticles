using Unity.Collections;
using Unity.Mathematics;

/// <summary>
/// Some extensions to Unity's math library
/// </summary>
public static class mathext {
	// Below code for eigen decomposition is taken from Conelly Barnes
	// http://barnesc.blogspot.com/2007/02/eigenvectors-of-3x3-symmetric-matrix.html
	// And ported to Unity's math library by Arthur Brussee
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
					float r = math.length(math.float2(p, 1.0f));
					
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
						r = math.length(math.float2(p, e[i]));
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