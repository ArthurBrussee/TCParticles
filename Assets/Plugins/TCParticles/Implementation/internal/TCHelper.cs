using UnityEngine;

//Utilities class

namespace TC.Internal {
	public class TCHelper {
		static Color[] s_colours = new Color[c_dimSize];
		const int c_dimSize = 128;

		//Prodcues a texture of resolution of dimx1 encoding a gradient
		public static void TextureFromGradient(Gradient gradient, Texture2D toSet, Color tint, float firstA) {

			for (int i = 0; i < c_dimSize; ++i) {
				s_colours[i] = gradient.Evaluate(i / (c_dimSize - 1.0f)) * tint;
			}

			if (firstA > 0) {
				s_colours[0].a = firstA;
			}

			toSet.SetPixels(s_colours);
			toSet.Apply();
		}

		public static float TransScale(Transform trans) {
			return Mathf.Max(trans.localScale.x, trans.localScale.y, trans.localScale.z);
		}

		public static float TransXzScale(Transform trans) {
			return Mathf.Max(trans.localScale.x, trans.localScale.z);
		}

		static float[] s_matrixArr4 = new float[16];
		static float[] s_matrixArr = new float[12];

		public static void SetMatrix4(ComputeShader shader, string name, Matrix4x4 matrix) {
			for (int i = 0; i < 16; ++i) {
				s_matrixArr4[i] = matrix[i];
			}

			shader.SetFloats(name, s_matrixArr4);
		}

		public static void SetMatrix3(ComputeShader shader, string name, Matrix4x4 matrix) {
			for (int i = 0; i < 12; ++i) {
				s_matrixArr[i] = matrix[i];
			}

			shader.SetFloats(name, s_matrixArr);
		}
	}
}