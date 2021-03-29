using UnityEngine;

public struct ColorHSV {
	float m_h; // 0..360

	public float H {
		get => m_h;
		set => m_h = value % 360;
	}

	float m_s; // 0..100

	public float S {
		get => m_s;
		set {
			if (value < 0) m_s = 0;
			else if (value > 100) m_s = 100;
			else m_s = value;
		}
	}

	float m_v; // 0..100

	public float V {
		get => m_v;
		set {
			if (value < 0) m_v = 0;
			else if (value > 100) m_v = 100;
			else m_v = value;
		}
	}

	float m_a; // 0..100

	public float A {
		get => m_a;
		set {
			if (value < 0) m_a = 0;
			else if (value > 100) m_a = 100;
			else m_a = value;
		}
	}

	public ColorHSV(float h, float s, float v) : this() {
		H = h;
		S = s;
		V = v;
		A = 100.0f;
	}

	public ColorHSV(float h, float s, float v, float a)
		: this() {
		H = h;
		S = s;
		V = v;
		A = a;
	}

	public ColorHSV(Color color)
		: this() {
		Color32 col32 = color;
		SetColorHSV(col32);
	}

	public ColorHSV(Color32 color)
		: this() {
		SetColorHSV(color);
	}

	private void SetColorHSV(Color32 color) {
		float div = 100.0f / 255.0f;
		m_a = color.a * div;
		m_h = 0.0f;
		float minRGB = Mathf.Min(Mathf.Min(color.r, color.g), color.b);
		float maxRGB = Mathf.Max(Mathf.Max(color.r, color.g), color.b);
		float delta = maxRGB - minRGB;
		m_v = maxRGB;
		if (maxRGB != 0.0f) {
			m_s = 255.0f * delta / maxRGB;
		} else {
			m_s = 0.0f;
		}

		if (m_s != 0.0f) {
			if (color.r == maxRGB) {
				m_h = (color.g - color.b) / delta;
			}
			else if (color.g == maxRGB) {
				m_h = 2.0f + (color.b - color.r) / delta;
			}
			else if (color.b == maxRGB) {
				m_h = 4.0f + (color.r - color.g) / delta;
			}
		} else {
			m_h = -1.0f;
		}

		m_h *= 60.0f;

		if (m_h < 0.0f) {
			m_h += 360.0f;
		}

		m_s *= div;
		m_v *= div;
	}

	public Color ToColor() {
		Color color = ToColor32();
		return color;
	}

	public Color32 ToColor32() {
		float saturation = m_s * .01f;
		float value = m_v * 2.55f;
		int hi = (int)(Mathf.Floor(m_h / 60.0f)) % 6;
		float f = m_h / 60.0f - Mathf.Floor(m_h / 60.0f);
		byte v1 = (byte)Mathf.Round(value);
		byte p = (byte)Mathf.Round(value * (1.0f - saturation));
		byte q = (byte)Mathf.Round(value * (1.0f - f * saturation));
		byte t = (byte)Mathf.Round(value * (1.0f - (1.0f - f) * saturation));
		byte a1 = (byte)Mathf.Round(m_a * 2.55f);

		switch (hi) {
			case 0: return new Color32(v1, t, p, a1);
			case 1: return new Color32(q, v1, p, a1);
			case 2: return new Color32(p, v1, t, a1);
			case 3: return new Color32(p, q, v1, a1);
			case 4: return new Color32(t, p, v1, a1);
			default: return new Color32(v1, p, q, a1);
		}
	}
}
