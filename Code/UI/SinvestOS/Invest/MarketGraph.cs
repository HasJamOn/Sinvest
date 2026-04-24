using Sandbox;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

public class MarketGraph : Panel
{
    /// <summary>
    /// The price points to be drawn.
    /// </summary>
    public List<double> History { get; set; } = new();

    /// <summary>
    /// The floor of the graph, synced with the Y-axis labels.
    /// </summary>
    public float MinVal { get; set; }

    /// <summary>
    /// The ceiling of the graph, synced with the Y-axis labels.
    /// </summary>
    public float MaxVal { get; set; }

    private Rect _lastRect;

    public override void OnLayout( ref Rect rect )
    {
        // Redraw if the UI panel changes size (e.g., window resize)
        if ( rect.Width != _lastRect.Width || rect.Height != _lastRect.Height )
        {
            _lastRect = rect;
            UpdateGraph();
        }
    }

    /// <summary>
    /// Clears the current line and redraws based on History and Min/Max bounds.
    /// </summary>
    public void UpdateGraph()
{
    DeleteChildren( true );

    var rect = Box.Rect;
    if ( rect.Width <= 0 || rect.Height <= 0 || History == null || History.Count < 2 )
        return;

    float range = MaxVal - MinVal;
    if ( range <= 0 ) // Fallback if range is invalid
    {
        float localMin = (float)History.Min();
        float localMax = (float)History.Max();
        range = MathF.Max(localMax - localMin, 1.0f);
        MinVal = localMin;
        MaxVal = localMax;
    }

    // NEW: Vertical padding factor (5% top, 5% bottom)
    // This prevents the line from clipping into the container borders.
    float paddingFactor = 0.05f; 
    float effectiveHeight = rect.Height * (1.0f - (paddingFactor * 2));
    float topOffset = rect.Height * paddingFactor;

    for ( int i = 0; i < History.Count - 1; i++ )
    {
        float x1 = (i / (float)(History.Count - 1)) * rect.Width;
        float x2 = ((i + 1) / (float)(History.Count - 1)) * rect.Width;

        // Calculate normalized values
        float val1 = ((float)History[i] - MinVal) / range;
        float val2 = ((float)History[i + 1] - MinVal) / range;

        // Map to the "Safe Zone" height
        // We multiply by effectiveHeight and add topOffset to keep it centered
        float y1 = topOffset + (1.0f - val1) * effectiveHeight;
        float y2 = topOffset + (1.0f - val2) * effectiveHeight;

        if ( !float.IsFinite( y1 ) || !float.IsFinite( y2 ) )
            continue;

        float dx = x2 - x1;
        float dy = y2 - y1;
        float distance = MathF.Sqrt( dx * dx + dy * dy );
        float angle = MathF.Atan2( dy, dx ) * (180.0f / MathF.PI);

        var line = AddChild<Panel>( "graph-line" );
        line.Style.Position = PositionMode.Absolute;
        line.Style.Left = x1;
        line.Style.Top = y1;
        line.Style.Width = distance + 0.5f; 
        line.Style.Height = 2; 

        line.Style.TransformOriginX = 0;
        line.Style.TransformOriginY = 0.5f;

        var transform = new PanelTransform();
        transform.AddRotation( 0, 0, angle );
        line.Style.Transform = transform;
    }
}
}
