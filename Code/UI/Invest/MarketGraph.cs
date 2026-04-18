using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

public class MarketGraph : Panel
{
	public List<double> History { get; set; } = new();
	public Color GraphColor { get; set; } = Color.Parse("#00ff88") ?? Color.Green;

	public void UpdateGraph()
	{
		DeleteChildren(true);

		if (History == null || History.Count < 2) return;

		float min = (float)History.Min();
		float max = (float)History.Max();
		float range = MathF.Max(max - min, 1.0f);

		for (int i = 0; i < History.Count - 1; i++)
		{
			// Point A (Current)
			float x1 = (i / (float)(History.Count - 1)) * Box.Rect.Width;
			float y1 = (1.0f - (((float)History[i] - min) / range)) * Box.Rect.Height;

			// Point B (Next)
			float x2 = ((i + 1) / (float)(History.Count - 1)) * Box.Rect.Width;
			float y2 = (1.0f - (((float)History[i + 1] - min) / range)) * Box.Rect.Height;

			// Calculate Vector from A to B
			float dx = x2 - x1;
			float dy = y2 - y1;
			float distance = MathF.Sqrt(dx * dx + dy * dy);
			float angle = MathF.Atan2(dy, dx) * (180.0f / MathF.PI);

			// Create the Line Segment
			var line = AddChild<Panel>("graph-line");
			line.Style.Position = PositionMode.Absolute;
			line.Style.Left = x1;
			line.Style.Top = y1;
			line.Style.Width = distance;
			line.Style.Height = 2; // Line thickness
			line.Style.BackgroundColor = GraphColor;

			// Set the pivot to the left so it rotates from the start point
			line.Style.TransformOriginX = 0;
			line.Style.TransformOriginY = 0.5f;

			// Create the transform manually to avoid AddRotation errors
			var transform = new PanelTransform();
			transform.AddRotation(0, 0, angle);
			line.Style.Transform = transform;
		}
	}
}
