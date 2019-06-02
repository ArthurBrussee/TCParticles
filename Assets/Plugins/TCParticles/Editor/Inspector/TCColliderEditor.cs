using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	[CustomEditor(typeof(TCCollider)), CanEditMultipleObjects]
	public class TCColliderEditor : TCEdtiorBase<TCCollider> {
		// Update is called once per frame
		protected override void OnTCInspectorGUI() {
			var shape = (ColliderShape) GetProperty("shape").enumValueIndex;

			PropField("shape",
				new GUIContent("Shape",
					"Determines the shape of the collider, PhysX or custom (PhysX does not support Mesh Colliders)"));

			switch (shape) {
				case ColliderShape.Disc:
					PropField("radius", new GUIContent("Radius", "Radius of the disc"));
					PropField("rounding", new GUIContent("Rounding", "Amount to round the edges of the disc"));
					PropField("discHeight", new GUIContent("Disc Height", "Vertical height of the disc"));
					PropField("discType", new GUIContent("Disc Type"));
					break;

				case ColliderShape.Hemisphere:
					PropField("radius", new GUIContent("Radius", "Radius of the hemi sphere"));
					break;

				case ColliderShape.RoundedBox:
					PropField("boxSize", new GUIContent("Box Size", "Size of the collision box"));
					PropField("rounding", new GUIContent("Rounding", "Amount to round the edges of the box"));
					break;
			}

			GUILayout.Space(10.0f);

			if (shape != ColliderShape.Terrain) {
				PropField("inverse", new GUIContent("Inverse", "Determines whether the collider is solid or hollow"));
				PropField("_inheritVelocity",
					new GUIContent("Inherit Velocity", "Factor of velocity to carry over to the particle on collision"));
				PropField("_particleLifeLoss", new GUIContent("Amount of life particle loses per second when it's colliding"));
			}

			PropField("_bounciness", new GUIContent("Bounciness"));
			PropField("_stickiness", new GUIContent("Stickiness"));
		}

		public void OnSceneGUI() {
			var c = target as TCCollider;

			if (c == null) {
				return;
			}

			switch (c.shape) {
				case ColliderShape.Disc:
					if (c.radius == null) {
						return;
					}

					float rmin = c.radius.IsConstant ? 0.0f : c.radius.Min;
					float rmax = c.radius.Max;

					TCDrawFunctions.DiscHandle(c.transform, rmin, rmax, c.discHeight, c.rounding, (int) c.discType, out rmin, out rmax, out float round);

					c.rounding = round;

					c.radius.Min = rmin;
					c.radius.Max = rmax;

					break;

				case ColliderShape.Hemisphere:
					c.radius.Max = TCDrawFunctions.HemisphereHandle(c.transform, c.radius.Value);
					break;

				case ColliderShape.RoundedBox:
					Vector3 sz = c.boxSize;

					float r = c.rounding;
					c.rounding = Mathf.Clamp(c.rounding, 0.0f, Mathf.Min(c.boxSize.x, c.boxSize.y, c.boxSize.z) * 0.5f);
					c.boxSize = TCDrawFunctions.RoundedCubeHandle(sz, r, c.transform);

					break;
			}
		}
	}
}