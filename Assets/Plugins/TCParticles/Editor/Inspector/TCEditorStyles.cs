using System.Collections.Generic;
using UnityEngine;


namespace TC.EditorIntegration {
	public static class TCEditorStyles {
		static Dictionary<Color, GUIStyle> s_colorCache = new Dictionary<Color, GUIStyle>();

		public static GUIStyle GetBackgroundForColor(Color color) {
			if (s_colorCache.ContainsKey(color)) {
				return s_colorCache[color];
			}

			var newStyle = new GUIStyle();
			var bckTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) {hideFlags = HideFlags.HideAndDontSave};

			color.a = 0.03f * (1 / color.grayscale) * color.a;
			bckTex.SetPixel(1, 1, color);
			bckTex.Apply(false);

			newStyle.normal.background = bckTex;
			s_colorCache[color] = newStyle;

			return newStyle;
		}
	}
}