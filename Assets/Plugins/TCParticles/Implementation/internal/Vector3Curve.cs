using System;
using UnityEngine;

namespace TC {
	[Serializable]
	public class Vector3Curve
	{
		/// <summary>
		/// Constant x value
		/// </summary>
		public float x;
		/// <summary>
		/// Constant y value
		/// </summary>
		public float y;
		/// <summary>
		/// Constant z value
		/// </summary>
		public float z;

		/// <summary>
		/// Is the vector a curve or constant 
		/// </summary>
		public bool isConstant = true;


		/// <summary>
		/// x value over time
		/// </summary>
		public AnimationCurve xCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);
		/// <summary>
		/// y value over time
		/// </summary>
		public AnimationCurve yCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);
		/// <summary>
		/// z value over time
		/// </summary>
		public AnimationCurve zCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);


		/// <summary>
		/// Value at current time
		/// </summary>
		/// <param name="t">Current time</param>
		/// <returns>Time in curve or constant value</returns>
		public Vector3 Value(float t) {
			return isConstant ? new Vector3(x, y, z) : new Vector3(xCurve.Evaluate(t), yCurve.Evaluate(t), zCurve.Evaluate(t));
		}

		/// <summary>
		/// Creates a new curve that is constant 0
		/// </summary>
		/// <returns>Instance of curve constant 0</returns>
		public static Vector3Curve Zero() {
			return new Vector3Curve();
		}
	}
}