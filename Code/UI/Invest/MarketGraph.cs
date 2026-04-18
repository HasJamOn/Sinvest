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

	    // Force the panel to actually occupy its full parent space
	    Style.Width = Length.Percent(100);
	    Style.Height = Length.Percent(100);

	    var rect = Box.Rect;
	    float width = rect.Width;
	    float height = rect.Height;

	    if (width <= 0) return;

	    // Define internal "Safe Margins" here in C# instead of SCSS 
	    // to ensure the math is 100% under your control.
	    float verticalMargin = 20f; 
	    float drawableHeight = height - (verticalMargin * 2);

	    float min = (float)History.Min();
	    float max = (float)History.Max();
	    float range = MathF.Max(max - min, 1.0f);

	    for (int i = 0; i < History.Count - 1; i++)
	    {
		    // THE FIX: (i / (float)(History.Count - 1)) 
		    // When i is 59 and Count is 60, this equals 1.0 (The absolute right edge)
		    float x1 = (i / (float)(History.Count - 1)) * width;
		    float y1 = verticalMargin + (1.0f - (((float)History[i] - min) / range)) * drawableHeight;

		    float x2 = ((i + 1) / (float)(History.Count - 1)) * width;
		    float y2 = verticalMargin + (1.0f - (((float)History[i + 1] - min) / range)) * drawableHeight;

		    float dx = x2 - x1;
		    float dy = y2 - y1;
		    float distance = MathF.Sqrt(dx * dx + dy * dy);
		    float angle = MathF.Atan2(dy, dx) * (180.0f / MathF.PI);

		    var line = AddChild<Panel>("graph-line");
		    line.Style.Position = PositionMode.Absolute;
		    line.Style.Left = x1;
		    line.Style.Top = y1;
        
		    // Add a tiny overlap to prevent pixel gaps
		    line.Style.Width = distance + 0.5f; 
		    line.Style.Height = 2; 

		    line.Style.TransformOriginX = 0;
		    line.Style.TransformOriginY = 0.5f;

		    var transform = new PanelTransform();
		    transform.AddRotation(0, 0, angle);
		    line.Style.Transform = transform;
	    }
    }
}
