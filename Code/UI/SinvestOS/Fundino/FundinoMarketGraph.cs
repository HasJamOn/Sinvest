using Sandbox; 
using Sinvest;
using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public class FundinoMarketGraph : Panel
{
    public List<double> History { get; set; } = new();
    public float MinVal { get; set; }
    public float MaxVal { get; set; }

    // -------------------------------------------------------------------------
    // PADDING CONSTANTS — must be kept in sync with SCSS manually.
    //
    // HorizontalPadding: pixels inset from left/right edge of the painter rect.
    //   • Matches padding-left/padding-right on .graph-labels in SCSS.
    //   • Set to 0 if you want the line to touch the edges (simplest alignment).
    //
    // VerticalPadding: pixels inset from top/bottom edge of the painter rect.
    //   • Matches padding-top/padding-bottom on .y-axis in SCSS.
    //   • Ensures the top and bottom y-labels are pixel-aligned with the graph.
    // -------------------------------------------------------------------------
    public float HorizontalPadding { get; set; } = 0f;
    public float VerticalPadding   { get; set; } = 10f; // matches .y-axis padding: 10px 0

    // The last rect that was actually used to draw — published so the parent
    // can call MapPriceToY() to position the current-price-tag correctly.
    public Rect LastKnownRect { get; private set; }

    private bool _queuedUpdate = false;

    // -------------------------------------------------------------------------
    // LIFECYCLE — OnLayout receives the *freshly computed* rect from the
    // layout engine, which is always correct even on the very first frame.
    // We use the size-change check as a lightweight guard so we don't rebuild
    // the whole child list every frame when nothing has changed.
    // -------------------------------------------------------------------------
    public override void OnLayout( ref Rect rect )
    {
        if ( _queuedUpdate
             || MathF.Abs( rect.Width  - LastKnownRect.Width  ) > 0.5f
             || MathF.Abs( rect.Height - LastKnownRect.Height ) > 0.5f )
        {
            // Store now, before we enter UpdateGraph, so _queuedUpdate = false
            // and any re-entrant call sees the correct rect.
            LastKnownRect  = rect;
            _queuedUpdate = false;
            DrawSegments( rect );
        }
    }

    // -------------------------------------------------------------------------
    // PUBLIC API — called by the parent (InvestMenu) whenever data changes.
    //
    // Strategy:
    //   • If we already have a valid rect (normal steady-state), redraw
    //     immediately with the stored rect — no layout ping needed.
    //   • If we don't yet have a rect (first frame, or after a DOM rebuild),
    //     set the flag so the *next* OnLayout call triggers the draw with the
    //     fresh rect the engine provides. This eliminates the Task.Delay hack.
    // -------------------------------------------------------------------------
    public void RequestUpdate()
    {
        if ( LastKnownRect.Width > 0 && LastKnownRect.Height > 0 )
        {
            DrawSegments( LastKnownRect );
        }
        else
        {
            // Panel has no size yet — defer until OnLayout fires.
            _queuedUpdate = true;
        }
    }

    // -------------------------------------------------------------------------
    // COORDINATE HELPERS — exposed as public so InvestMenu can use the exact
    // same math when positioning the current-price-tag and y-axis labels.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps a price value to a Y pixel coordinate (top = 0).
    /// Respects VerticalPadding so the result aligns with y-axis labels.
    /// </summary>
    public float MapPriceToY( float price, Rect rect )
    {
        float range = MaxVal - MinVal;
        if ( range <= 0f ) range = 1f;

        float drawHeight = rect.Height - VerticalPadding * 2f;
        float normalized = ( price - MinVal ) / range; // 0 = Min, 1 = Max
        // In UI space Y=0 is the top, so invert: high price → low Y.
        return VerticalPadding + ( 1f - normalized ) * drawHeight;
    }

    /// <summary>
    /// Maps a data-series index to an X pixel coordinate.
    /// Respects HorizontalPadding so the result aligns with x-axis labels.
    /// </summary>
    public float MapIndexToX( int index, int totalCount, Rect rect )
    {
        if ( totalCount <= 1 ) return HorizontalPadding;
        float drawWidth = rect.Width - HorizontalPadding * 2f;
        return HorizontalPadding + ( index / (float)( totalCount - 1 ) ) * drawWidth;
    }

    /// <summary>
    /// Convenience wrapper: maps the current price using the last known rect.
    /// Returns the midpoint of the rect if no valid rect exists yet.
    /// </summary>
    public float GetCurrentPriceY( float price )
    {
        if ( LastKnownRect.Height <= 0 ) return 0f;
        return MapPriceToY( price, LastKnownRect );
    }

    // -------------------------------------------------------------------------
    // DRAW IMPLEMENTATION — private; always called with a validated rect.
    // -------------------------------------------------------------------------
    private void DrawSegments( Rect rect )
    {
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

            float dx       = x2 - x1;
            float dy       = y2 - y1;
            float distance = MathF.Sqrt( dx * dx + dy * dy );
            float angle    = MathF.Atan2( dy, dx ) * ( 180f / MathF.PI );

            var line = AddChild<Panel>( "graph-line" );

            line.Style.Position        = PositionMode.Absolute;
            line.Style.Left            = x1;
            line.Style.Top             = y1;
            // +0.5px overlap prevents sub-pixel gaps between adjacent segments.
            line.Style.Width           = distance + 0.5f;
            line.Style.Height          = 2;
            line.Style.TransformOriginX = 0f;
            line.Style.TransformOriginY = 0.5f;

            var transform = new PanelTransform();
            transform.AddRotation( 0, 0, angle );
            line.Style.Transform = transform;
        }
    }
}
