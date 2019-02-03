using UnityEngine;
using TC;

public struct ColorHSV
{
	float _h;	// 0..360
	public float H
	{
		get { return _h; }
		set { _h = value % 360; }
	}

	float _s;	// 0..100
	public float S
	{
		get { return _s; }
		set
		{
			if (value < 0) _s = 0;
			else if (value > 100) _s = 100;
			else _s = value;
		}
	}
	float _v;	// 0..100
	public float V
	{
		get { return _v; }
		set
		{
			if (value < 0) _v = 0;
			else if (value > 100) _v = 100;
			else _v = value;
		}
	}

	float _a;	// 0..100
	public float A
	{
		get { return _a; }
		set
		{
			if (value < 0) _a = 0;
			else if (value > 100) _a = 100;
			else _a = value;
		}
	}

	public ColorHSV(float h, float s, float v) : this()
	{
		H = h;
		S = s;
		V = v;
		A = 100.0f;
	}

	public ColorHSV(float h, float s, float v, float a)
		: this()
	{
		H = h;
		S = s;
		V = v;
		A = a;
	}

	public ColorHSV(Color color)
		: this()
	{
		Color32 col32 = color;
		SetColorHSV(col32);
	}

	public ColorHSV(Color32 color)
		: this()
	{
		SetColorHSV(color);
	}

	private void SetColorHSV(Color32 color)
	{
		float div = 100.0f / 255.0f;
		_a = color.a * div;
		_h = 0.0f;
		float minRGB = Mathf.Min(Mathf.Min(color.r, color.g), color.b);
		float maxRGB = Mathf.Max(Mathf.Max(color.r, color.g), color.b);
		float delta = maxRGB - minRGB;
		_v = maxRGB;
		if (maxRGB != 0.0f)
		{
			_s = 255.0f * delta / maxRGB;
		}
		else
		{
			_s = 0.0f;
		}
		if (_s != 0.0f)
		{
			if (color.r == maxRGB)
			{
				_h = (color.g - color.b) / delta;
			}
			else if (color.g == maxRGB)
			{
				_h = 2.0f + (color.b - color.r) / delta;
			}
			else if (color.b == maxRGB)
			{
				_h = 4.0f + (color.r - color.g) / delta;
			}
		}
		else
		{
			_h = -1.0f;
		}
		_h *= 60.0f;
		if (_h < 0.0f)
		{
			_h += 360.0f;
		}
		_s *= div;
		_v *= div;
	}

	public Color ToColor()
	{
		Color color = ToColor32();
		return color;
	}

	public Color32 ToColor32()
	{
		float saturation = _s * .01f;
		float value = _v * 2.55f;
		int hi = (int)(Mathf.Floor(_h / 60.0f)) % 6;
		float f = _h / 60.0f - Mathf.Floor(_h / 60.0f);
		var v1 = (byte)Mathf.Round(value);
		var p = (byte)Mathf.Round(value * (1.0f - saturation));
		var q = (byte)Mathf.Round(value * (1.0f - f * saturation));
		var t = (byte)Mathf.Round(value * (1.0f - (1.0f - f) * saturation));
		var a1 = (byte)Mathf.Round(_a * 2.55f);

		switch (hi)
		{
			case 0:
				return new Color32(v1, t, p, a1);
			case 1:
				return new Color32(q, v1, p, a1);
			case 2:
				return new Color32(p, v1, t, a1);
			case 3:
				return new Color32(p, q, v1, a1);
			case 4:
				return new Color32(t, p, v1, a1);
			default:
				return new Color32(v1, p, q, a1);
		}
	}
}

public class ParticleRandomColor : MonoBehaviour
{
	ColorHSV col;
	TCParticleSystem syst;
	// Use this for initialization
	void Start ()
	{
		col = new ColorHSV {A = 100.0f, H = Random.value * 360.0f, S = Random.value * 100.0f, V = Random.value * 100.0f};
		syst = GetComponent<TCParticleSystem>();

		syst.ParticleRenderer.Material = Instantiate(syst.ParticleRenderer.Material);
		syst.ParticleRenderer.Material.SetColor("_Color", col.ToColor());
	}
	
	// Update is called once per frame
	void Update ()
	{

		col.H += Mathf.PerlinNoise(Time.time, 0.0f) * 0.25f;
		col.S += Mathf.PerlinNoise(Time.time, 1.0f) * 0.1f;
		col.V += Mathf.PerlinNoise(Time.time, 2.0f) * 0.1f;

		syst.ParticleRenderer.Material.SetColor("_Color", col.ToColor());
	}
}
