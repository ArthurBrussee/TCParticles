using System;
using UnityEngine;

namespace TC.Internal
{
	[Serializable]
	public class MinMax
	{
		public enum MinMaxMode
		{
			Constant,
			Between
		}

		//Serialize the vars internally, 
		[SerializeField] private float minProp;

		[SerializeField] private float maxProp = 1.0f;

		[SerializeField] private float valueProp;

		[SerializeField] private MinMaxMode modeProp;


		//and some helpfull acces functions for the API
		public MinMaxMode Mode
		{
			get { return modeProp; }

			set
			{
				if (IsConstant && value == MinMaxMode.Between)
				{
					minProp = valueProp;
					maxProp = valueProp;
				}

				modeProp = value;
			}
		}


		public float Min
		{
			get
			{
				switch (Mode)
				{
					case MinMaxMode.Constant:
						return valueProp;
					case MinMaxMode.Between:
						return minProp;
					default:
						return 0.0f;
				}
			}

			set { minProp = value; }
		}

		public float Max
		{
			get
			{
				switch (Mode)
				{
					case MinMaxMode.Constant:
						return valueProp;
					case MinMaxMode.Between:
						return maxProp;
					default:
						return 0.0f;
				}
			}

			set
			{
				maxProp = value;

				if (IsConstant)
					valueProp = value;
			}
		}


		public float Value
		{
			get
			{
				if (IsConstant)
					return valueProp;

				return (Min + Max) / 2.0f;
			}

			set { valueProp = value; }
		}

		public bool IsConstant
		{
			get { return Mode == MinMaxMode.Constant; }
		}

		public void Init()
		{
			if (Mode != MinMaxMode.Constant) return;
			minProp = valueProp;
			maxProp = valueProp;
		}

		public static MinMax Constant(float value)
		{
			var m = new MinMax {Mode = MinMaxMode.Constant, Value = value};
			return m;
		}
	}

	//MinMax vars
	[Serializable]
	public class MinMaxRandom
	{
		public enum MinMaxMode
		{
			Constant,
			Curve,
			RandomBetween,
			RandomBetweenCurves
		}

		//Serialize the vars internally, 
		[SerializeField] private float minProp;

		[SerializeField] private float maxProp = 1.0f;

		[SerializeField] private float valueProp;

		public AnimationCurve minCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);
		public AnimationCurve maxCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f);
		public AnimationCurve valueCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);


		[SerializeField] private MinMaxMode modeProp;


		private bool IsCurve
		{
			get { return Mode == MinMaxMode.Curve || Mode == MinMaxMode.RandomBetweenCurves; }
		}

		public float t;


		//and some helpfull acces functions for the API
		public MinMaxMode Mode
		{
			get { return modeProp; }

			set
			{
				if (IsNotRandom && (value == MinMaxMode.RandomBetween || value == MinMaxMode.RandomBetweenCurves))
				{
					minProp = valueProp;
					maxProp = valueProp;
					minCurve = valueCurve;
					maxCurve = valueCurve;
				}

				modeProp = value;
			}
		}


		public float Min
		{
			get
			{
				switch (Mode)
				{
					case MinMaxMode.Constant:
						return valueProp;
					case MinMaxMode.Curve:
						return valueProp;
					case MinMaxMode.RandomBetween:
						return minProp;
					case MinMaxMode.RandomBetweenCurves:
						return minCurve.Evaluate(t);
					default:
						return 0.0f;
				}
			}

			set
			{
				if (IsCurve)
					minCurve.MoveKey(0, new Keyframe(0.0f, value));

				minProp = value;
			}
		}

		public float Max
		{
			get
			{
				switch (Mode)
				{
					case MinMaxMode.Constant:
						return valueProp;
					case MinMaxMode.Curve:
						return valueCurve.Evaluate(t);
					case MinMaxMode.RandomBetween:
						return maxProp;
					case MinMaxMode.RandomBetweenCurves:
						return maxCurve.Evaluate(t);
					default:
						return 0.0f;
				}
			}

			set
			{
				if (IsCurve)
					maxCurve.MoveKey(0, new Keyframe(0.0f, value));

				maxProp = value;

				if (IsNotRandom)
					valueProp = value;
			}
		}


		public float Value
		{
			get
			{
				if (IsNotRandom)
				{
					if (IsCurve)
						return valueCurve.Evaluate(t);

					return valueProp;
				}

				return (Min + Max) / 2.0f;
			}

			set
			{
				if (!IsCurve)
					valueProp = value;

				valueCurve.MoveKey(0, new Keyframe(0.0f, value));
			}
		}

		public bool IsNotRandom
		{
			get { return Mode == MinMaxMode.Constant || Mode == MinMaxMode.Curve; }
		}

		public void Init()
		{
			if (!IsNotRandom) return;
			minProp = valueProp;
			maxProp = valueProp;
			minCurve = valueCurve;
			maxCurve = valueCurve;
		}

		public static MinMaxRandom Constant(float value)
		{
			var m = new MinMaxRandom {Mode = MinMaxMode.Constant, Value = value};
			return m;
		}
	}
}