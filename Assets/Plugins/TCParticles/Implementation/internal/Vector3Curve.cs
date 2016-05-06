using System;
using UnityEngine;

namespace TC.Internal
{
	[Serializable]
	public class Vector3Curve
	{
		public float x;
		public float y;
		public float z;

		public bool isConstant = true;

		public AnimationCurve xCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);
		public AnimationCurve yCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);
		public AnimationCurve zCurve = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 0.0f);


		public Vector3 Value(float t)
		{
			return isConstant ? new Vector3(x, y, z) : new Vector3(xCurve.Evaluate(t), yCurve.Evaluate(t), zCurve.Evaluate(t));
		}

		public static Vector3Curve Zero()
		{
			return new Vector3Curve();
		}
	}
}