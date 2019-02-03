using UnityEngine;

//Utilities class

namespace TC.Internal {
	public class TCHelper {
		static Color[] s_colours = new Color[c_dimSize];
		const int c_dimSize = 128;

		//Prodcues a texture of resolution of dimx1 encoding a gradient
		public static void TextureFromGradient(Gradient gradient, Texture2D toSet, Color tint) {
			if (toSet == null) {
				return;
			}

			for (int i = 0; i < c_dimSize; ++i) {
				s_colours[i] = gradient.Evaluate(i / (c_dimSize - 1.0f)) * tint;
			}

			toSet.SetPixels(s_colours);
			toSet.Apply();
		}
	}
}