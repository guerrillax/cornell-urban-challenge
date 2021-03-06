using System;
using System.Collections.Generic;
using System.Text;
using UrbanChallenge.OperationalUI.Common.Map;
using UrbanChallenge.Operational.Common;
using UrbanChallenge.OperationalUI.Common.DataItem;
using System.Drawing;
using UrbanChallenge.Common;
using UrbanChallenge.OperationalUI.Common.GraphicsWrapper;
using UrbanChallenge.OperationalUI.Common;
using UrbanChallenge.Common.Path;
using UrbanChallenge.Common.Shapes;
using System.Windows.Forms;

namespace UrbanChallenge.OperationalUI.Controls.DisplayObjects {
	public class LocalLaneModelDisplayObject : IHittable, IAttachable<LocalLaneModel>, IClearable, ISimpleColored, IProvideContextMenu {
		private LocalLaneModel laneModel;
		private Color color;
		private string name;

		private Font labelFont = new Font("Segoe UI", 8.25f, FontStyle.Bold);

		private double sigma = 0;

		private LinePath prevLeftBound;
		private LinePath prevRightBound;

		private ToolStripMenuItem[] menuItems;

		public LocalLaneModelDisplayObject(string name, Color color) {
			this.name = name;
			this.color = color;

			ToolStripMenuItem menuNoBounds = new ToolStripMenuItem("No Bounds", null, menuNoBounds_Click);
			ToolStripMenuItem menuOneSigma = new ToolStripMenuItem("1σ Bounds", null, menuOneSigma_Click);
			ToolStripMenuItem menuTwoSigma = new ToolStripMenuItem("2σ Bounds", null, menuTwoSigma_Click);

			menuNoBounds.Checked = true;

			menuItems = new ToolStripMenuItem[] { menuNoBounds, menuOneSigma, menuTwoSigma };
		}

		private void SetChecked(ToolStripMenuItem sender) {
			foreach (ToolStripMenuItem menuItem in menuItems) {
				if (!object.Equals(menuItem, sender)) {
					menuItem.Checked = false;
				}
				else {
					menuItem.Checked = true;
				}
			}
		}

		private void menuNoBounds_Click(object sender, EventArgs e) {
			sigma = 0;
			SetChecked(sender as ToolStripMenuItem);
		}

		private void menuOneSigma_Click(object sender, EventArgs e) {
			sigma = 1;
			SetChecked(sender as ToolStripMenuItem);
		}

		private void menuTwoSigma_Click(object sender, EventArgs e) {
			sigma = 2;
			SetChecked(sender as ToolStripMenuItem);
		}

		#region IHittable Members

		public System.Drawing.RectangleF GetBoundingBox() {
			return RectangleF.Empty;
		}

		public HitTestResult HitTest(Coordinates loc, float tol) {
			if (laneModel == null || laneModel.LanePath == null || laneModel.LanePath.Count <= 1) {
				return HitTestResult.NoHit;
			}

			// test for a hit on the center line
			Coordinates closestPoint = laneModel.LanePath.GetClosestPoint(loc).Location;
			double dist = closestPoint.DistanceTo(loc);

			if (prevLeftBound != null) {
				Coordinates leftPoint = prevLeftBound.GetClosestPoint(loc).Location;
				double newDist = leftPoint.DistanceTo(loc);
				if (newDist < dist) {
					dist = newDist;
					closestPoint = leftPoint;
				}
			}

			if (prevRightBound != null) {
				Coordinates rightPoint = prevRightBound.GetClosestPoint(loc).Location;
				double newDist = rightPoint.DistanceTo(loc);
				if (newDist < dist) {
					dist = newDist;
					closestPoint = rightPoint;
				}
			}

			if (closestPoint.DistanceTo(loc) < tol) {
				return new HitTestResult(this, true, closestPoint, null);
			}

			return HitTestResult.NoHit;
		}

		public IHittable Parent {
			get { return null; }
		}

		#endregion

		#region IRenderable Members

		public void Render(IGraphics g, WorldTransform wt) {
			LocalLaneModel laneModel = this.laneModel;

			if (laneModel == null || laneModel.LanePath == null || laneModel.LanePath.Count <= 1) {
				return;
			}

			// generate the left and right bounds
			LinePath leftBound = laneModel.LanePath.ShiftLateral(laneModel.Width/2.0);
			LinePath leftBound2 = null;
			if (laneModel.WidthVariance > 0.01) {
				leftBound2 = laneModel.LanePath.ShiftLateral(laneModel.Width/2.0 + Math.Sqrt(laneModel.WidthVariance)*1.96/2.0);
			}

			LinePath rightBound = laneModel.LanePath.ShiftLateral(-laneModel.Width/2.0);
			LinePath rightBound2 = null;
			if (laneModel.WidthVariance > 0.01) {
				rightBound2 = laneModel.LanePath.ShiftLateral(-laneModel.Width/2.0 - Math.Sqrt(laneModel.WidthVariance)*1.96/2.0);
			}

			// generate a polygon of the confidence region of the center line
			Polygon centerConfLeft = null;
			Polygon centerConfRight = null;
			Polygon centerConfFull = null;
			if (sigma > 0 && laneModel.LaneYVariance != null && laneModel.LaneYVariance.Length == laneModel.LanePath.Count) {
				float varianceThreshold = 9f*9f;
				if (laneModel.LaneYVariance[0] > varianceThreshold) {
					varianceThreshold = 600;
				}
				int numPoints;
				for (numPoints = 0; numPoints < laneModel.LaneYVariance.Length; numPoints++) {
					if (laneModel.LaneYVariance[numPoints] > varianceThreshold) break;
				}
				double[] leftDist = new double[numPoints];
				double[] rightDist = new double[numPoints];

				double prevDist = 0;
				for (int i = 0; i < numPoints; i++) {
					double dist = laneModel.LaneYVariance[i];
					if (dist > 0.01) {
						dist = Math.Sqrt(dist)*sigma;
						leftDist[i] = dist;
						rightDist[i] = -dist;
						prevDist = dist;
					}
					else {
						leftDist[i] = prevDist;
						rightDist[i] = -prevDist;
					}
				}

				// get the subset of the lane model we're interested in
				LinePath subset = laneModel.LanePath.SubPath(0, numPoints-1);

				LinePath left = subset.ShiftLateral(leftDist);
				LinePath right = subset.ShiftLateral(rightDist);

				Coordinates midTop = (left[left.Count-1] + right[right.Count-1])/2.0;
				Coordinates midBottom = (left[0] + right[0])/2.0;

				centerConfLeft = new Polygon(numPoints+2);
				centerConfLeft.Add(midTop);
				centerConfLeft.Add(midBottom);
				centerConfLeft.AddRange(left);

				centerConfRight = new Polygon(numPoints+2);
				centerConfRight.Add(midTop);
				centerConfRight.Add(midBottom);
				centerConfRight.AddRange(right);

				centerConfFull = new Polygon(numPoints*2);
				right.Reverse();
				centerConfFull.AddRange(right);
				centerConfFull.AddRange(left);
			}

			// draw the shits
			IPen pen = g.CreatePen();
			pen.Width = 1.0f / wt.Scale;
			pen.Color = color;
			
			// first draw the confidence polygon
			if (centerConfFull != null) {
				g.FillPolygon(Color.FromArgb(20, color), Utility.ToPointF(centerConfLeft));
				g.FillPolygon(Color.FromArgb(20, color), Utility.ToPointF(centerConfRight));
				pen.Color = Color.FromArgb(30, color);
				g.DrawPolygon(pen, Utility.ToPointF(centerConfFull));
			}

			// next draw the center line
			pen.Color = color;
			//g.DrawLines(pen, Utility.ToPointF(laneModel.LanePath));

			//// next draw the left lane confidence bound
			//if (leftBound2 != null) {
			//  pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
			//  g.DrawLines(pen, Utility.ToPointF(leftBound2));
			//}

			//// draw the right lane confidence bound
			//if (rightBound2 != null) {
			//  pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
			//  g.DrawLines(pen, Utility.ToPointF(rightBound2));
			//}

			// draw the left bound
			pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid;
			g.DrawLines(pen, Utility.ToPointF(leftBound));
			// draw the right bound
			g.DrawLines(pen, Utility.ToPointF(rightBound));

			// draw the model confidence 
			string labelString = laneModel.Probability.ToString("F3");
			SizeF stringSize = g.MeasureString(labelString, labelFont);
			stringSize.Width /= wt.Scale;
			stringSize.Height /= wt.Scale;
			RectangleF rect = new RectangleF(Utility.ToPointF(laneModel.LanePath[0]), stringSize);
			float inflateValue = 4/wt.Scale;
			rect.X -= inflateValue;
			rect.Y -= inflateValue;
			g.FillRectangle(Color.FromArgb(127, Color.White), rect);
			g.DrawString(labelString, labelFont, Color.Black, Utility.ToPointF(laneModel.LanePath[0]));

			prevLeftBound = leftBound;
			prevRightBound = rightBound;
		}

		public string Name {
			get { return name; }
		}

		#endregion

		#region ISimpleColored Members

		public Color Color {
			get { return color; }
			set { color = value; }
		}

		#endregion

		#region IClearable Members

		public void Clear() {
			this.laneModel = null;
			this.prevLeftBound = null;
			this.prevRightBound = null;
		}

		#endregion

		#region IAttachable<LocalLaneModel> Members

		public void SetCurrentValue(LocalLaneModel value, string label) {
			this.laneModel = value;
		}

		#endregion

		#region IProvideContextMenu Members

		public ICollection<ToolStripMenuItem> GetMenuItems() {
			return menuItems;
		}

		public void OnMenuOpening() {
			
		}

		#endregion
	}
}
