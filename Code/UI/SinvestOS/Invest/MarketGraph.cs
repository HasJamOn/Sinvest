using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

public class MarketGraph : Panel
{
    private List<double> _history = new();
    public List<double> History 
    { 
        get => _history; 
        set 
        { 
            _history = value; 
            _needsUpdate = true; // Mark for redraw
        } 
    }

    private bool _needsUpdate = false;
    private Rect _lastRect;

    public override void Tick()
    {
        // If the price history changed, we need to redraw
        if ( _needsUpdate )
        {
            UpdateGraph();
            _needsUpdate = false;
        }
    }

    public override void OnLayout( ref Rect rect )
    {
	    // If the window was resized (or finally gained dimensions), redraw
	    if ( rect.Width != _lastRect.Width || rect.Height != _lastRect.Height )
	    {
		    _lastRect = rect;
		    UpdateGraph();
	    }
    }

    public void UpdateGraph()
    {
        DeleteChildren(true);
        
        // Use the rect from the layout engine
        var rect = Box.Rect;
        float width = rect.Width;
        float height = rect.Height;

        // If we still don't have a width, don't bother drawing
        if ( width <= 0 || History == null || History.Count < 2 ) 
            return;

        float verticalMargin = 20f; 
        float drawableHeight = height - (verticalMargin * 2);

        float min = (float)History.Min();
        float max = (float)History.Max();
        float range = MathF.Max(max - min, 1.0f);

        for (int i = 0; i < History.Count - 1; i++)
        {
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
