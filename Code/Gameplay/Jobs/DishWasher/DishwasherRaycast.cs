using Sandbox;
using System;

namespace Sinvest;

public sealed class DishwasherRaycast : Component
{
    [Property] public float ReachDistance  { get; set; } = 1000f;
    [Property] public bool  ShowDebug      { get; set; } = true;
    [Property] public GameObject PlateRoot { get; set; }

    [Property] public Vector3 EyeLocalOffset { get; set; } = new Vector3( 0f, -300f, 150f );
    [Property] public float   ScrubPlaneSize { get; set; } = 120f;

    // ── runtime state (fields so DrawDebugVisuals can read them) ──────────
    private SceneTraceResult _lastTrace;
    private Vector3 _eyeWorldPos;
    private Vector3 _cursorWorldPos;
    private Vector3 _plateCenter;
    private Vector3 _planeRight;
    private Vector3 _planeUp;
    private Vector3 _eyeToPlate;
    private bool    _hasValidFrame;

    protected override void OnUpdate()
    {
        if ( !PlateRoot.IsValid() ) return;

        _eyeWorldPos  = PlateRoot.WorldTransform.PointToWorld( EyeLocalOffset );
        _plateCenter  = PlateRoot.WorldPosition;
        _eyeToPlate   = (_plateCenter - _eyeWorldPos).Normal;

        Vector3 refUp = MathF.Abs( Vector3.Dot( _eyeToPlate, Vector3.Up ) ) < 0.98f
            ? Vector3.Up
            : Vector3.Forward;

        _planeRight = Vector3.Cross( _eyeToPlate, refUp ).Normal;
        _planeUp    = Vector3.Cross( _planeRight, _eyeToPlate ).Normal;

        float nx =  (Mouse.Position.x / Screen.Width  - 0.5f) * ScrubPlaneSize;
        float ny = -(Mouse.Position.y / Screen.Height - 0.5f) * ScrubPlaneSize;

        _cursorWorldPos = _plateCenter + _planeRight * nx + _planeUp * ny;
        _hasValidFrame  = true;

        var ray = new Ray( _eyeWorldPos, (_cursorWorldPos - _eyeWorldPos).Normal );
        _lastTrace = Scene.Trace.Ray( ray, ReachDistance ).UsePhysicsWorld().Run();

        if ( Input.Down( "attack1" ) && _lastTrace.Hit )
        {
            var mask = _lastTrace.GameObject.Components.Get<DishwasherDynamicMask>();
            if ( mask.IsValid() )
                mask.CleanAtWorldLocation( _lastTrace );
        }

        if ( ShowDebug ) DrawDebugVisuals();
    }

    private void DrawDebugVisuals()
    {
        if ( !_hasValidFrame ) return;

        using ( Gizmo.Scope() )
        {
            float half = ScrubPlaneSize / 2f;

            // ── 1. Eye local-offset line (plate anchor → eye) ─────────────
            // Dashed white line so you can see the offset vector in the editor
            Gizmo.Draw.Color = Color.White.WithAlpha( 0.4f );
            Gizmo.Draw.LineThickness = 1f;
            Gizmo.Draw.Line( _plateCenter, _eyeWorldPos );

            // ── 2. Eye sphere + label ─────────────────────────────────────
            Gizmo.Draw.Color = Color.Cyan;
            Gizmo.Draw.LineThickness = 1f;
            Gizmo.Draw.SolidSphere( _eyeWorldPos, 3f );
            Gizmo.Draw.Text( "Eye", new Transform( _eyeWorldPos + Vector3.Up * 8f ) );

            // ── 3. Eye-to-plate direction arrow ───────────────────────────
            // Shows which way the eye is "looking"
            Vector3 arrowTip = _eyeWorldPos + _eyeToPlate * 30f;
            Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.6f );
            Gizmo.Draw.Line( _eyeWorldPos, arrowTip );
            Gizmo.Draw.SolidSphere( arrowTip, 1.5f );

            // ── 4. Scrub plane (wireframe quad + axis lines) ──────────────
            Vector3 bl = _plateCenter - _planeRight * half - _planeUp * half;
            Vector3 br = _plateCenter + _planeRight * half - _planeUp * half;
            Vector3 tr = _plateCenter + _planeRight * half + _planeUp * half;
            Vector3 tl = _plateCenter - _planeRight * half + _planeUp * half;

            Gizmo.Draw.Color = Color.Green.WithAlpha( 0.35f );
            Gizmo.Draw.LineThickness = 1f;
            Gizmo.Draw.Line( bl, br );
            Gizmo.Draw.Line( br, tr );
            Gizmo.Draw.Line( tr, tl );
            Gizmo.Draw.Line( tl, bl );

            // Centre cross (shows the plate anchor on the plane)
            Gizmo.Draw.Color = Color.Green.WithAlpha( 0.2f );
            Gizmo.Draw.Line( _plateCenter - _planeRight * half, _plateCenter + _planeRight * half );
            Gizmo.Draw.Line( _plateCenter - _planeUp    * half, _plateCenter + _planeUp    * half );

            // Plane normal arrow (so you can see it's perpendicular to the eye direction)
            Gizmo.Draw.Color = Color.Green.WithAlpha( 0.5f );
            Gizmo.Draw.Line( _plateCenter, _plateCenter + _eyeToPlate * 20f );
            Gizmo.Draw.Text( "Scrub plane", new Transform( tl + _planeUp * 4f ) );

            // ── 5. Cursor on scrub plane ──────────────────────────────────
            // Small crosshair + sphere so you see exactly where the mouse maps to
            Gizmo.Draw.Color = Color.Orange;
            Gizmo.Draw.LineThickness = 1.5f;
            float ch = 5f; // crosshair arm length
            Gizmo.Draw.Line( _cursorWorldPos - _planeRight * ch, _cursorWorldPos + _planeRight * ch );
            Gizmo.Draw.Line( _cursorWorldPos - _planeUp    * ch, _cursorWorldPos + _planeUp    * ch );
            Gizmo.Draw.SolidSphere( _cursorWorldPos, 1.5f );

            // ── 6. Ray from eye through cursor ────────────────────────────
            Gizmo.Draw.Color = _lastTrace.Hit ? Color.Green : Color.Red;
            Gizmo.Draw.LineThickness = 2f;

            // Draw in two segments so the gap at the scrub plane is clear:
            // eye → cursor (thin), then cursor → hit/miss (bold)
            Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
            Gizmo.Draw.LineThickness = 1f;
            Gizmo.Draw.Line( _eyeWorldPos, _cursorWorldPos );

            Vector3 rayEnd = _lastTrace.Hit ? _lastTrace.HitPosition : _cursorWorldPos +
                (_cursorWorldPos - _eyeWorldPos).Normal * ReachDistance;

            Gizmo.Draw.Color = _lastTrace.Hit ? Color.Green : Color.Red;
            Gizmo.Draw.LineThickness = 2f;
            Gizmo.Draw.Line( _cursorWorldPos, rayEnd );

            // ── 7. Hit-point indicators ───────────────────────────────────
            if ( _lastTrace.Hit )
            {
                var mask = _lastTrace.GameObject?.Components.Get<DishwasherDynamicMask>();

                if ( mask.IsValid() )
                {
                    // Yellow = hit a cleanable surface
                    Gizmo.Draw.Color = Color.Yellow;
                    Gizmo.Draw.SolidSphere( _lastTrace.HitPosition, 2.5f );

                    if ( Input.Down( "attack1" ) )
                    {
                        Gizmo.Draw.Text( "SCRUBBING",
                            new Transform( _lastTrace.HitPosition + Vector3.Up * 10f ) );
                    }
                    else
                    {
                        Gizmo.Draw.Text( $"Hit: {_lastTrace.GameObject.Name}",
                            new Transform( _lastTrace.HitPosition + Vector3.Up * 10f ) );
                    }
                }
                else
                {
                    // White = hit something, but not a dish
                    Gizmo.Draw.Color = Color.White.WithAlpha( 0.5f );
                    Gizmo.Draw.SolidSphere( _lastTrace.HitPosition, 1.5f );
                }
            }
        }
    }
}
