using System;
using UnityEngine;

namespace TC {
	/// <summary>
	/// Holds values that can be set to a constant or a range
	/// </summary>
	[Serializable]
	public class MinMax {
		public enum MinMaxMode {
			/// <summary>
			/// Constant value
			/// </summary>
			Constant,

			/// <summary>
			/// Random value between <see cref="Min"/> and <see cref="Max"/>
			/// </summary>
			Between
		}

		[SerializeField] float minProp;
		[SerializeField] float maxProp = 1.0f;
		[SerializeField] float valueProp;
		[SerializeField] MinMaxMode modeProp;

		/// <summary>
		/// Current mode this MinMax property is in
		/// </summary>
		public MinMaxMode Mode {
			get => modeProp;

			set {
				if (IsConstant && value == MinMaxMode.Between) {
					minProp = valueProp;
					maxProp = valueProp;
				}

				modeProp = value;
			}
		}

		/// <summary>
		/// Minimum value of this property
		/// </summary>
		/// <remarks>
		/// In constant mode this returns the constant value
		/// </remarks>
		public float Min {
			get => IsConstant ? valueProp : minProp;
			set {
				if (IsConstant) {
					valueProp = value;
				}

				minProp = value;
			}
		}

		/// <summary>
		/// Maximum value of this property
		/// </summary>
		/// <remarks>
		/// In constant mode this returns the constant value
		/// </remarks>
		public float Max {
			get => IsConstant ? valueProp : maxProp;

			set {
				if (IsConstant) {
					valueProp = value;
				}

				maxProp = value;
			}
		}

		/// <summary>
		/// Constant value if IsConstant is true or overage of Min and Max otherwise.
		/// </summary>
		/// <remarks>
		/// When setting this value in Non Constant mode this sets both the Min and Max
		/// </remarks>
		public float Value {
			get {
				if (IsConstant) {
					return valueProp;
				}

				return (Min + Max) / 2.0f;
			}

			set {
				if (IsConstant) {
					valueProp = value;
				} else {
					Min = value;
					Max = value;
				}
			}
		}

		/// <summary>
		/// Is this MinMax just a constant value
		/// </summary>
		public bool IsConstant => Mode == MinMaxMode.Constant;

		/// <summary>
		/// Creates a new MinMax instance holding a constant value
		/// </summary>
		/// <param name="value">Constant value</param>
		/// <returns>MinMax instance</returns>
		public static MinMax Constant(float value) {
			var m = new MinMax {Mode = MinMaxMode.Constant, Value = value};
			return m;
		}
	}

	/// <summary>
	/// Holds values that can be a constant, a curve, or random between constants or curves
	/// </summary>
	[Serializable]
	public class MinMaxRandom {
		public enum MinMaxMode {
			Constant,
			Curve,
			RandomBetween,
			RandomBetweenCurves
		}

		//Serialize the vars internally, 
		[SerializeField] float minProp;
		[SerializeField] float maxProp = 1.0f;
		[SerializeField] float valueProp;

		/// <summary>
		/// Minimum value curve over the system time
		/// </summary>
		public AnimationCurve minCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);

		/// <summary>
		/// Maximum value curve over the system time
		/// </summary>
		public AnimationCurve maxCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f);

		/// <summary>
		/// Value curve over the system time
		/// </summary>
		public AnimationCurve valueCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);

		/// <summary>
		/// Current time used for sampling
		/// </summary>
		public float t;

		[SerializeField] MinMaxMode modeProp;

		bool IsCurve => Mode == MinMaxMode.Curve || Mode == MinMaxMode.RandomBetweenCurves;

		public MinMaxMode Mode {
			get => modeProp;

			set {
				if (IsConstant && (value == MinMaxMode.RandomBetween || value == MinMaxMode.RandomBetweenCurves)) {
					minProp = valueProp;
					maxProp = valueProp;
					minCurve = valueCurve;
					maxCurve = valueCurve;
				}

				modeProp = value;
			}
		}

		/// <summary>
		/// Minium value at this time
		/// </summary>
		public float Min {
			get {
				switch (Mode) {
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

			set {
				if (IsCurve) {
					minCurve.MoveKey(0, new Keyframe(0.0f, value));
				}

				minProp = value;

				if (IsConstant) {
					valueProp = value;
				}
			}
		}

		/// <summary>
		/// Maximum value at this time
		/// </summary>
		public float Max {
			get {
				switch (Mode) {
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

			set {
				if (IsCurve) {
					maxCurve.MoveKey(0, new Keyframe(0.0f, value));
				}

				maxProp = value;

				if (IsConstant) {
					valueProp = value;
				}
			}
		}

		/// <summary>
		/// Current value. Returns constant if <see cref="IsConstant"/>, otherwise returns average of min and max
		/// </summary>
		public float Value {
			get {
				if (IsConstant) {
					if (IsCurve) {
						return valueCurve.Evaluate(t);
					}

					return valueProp;
				}

				if (IsCurve) {
					return (minCurve.Evaluate(t) + maxCurve.Evaluate(t)) / 2.0f;
				}

				return (Min + Max) / 2.0f;
			}

			set {
				valueProp = value;
				minProp = value;
				maxProp = value;
				valueCurve.MoveKey(0, new Keyframe(0.0f, value));
			}
		}

		/// <summary>
		/// Is the value a constant or a single curve
		/// </summary>
		public bool IsConstant => Mode == MinMaxMode.Constant || Mode == MinMaxMode.Curve;

		/// <summary>
		/// Returns a new MinMaxRandom instance holding a constant value
		/// </summary>
		/// <param name="value">constant value</param>
		/// <returns>MinMaxRandom instance</returns>
		public static MinMaxRandom Constant(float value) {
			var m = new MinMaxRandom {Mode = MinMaxMode.Constant, Value = value};
			return m;
		}
	}
}