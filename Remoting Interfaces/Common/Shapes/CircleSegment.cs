using System;
using System.Collections.Generic;
using System.Text;

namespace UrbanChallenge.Common.Shapes {
	[Serializable]
	public struct CircleSegment : IEquatable<CircleSegment> {
		public double r;
		public Coordinates center;
		public double startAngle, endAngle;
		public bool ccw;

		public CircleSegment(Circle circle, double startAngle, double endAngle, bool ccw) {
			startAngle = Math.IEEERemainder(startAngle, 2*Math.PI);
			this.r = circle.r;
			this.center = circle.center;
			this.startAngle = startAngle;
			this.endAngle = endAngle;
			this.ccw = ccw;

			if (ccw) {
				// make sure endangle is bigger than start angle for counter-clockwise
				while (endAngle < startAngle)
					endAngle += 2*Math.PI;
			}
			else {
				// make sure endangle is smaller than start angle
				while (endAngle > startAngle)
					endAngle -= 2*Math.PI;
			}
		}

		public CircleSegment(double r, Coordinates center, Coordinates startPoint, double dist, bool ccw)
			: this(new Circle(r, center), startPoint, dist, ccw) { }

		public CircleSegment(Circle c, Coordinates startPoint, double dist, bool ccw) {
			this.r = c.r;
			this.center = c.center;
			this.startAngle = (startPoint - center).ArcTan;
			this.ccw = ccw;

			// s = rθ, θ = sk = s/r (s = arc len, r = radius, θ = angle, k = curvature (1/r)
			if (ccw) {
				this.endAngle = startAngle + dist/r;
			}
			else {
				this.endAngle = startAngle - dist/r;
			}
		}

		public CircleSegment(double r, Coordinates center, Coordinates startPoint, Coordinates endPoint, bool ccw) {
			this.r = r;
			this.center = center;
			this.startAngle = (startPoint - center).ArcTan;
			this.endAngle = (endPoint - center).ArcTan;
			this.ccw = ccw;

			// s = rθ, θ = sk = s/r (s = arc len, r = radius, θ = angle, k = curvature (1/r)
			if (ccw) {
				while (endAngle < startAngle)
					endAngle += 2*Math.PI;
			}
			else {
				while (endAngle > startAngle)
					endAngle -= 2*Math.PI;
			}
		}

		public bool Intersect(LineSegment line, out Coordinates[] pts) {
			double[] K;
			return Intersect(line, out pts, out K);
		}

		public bool Intersect(LineSegment line, out Coordinates[] pts, out double[] K) {
			// use the circle intersection tester
			Circle c = new Circle(r, center);
			if (!c.Intersect(line, out pts, out K)) return false;

			List<Coordinates> good_pts = new List<Coordinates>();
			List<double> good_k = new List<double>();
			
			for (int i = 0; i < pts.Length; i++) {
				// get the angle on the circle
				double theta = (pts[i] - center).ArcTan;

				if (ccw) {
					while (theta < startAngle) {
						theta += 2*Math.PI;
					}

					// check if we're less than the end angle
					if (theta < endAngle) {
						good_pts.Add(pts[i]);
						good_k.Add(K[i]);
					}
				}
				else {
					while (theta > startAngle) {
						theta -= 2*Math.PI;
					}

					if (theta > endAngle) {
						good_pts.Add(pts[i]);
						good_k.Add(K[i]);
					}
				}
			}

			pts = good_pts.ToArray();
			K = good_k.ToArray();
			return pts.Length > 0;
		}

		public Coordinates GetClosestPoint(Coordinates pt) {
			return center + r*Coordinates.FromAngle(GetClosestAngle(pt));
		}

		public double DistFromStart(Coordinates pt) {
			double angle = (pt - center).ArcTan;

			if (ccw) {
				while (angle < startAngle) {
					angle += 2*Math.PI;
				}

				return (angle - startAngle)*r;
			}
			else {
				while (angle > startAngle) {
					angle -= 2*Math.PI;
				}

				return (startAngle - angle)*r;
			}
		}

		public double GetClosestAngle(Coordinates pt) {
			double angle = (pt - center).ArcTan;

			if (ccw) {
				while (angle < startAngle) {
					angle += 2*Math.PI;
				}

				if (angle < endAngle) {
					// return the angle, we're in range
					return angle;
				}
				else {
					// we're after the end-angle, split the middle
					double halfAngle = Math.PI + (startAngle + endAngle)/2.0;
					if (angle > halfAngle)
						return startAngle;
					else
						return endAngle;
				}
			}
			else {
				while (angle > startAngle) {
					angle -= 2*Math.PI;
				}

				if (angle > endAngle) {
					return angle;
				}
				else {
					double halfAngle = Math.PI + (startAngle + endAngle)/2.0;
					if (angle < halfAngle) {
						return startAngle;
					}
					else {
						return endAngle;
					}
				}
			}
		}

		public Coordinates[] ToPoints(int num) {
			double deltaAngle = (endAngle-startAngle)/(num-1);

			Coordinates[] pts = new Coordinates[num];
			for (int i = 0; i < num; i++) {
				pts[i] = center + Coordinates.FromAngle(startAngle + deltaAngle*i)*r;
			}

			return pts;
		}

		public double AngularLength {
			get { return Math.Abs(endAngle - startAngle); }
		}

		public double Length {
			get { return r*Math.Abs(endAngle-startAngle); }
		}

		public CircleSegment Transform(IPointTransformer transform) {
			Coordinates newCenter = transform.TransformPoint(center);
			Coordinates newStartPoint = transform.TransformPoint(center + Coordinates.FromAngle(startAngle)*r);
			Coordinates newEndPoint = transform.TransformPoint(center + Coordinates.FromAngle(endAngle)*r);

			return new CircleSegment(r, newCenter, newStartPoint, newEndPoint, ccw);
		}

		#region IEquatable<CircleSegment> Members

		public override bool Equals(object obj) {
			if (obj is CircleSegment) {
				return Equals((CircleSegment)obj);
			}
			else {
				return false;
			}
		}

		public override int GetHashCode() {
			return r.GetHashCode() ^ center.GetHashCode() ^ startAngle.GetHashCode() ^ endAngle.GetHashCode() ^ ccw.GetHashCode();
		}

		public bool Equals(CircleSegment other) {
			return (other.r == r) && (other.center == center) && (other.startAngle == startAngle) && (other.endAngle == endAngle) && (other.ccw == ccw);
		}

		#endregion
	}
}
