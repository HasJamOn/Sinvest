using Sandbox; 
using Sinvest;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

/// <summary>
/// A custom Panorama panel that renders a line-graph representing market history.
/// It uses a segment-based drawing approach, creating individual panels for line
/// segments to bypass the lack of a traditional 2D Canvas API in Panorama.
/// </summary>
public class FundinoMarketGraph : Panel
{
    public List<double> History { get; set; } = new();
    public float MinVal { get; set; }
    public float MaxVal { get; set; }

    /* --- Padding Configuration --- */
    // These must align with the SCSS to ensure the graph line doesn't 
    // clip into the axis labels or borders.
    public float HorizontalPadding { get; set; } = 0f;
    public float VerticalPadding   { get; set; } = 10f; 

    /// <summary> The most recent Rect provided by the layout engine. </summary>
    public Rect LastKnownRect { get; private set; }

    private bool _queuedUpdate = false;

    /// <summary>
    /// Triggered by the UI engine whenever the panel's geometry is recalculated.
    /// This is the most reliable place to draw, as it guarantees we have a 
    /// non-zero width and height.
    /// </summary>
    public override void OnLayout( ref Rect rect )
    {
	    // 1. DYNAMIC SYNC: Fetch padding/margin from the SCSS computed values
	    // This allows you to change the SCSS and have the graph "just work."
	    UpdateDynamicPadding();

	    if ( _queuedUpdate
	         || MathF.Abs( rect.Width  - LastKnownRect.Width  ) > 0.5f
	         || MathF.Abs( rect.Height - LastKnownRect.Height ) > 0.5f )
	    {
		    LastKnownRect  = rect;
		    _queuedUpdate = false;
		    DrawSegments( rect );
	    }
    }
    
    private void UpdateDynamicPadding()
    {
	    // Traverses up to .chart-container to find the .y-axis sibling
	    var yAxis = Parent?.Parent?.Children.FirstOrDefault( x => x.HasClass( "y-axis" ) ); 

	    if ( yAxis != null )
	    {
		    // Use .Value (pixels) and fallback to 10f if SCSS isn't loaded yet
		    float top = yAxis.ComputedStyle.PaddingTop?.Value ?? 10f;
		    float bottom = yAxis.ComputedStyle.PaddingBottom?.Value ?? 10f;
        
		    // We take the top padding to ensure the Y=0 point aligns with the first Y-label
		    VerticalPadding = top;
	    }

	    // HorizontalPadding maps to the .graph-window margin
	    HorizontalPadding = ComputedStyle.MarginLeft?.Value ?? 0f;
    }

    /// <summary>
    /// Forces the graph to refresh its visual segments. 
    /// If the panel hasn't been laid out yet, it queues the update for the next frame.
    /// </summary>
    public void RequestUpdate()
    {
        if ( LastKnownRect.Width > 0 && LastKnownRect.Height > 0 )
        {
            DrawSegments( LastKnownRect );
        }
        else
        {
            _queuedUpdate = true;
        }
    }

    /* --- Coordinate Mapping Logic --- */

    /// <summary>
    /// Converts a price value into a vertical pixel offset.
    /// Inverts the scale so higher prices move toward the top (Y=0).
    /// </summary>
    public float MapPriceToY( float price, Rect rect )
    {
        float range = MaxVal - MinVal;
        if ( range <= 0f ) range = 1f;

        float drawHeight = rect.Height - VerticalPadding * 2f;
        float normalized = ( price - MinVal ) / range; 
        
        return VerticalPadding + ( 1f - normalized ) * drawHeight;
    }

    /// <summary>
    /// Maps a history index into a horizontal pixel offset.
    /// </summary>
    public float MapIndexToX( int index, int totalCount, Rect rect )
    {
        if ( totalCount <= 1 ) return HorizontalPadding;
        float drawWidth = rect.Width - HorizontalPadding * 2f;
        return HorizontalPadding + ( index / (float)( totalCount - 1 ) ) * drawWidth;
    }

    public float GetCurrentPriceY( float price )
    {
        if ( LastKnownRect.Height <= 0 ) return 0f;
        return MapPriceToY( price, LastKnownRect );
    }

    /// <summary>
    /// The Core Painter: Instantiates 'graph-line' panels and calculates their 
    /// transforms (rotation and length) to connect history points.
    /// </summary>
    private void DrawSegments( Rect rect )
    {
        // Clear previous segments to prevent visual stacking.
        DeleteChildren( true );

        if ( History == null || History.Count < 2 ) return;

        int count = History.Count;

        for ( int i = 0; i < count - 1; i++ )
        {
            float x1 = MapIndexToX( i,     count, rect );
            float x2 = MapIndexToX( i + 1, count, rect );
            float y1 = MapPriceToY( (float)History[i],     rect );
            float y2 = MapPriceToY( (float)History[i + 1], rect );

            if ( !float.IsFinite( y1 ) || !float.IsFinite( y2 ) ) continue;

            // Geometry Math: Calculate distance (width) and angle (rotation).
            float dx       = x2 - x1;
            float dy       = y2 - y1;
            float distance = MathF.Sqrt( dx * dx + dy * dy );
            float angle    = MathF.Atan2( dy, dx ) * ( 180f / MathF.PI );

            var line = AddChild<Panel>( "graph-line" );

            // Applying inline styles for the "Procedural Line"
            line.Style.Position         = PositionMode.Absolute;
            line.Style.Left             = x1;
            line.Style.Top              = y1;
            // 0.5px overlap to combat anti-aliasing seams between segments.
            line.Style.Width            = distance + 0.5f; 
            line.Style.Height           = 2;
            line.Style.TransformOriginX = 0f;
            line.Style.TransformOriginY = 0.5f;

            var transform = new PanelTransform();
            transform.AddRotation( 0, 0, angle );
            line.Style.Transform = transform;
        }
    }
}
