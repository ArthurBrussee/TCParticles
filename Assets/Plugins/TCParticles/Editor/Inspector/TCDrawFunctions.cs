using System;
using TC.Internal;
using UnityEditor;
using UnityEngine;

namespace TC.EditorIntegration {
	public static class TCDrawFunctions {
		static Material s_lineMat;

		public static void DrawEmitterShape(ParticleEmitterShape pes, Transform transform) {
			var col = new Color(0.6f, 0.9f, 1.0f);

			Handles.color = col;

			switch (pes.shape) {
				case EmitShapes.Sphere:
					pes.radius.Max = RadiusHandle(transform, pes.radius.Max, true);

					if (!pes.radius.IsConstant) {
						Handles.color = new Color(0.6f, 0.9f, 1.0f, 0.4f);

						if (pes.radius.Min > 0.0f) {
							pes.radius.Min = RadiusHandle(transform, pes.radius.Min, true);
						}

						Handles.color = col;
					}

					break;

				case EmitShapes.Box:
					pes.cubeSize = CubeHandle(transform, pes.cubeSize);
					break;

				case EmitShapes.HemiSphere:
					pes.radius.Value = HemisphereHandle(transform, pes.radius.Max, true);

					if (!pes.radius.IsConstant) {
						if (pes.radius.Min > 0.0f) {
							pes.radius.Min = HemisphereHandle(transform, pes.radius.Min, true);
						}
					}

					break;

				case EmitShapes.Cone:
					float coneAngle = pes.coneAngle;
					float coneHeight = pes.coneHeight;
					float coneRadius = pes.coneRadius;

					ConeHandle(ref coneAngle, ref coneHeight, ref coneRadius, transform);

					pes.coneAngle = coneAngle;
					pes.coneHeight = coneHeight;
					pes.coneRadius = coneRadius;
					break;

				case EmitShapes.Ring:
					float ringRadius = pes.ringRadius;
					float ringOuter = pes.ringOuterRadius;

					TorusHandle(ref ringRadius, ref ringOuter, transform);

					pes.ringRadius = ringRadius;
					pes.ringOuterRadius = ringOuter;
					break;

				case EmitShapes.Line:
					pes.lineLength = LineHandle(pes.lineLength, transform);
					break;

				case EmitShapes.Mesh:
					if (pes.emitMesh == null) {
						break;
					}

					if (s_lineMat == null) {
						s_lineMat = new Material(Shader.Find("Hidden/TCWireframeShader"));
						s_lineMat.hideFlags = HideFlags.HideAndDontSave;
					}

					GL.wireframe = true;
					for (int i = 0; i < s_lineMat.passCount; ++i) {
						s_lineMat.SetPass(i);
						Graphics.DrawMeshNow(pes.emitMesh, Matrix4x4.TRS(transform.position, transform.rotation, transform.localScale));
					}

					GL.wireframe = false;

					break;
			}
		}

		//=========================================
		//Handle functions
		static float ValueSlider(Vector3 offset, float val, Vector3 dir, float scale) {
			Vector3 pos = offset + val * scale * dir;
			Vector3 newPos = Handles.Slider(pos, dir, HandleUtility.GetHandleSize(Vector3.zero) * 0.045f, Handles.DotHandleCap, 0.0f);
			return Mathf.Abs(val + Vector3.Dot(newPos - pos, dir) / scale);
		}

		static void DrawDiscCap(Vector3 p1, Vector3 p2, float mSign, Vector3 h, float hsign, float rounding,
			Vector3 discNorm,
			Vector3 move, Vector3 up, float rMin) {
			Vector3 rup = hsign * h + hsign * rounding * up;
			Vector3 mVec = mSign * rounding * move;

			Handles.DrawLine(p2 + h, p2 - h);
			Handles.DrawWireArc(p2 + hsign * h - mVec, mSign * hsign * -discNorm, p2, 90.0f, rounding);

			if (rounding < rMin) {
				Handles.DrawLine(p1 + rup + mVec, p2 + rup - mSign * rounding * move);
				Handles.DrawLine(p1 + h, p1 - h);

				Handles.DrawWireArc(p1 + hsign * h + mVec, mSign * hsign * discNorm, -p1, 90.0f, rounding);
			} else {
				Handles.DrawLine(rup, p2 + rup - mVec);
			}
		}

		static void DrawDiscWiresVertical(float rMin, float rMax, float height, float rounding, float angle) {
			Vector3 up = Vector3.up;
			Vector3 right = Vector3.right;
			Vector3 fw = Vector3.forward;

			float h = height / 2.0f - rounding;
			Vector3 hup = h * up;

			Vector3 fw1 = fw * (rMin - rounding);
			Vector3 fw2 = fw * (rMax + rounding);

			Vector3 bck1 = -fw * (rMin - rounding);
			Vector3 bck2 = -fw * (rMax + rounding);

			Vector3 rt1 = right * (rMin - rounding);
			Vector3 rt2 = right * (rMax + rounding);

			Vector3 lt1 = -right * (rMin - rounding);
			Vector3 lt2 = -right * (rMax + rounding);

			DrawDiscCap(fw1, fw2, 1.0f, hup, 1.0f, rounding, right, fw, up, rMin);
			DrawDiscCap(fw1, fw2, 1.0f, hup, -1.0f, rounding, right, fw, up, rMin);

			DrawDiscCap(rt1, rt2, 1.0f, hup, 1.0f, rounding, -fw, right, up, rMin);
			DrawDiscCap(rt1, rt2, 1.0f, hup, -1.0f, rounding, -fw, right, up, rMin);

			if (angle > 90) {
				DrawDiscCap(lt1, lt2, -1.0f, hup, 1.0f, rounding, -fw, right, up, rMin);
				DrawDiscCap(lt1, lt2, -1.0f, hup, -1.0f, rounding, -fw, right, up, rMin);
			}

			if (angle > 180) {
				DrawDiscCap(bck1, bck2, -1.0f, hup, 1.0f, rounding, right, fw, up, rMin);
				DrawDiscCap(bck1, bck2, -1.0f, hup, -1.0f, rounding, right, fw, up, rMin);
			}
		}

		static void ArcAngle(float angle, float radius, float height, float rounding, float roundMult) {
			Handles.DrawWireArc((height / 2.0f - rounding) * Vector3.up, -Vector3.up, Vector3.right, angle,
				radius + roundMult * rounding);
			Handles.DrawWireArc(-(height / 2.0f - rounding) * Vector3.up, -Vector3.up, Vector3.right, angle,
				radius + roundMult * rounding);

			Handles.DrawWireArc(height / 2.0f * Vector3.up, -Vector3.up, Vector3.right, angle, radius);
			Handles.DrawWireArc(-height / 2.0f * Vector3.up, -Vector3.up, Vector3.right, angle, radius);
		}

		static void DiscValueSlider(float rMin, float rMax, Vector3 dir, float sc, out float rMinOut, out float rMaxOut) {
			if (Mathf.Abs(rMin) > Mathf.Epsilon) {
				rMin = ValueSlider(Vector3.zero, rMin, dir, sc);
			}

			rMax = ValueSlider(Vector3.zero, rMax, dir, sc);

			rMinOut = rMin;
			rMaxOut = rMax;
		}

		static void DrawBoxCorner(Vector3 sz, float xflip, float yflip, float zflip, float r) {
			sz = sz * 0.5f;
			Handles.DrawWireArc(new Vector3(xflip * (sz.x - r), yflip * (sz.y - r), zflip * (sz.z - r)),
				yflip * xflip * Vector3.forward,
				xflip * Vector3.right, 90.0f, r);
			Handles.DrawWireArc(new Vector3(xflip * (sz.x - r), yflip * (sz.y - r), zflip * (sz.z - r)),
				zflip * yflip * Vector3.right,
				yflip * Vector3.up, 90.0f, r);
			Handles.DrawWireArc(new Vector3(xflip * (sz.x - r), yflip * (sz.y - r), zflip * (sz.z - r)),
				zflip * xflip * Vector3.up,
				zflip * Vector3.forward, 90.0f, r);
		}

		static void DrawBoxLine(Vector3 d1, Vector3 d2, Vector3 d3, float r, float x, float y, float z) {
			x *= 0.5f;
			y *= 0.5f;
			z *= 0.5f;
			Vector3 pp = (x - r) * d1 + y * d2;
			Vector3 dd = (z - r) * d3;
			Handles.DrawLine(pp - dd, pp + dd);

			Vector3 pph = x * d1 + (y - r) * d2;
			Handles.DrawLine(pph - dd, pph + dd);
		}

		static float RadiusDisc(Vector3 offset, Vector3 norm, Vector3 right, Vector3 forward, float r, float sc) {
			Handles.DrawWireDisc(offset, norm, r);

			float newVal = ValueSlider(offset, r, right, sc);

			if (newVal == r) {
				newVal = ValueSlider(offset, r, -right, sc);
			}

			if (newVal == r) {
				newVal = ValueSlider(offset, r, forward, sc);
			}

			if (newVal == r) {
				newVal = ValueSlider(offset, r, -forward, sc);
			}

			return newVal;
		}

		static void BaseHandle(Transform trans, Vector3 scale) {
			Handles.matrix = Matrix4x4.TRS(trans.position, trans.rotation, scale);
		}

		static float ScaleXZ(Transform trans) {
			var localScale = trans.localScale;
			return Mathf.Max(localScale.x, localScale.z);
		}

		static float ScaleTrans(Transform trans) {
			var localScale = trans.localScale;
			return Mathf.Max(localScale.x, localScale.y, localScale.z);
		}

		public static float LineHandle(float length, Transform trans) {
			BaseHandle(trans, Vector3.one);
			var localScale = trans.localScale;
			Handles.DrawLine(Vector3.zero, length * localScale.z * Vector3.forward);
			return ValueSlider(Vector3.zero, length, Vector3.forward, localScale.z);
		}

		public static Vector3 CubeHandle(Transform trans, Vector3 size) {
			BaseHandle(trans, Vector3.one);
			Vector3 orig = size;

			var localScale = trans.localScale;
			size.x = ValueSlider(Vector3.zero, size.x * 0.5f, Vector3.right, localScale.x) * 2.0f;
			size.y = ValueSlider(Vector3.zero, size.y * 0.5f, Vector3.up, localScale.y) * 2.0f;
			size.z = ValueSlider(Vector3.zero, size.z * 0.5f, Vector3.forward, localScale.z) * 2.0f;

			if (size.x == orig.x) {
				size.x = ValueSlider(Vector3.zero, size.x * 0.5f, -Vector3.right, trans.localScale.x) * 2.0f;
			}

			if (size.y == orig.y) {
				size.y = ValueSlider(Vector3.zero, size.y * 0.5f, -Vector3.up, trans.localScale.y) * 2.0f;
			}

			if (size.z == orig.z) {
				size.z = ValueSlider(Vector3.zero, size.z * 0.5f, -Vector3.forward, trans.localScale.z) * 2.0f;
			}

			return size;
		}

		public static float HemisphereHandle(Transform trans, float radius, bool nonUniform = false) {
			Vector3 scale;
			float sc = 1.0f;

			if (nonUniform) {
				scale = trans.localScale;
			} else {
				sc = ScaleTrans(trans);
				scale = new Vector3(sc, sc, sc);
			}

			BaseHandle(trans, scale);

			Handles.DrawWireArc(Vector3.zero, Vector3.forward, Vector3.right, 180.0f, radius);
			Handles.DrawWireArc(Vector3.zero, Vector3.right, -Vector3.forward, 180.0f, radius);
			Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius * sc);

			var newVal = ValueSlider(Vector3.zero, radius, Vector3.up, 1.0f);

			if (newVal == radius) {
				newVal = ValueSlider(Vector3.zero, radius, Vector3.right, 1.0f);
			}

			if (newVal == radius) {
				newVal = ValueSlider(Vector3.zero, radius, -Vector3.right, 1.0f);
			}

			if (newVal == radius) {
				newVal = ValueSlider(Vector3.zero, radius, Vector3.forward, 1.0f);
			}

			if (newVal == radius) {
				newVal = ValueSlider(Vector3.zero, radius, -Vector3.forward, 1.0f);
			}

			return newVal;
		}

		public static void ConeHandle(ref float angle, ref float height, ref float radius, Transform trans,
			bool flip = false) {
			BaseHandle(trans, trans.localScale);

			float r = radius;
			float r2 = Mathf.Tan(angle * Mathf.Deg2Rad) * height;
			float f = flip ? -1 : 1;
			float ret = RadiusDisc(Vector3.zero, Vector3.forward, Vector3.right, Vector3.up, r, 1.0f);

			Vector3 up = height * Vector3.forward;

			Handles.DrawLine(-Vector3.right * r, up - Vector3.right * (f * r2 + r));
			Handles.DrawLine(Vector3.right * r, up + Vector3.right * (f * r2 + r));

			Handles.DrawLine(-Vector3.up * r, up - Vector3.up * (f * r2 + r));
			Handles.DrawLine(Vector3.up * r, up + Vector3.up * (f * r2 + r));

			if (Mathf.Abs(height) > Mathf.Epsilon) {
				angle =
					Mathf.Clamp(
						Mathf.Atan((RadiusDisc(up, Vector3.forward, Vector3.right, Vector3.up, r2 + r, 1.0f) - r) / height) *
						Mathf.Rad2Deg, 0.0f, 90.0f);
			}

			height = ValueSlider(Vector3.zero, height, Vector3.forward, 1.0f);
			radius = ret;
		}

		public static float RadiusHandle(Transform trans, float radius, bool nonUniform = false) {
			float sc = 1.0f;
			Vector3 nsc = Vector3.one;

			if (nonUniform) {
				nsc = trans.localScale;
			} else {
				sc = ScaleTrans(trans);
			}

			Handles.matrix = Matrix4x4.TRS(trans.position, trans.rotation, nsc);
			return
				Handles.RadiusHandle(Quaternion.identity, Vector3.zero, radius * sc) / sc;
		}

		public static void TorusHandle(ref float innerRadius, ref float outerRadius, Transform trans) {
			BaseHandle(trans, trans.localScale);
			Vector2 ret;

			ret.x = RadiusDisc(Vector3.zero, Vector3.forward, Vector3.right, Vector3.up, innerRadius, 1.0f);

			Color c = Handles.color;

			Handles.color = new Color(c.r, c.g, c.b, 0.4f);

			Handles.DrawWireDisc(Vector3.zero, Vector3.forward, innerRadius - outerRadius);
			Handles.DrawWireDisc(Vector3.zero, Vector3.forward, innerRadius + outerRadius);

			Handles.DrawWireDisc(Vector3.zero + 0.5f * outerRadius * Vector3.forward, Vector3.forward,
				innerRadius - 0.5f * Mathf.Sqrt(2.0f) * outerRadius);
			Handles.DrawWireDisc(Vector3.zero - 0.5f * outerRadius * Vector3.forward, Vector3.forward,
				innerRadius - 0.5f * Mathf.Sqrt(2.0f) * outerRadius);

			Handles.DrawWireDisc(Vector3.zero + 0.5f * outerRadius * Vector3.forward, Vector3.forward,
				innerRadius + 0.5f * Mathf.Sqrt(2.0f) * outerRadius);
			Handles.DrawWireDisc(Vector3.zero - 0.5f * outerRadius * Vector3.forward, Vector3.forward,
				innerRadius + 0.5f * Mathf.Sqrt(2.0f) * outerRadius);

			Handles.DrawWireDisc(Vector3.zero + outerRadius * Vector3.forward, Vector3.forward, innerRadius);
			Handles.DrawWireDisc(Vector3.zero - outerRadius * Vector3.forward, Vector3.forward, innerRadius);

			Vector3 cross1 = (Vector3.up + Vector3.right).normalized;
			Vector3 cross2 = (Vector3.up - Vector3.right).normalized;

			Handles.DrawWireDisc(Vector3.zero + cross1 * innerRadius, Vector3.Cross(cross1, Vector3.forward), outerRadius);
			Handles.DrawWireDisc(Vector3.zero - cross1 * innerRadius, Vector3.Cross(cross1, Vector3.forward), outerRadius);

			Handles.DrawWireDisc(Vector3.zero + cross2 * innerRadius, Vector3.Cross(cross2, Vector3.forward), outerRadius);
			Handles.DrawWireDisc(Vector3.zero - cross2 * innerRadius, Vector3.Cross(cross2, Vector3.forward), outerRadius);

			Handles.color = c;

			ret.y = outerRadius;
			ret.y = RadiusDisc(Vector3.up * innerRadius, Vector3.right, Vector3.forward, Vector3.up, ret.y, 1.0f);

			if (ret.y == outerRadius) {
				ret.y = RadiusDisc(Vector3.right * innerRadius, Vector3.up, Vector3.right, Vector3.forward, ret.y, 1.0f);
			}

			if (ret.y == outerRadius) {
				ret.y = RadiusDisc(-Vector3.right * innerRadius, Vector3.up, Vector3.right, Vector3.forward, ret.y, 1.0f);
			}

			if (ret.y == outerRadius) {
				ret.y = RadiusDisc(-Vector3.up * innerRadius, Vector3.right, Vector3.forward, Vector3.up, ret.y, 1.0f);
			}

			ret.y = Mathf.Min(ret.x, ret.y);

			innerRadius = ret.x;
			outerRadius = ret.y;
		}

		public static void DiscHandle(Transform trans, float rMin, float rMax, float height, float rounding, int mode,
			out float outRMin, out float outRMax, out float outRounding) {
			BaseHandle(trans, Vector3.one);
			float angle = 360.0f / Mathf.Pow(2.0f, mode);

			float xzs = ScaleXZ(trans);
			float ys = trans.localScale.y;

			DiscValueSlider(rMin, rMax, Vector3.right, xzs, out rMin, out rMax);
			DiscValueSlider(rMin, rMax, Vector3.forward, xzs, out rMin, out rMax);

			if (angle > 90.0f) {
				DiscValueSlider(rMin, rMax, -Vector3.right, xzs, out rMin, out rMax);
			}

			if (angle > 180.0f) {
				DiscValueSlider(rMin, rMax, -Vector3.forward, xzs, out rMin, out rMax);
			}

			if (Mathf.Abs(height) > Mathf.Epsilon) {
				ArcAngle(angle, rMax * xzs, height * ys, rounding, 1.0f);

				if (rMin - rounding > 0.0f) {
					ArcAngle(angle, rMin * xzs, height * ys, rounding, -1.0f);
				}
			}

			DrawDiscWiresVertical(rMin * xzs, rMax * xzs, height * ys, rounding, angle);

			rMin = Mathf.Max(Mathf.Min(rMin, rMax), 0.0f);
			rMax = Mathf.Max(Mathf.Max(rMin, rMax), 0.0f);

			outRMin = rMin;
			outRMax = rMax;

			outRounding = Mathf.Clamp(rounding, 0.0f, height / 2.0f);
		}

		public static Vector3 RoundedCubeHandle(Vector3 sz, float r, Transform trans) {
			BaseHandle(trans, Vector3.one);

			Vector3 szOrig = sz;

			sz = Vector3.Scale(sz, trans.localScale);

			DrawBoxCorner(sz, 1.0f, 1.0f, 1.0f, r);
			DrawBoxCorner(sz, -1.0f, 1.0f, 1.0f, r);

			DrawBoxCorner(sz, 1.0f, -1.0f, 1.0f, r);
			DrawBoxCorner(sz, -1.0f, -1.0f, 1.0f, r);

			DrawBoxCorner(sz, 1.0f, -1.0f, -1.0f, r);
			DrawBoxCorner(sz, 1.0f, 1.0f, -1.0f, r);

			DrawBoxCorner(sz, -1.0f, -1.0f, -1.0f, r);
			DrawBoxCorner(sz, -1.0f, 1.0f, -1.0f, r);

			DrawBoxLine(Vector3.right, Vector3.up, Vector3.forward, r, sz.x, sz.y, sz.z);
			DrawBoxLine(-Vector3.right, Vector3.up, Vector3.forward, r, sz.x, sz.y, sz.z);

			DrawBoxLine(Vector3.right, -Vector3.up, Vector3.forward, r, sz.x, sz.y, sz.z);
			DrawBoxLine(-Vector3.right, -Vector3.up, Vector3.forward, r, sz.x, sz.y, sz.z);

			DrawBoxLine(-Vector3.up, -Vector3.forward, Vector3.right, r, sz.y, sz.z, sz.x);
			DrawBoxLine(-Vector3.up, Vector3.forward, Vector3.right, r, sz.y, sz.z, sz.x);

			DrawBoxLine(Vector3.up, -Vector3.forward, Vector3.right, r, sz.y, sz.z, sz.x);
			DrawBoxLine(Vector3.up, Vector3.forward, Vector3.right, r, sz.y, sz.z, sz.x);

			DrawBoxLine(-Vector3.right, -Vector3.forward, Vector3.up, r, sz.x, sz.z, sz.y);
			DrawBoxLine(Vector3.right, -Vector3.forward, Vector3.up, r, sz.x, sz.z, sz.y);

			DrawBoxLine(-Vector3.right, Vector3.forward, Vector3.up, r, sz.x, sz.z, sz.y);
			DrawBoxLine(Vector3.right, Vector3.forward, Vector3.up, r, sz.x, sz.z, sz.y);

			return szOrig;
		}

		public static Vector2 CapsuleHandle(Transform transform, float radius, float height) {
			BaseHandle(transform, Vector3.one);

			float xzs = ScaleXZ(transform);
			float ys = transform.localScale.y;

			var newVal = ValueSlider(Vector3.zero, radius, Vector3.right, xzs);

			if (newVal == radius) {
				newVal = ValueSlider(Vector3.zero, radius, -Vector3.right, xzs);
			}

			if (newVal == radius) {
				newVal = ValueSlider(Vector3.zero, radius, Vector3.forward, xzs);
			}

			if (newVal == radius) {
				newVal = ValueSlider(Vector3.zero, radius, -Vector3.forward, xzs);
			}

			var newHeight = ValueSlider(Vector3.zero, 0.5f * height, Vector3.up, xzs) * 2.0f;

			if (newHeight == height) {
				newHeight = ValueSlider(Vector3.zero, 0.5f * height, -Vector3.up, xzs) * 2.0f;
			}

			float offset = Mathf.Max(0.0f, 0.5f * newHeight - newVal);

			Vector3 ymax = offset * ys * Vector3.up;
			Vector3 ymin = -offset * ys * Vector3.up;

			Handles.DrawWireDisc(ymax, Vector3.up, radius * xzs);
			Handles.DrawWireDisc(ymin, -Vector3.up, radius * xzs);

			Handles.DrawLine(ymin + radius * xzs * Vector3.right, ymax + radius * xzs * Vector3.right);
			Handles.DrawLine(ymin + radius * xzs * Vector3.forward, ymax + radius * xzs * Vector3.forward);
			Handles.DrawLine(ymin + radius * xzs * -Vector3.right, ymax + radius * xzs * -Vector3.right);
			Handles.DrawLine(ymin + radius * xzs * -Vector3.forward, ymax + radius * xzs * -Vector3.forward);

			Handles.DrawWireArc(ymax, Vector3.forward, Vector3.right, 180.0f, radius * xzs);
			Handles.DrawWireArc(ymax, Vector3.right, -Vector3.forward, 180.0f, radius * xzs);

			Handles.DrawWireArc(ymin, Vector3.forward, -Vector3.right, 180.0f, radius * xzs);
			Handles.DrawWireArc(ymin, Vector3.right, Vector3.forward, 180.0f, radius * xzs);

			return new Vector2(newVal, Mathf.Max(newHeight, newVal * 2.0f));
		}
	}
}