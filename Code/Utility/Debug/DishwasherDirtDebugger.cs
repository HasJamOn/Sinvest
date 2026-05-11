using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

[Title( "Dishwasher Debugger" )]
[Category( "Sinvest/Debug" )]
public sealed class DishwasherDirtDebugger : Component
{
    [Property] public DishwasherDirtManager Target { get; set; }
    [Property] public bool ShowProjectionVolumes { get; set; } = true;

    // We keep internal tracking for the diagnostic UI
    private float _lastSize = 0.5f;
    private float _lastOffset = 0.05f;

    [Button( "Run Visibility Diagnostic" )]
    public void RunDiagnostic()
    {
       if ( Target is null ) return;

       Log.Info( "--- Starting Decal Visibility Diagnostic ---" );
       
       // Accessing children named "Dirt" as defined in your Manager
       var decals = Target.GameObject.Children.Where( x => x.Name.Contains( "Dirt" ) ).ToList();
       
       if ( !decals.Any() )
       {
          Log.Warning( "No decals found. Ensure you have called SpawnDirt() in the manager." );
          return;
       }

       foreach ( var go in decals )
       {
          if ( !go.Components.TryGet<Decal>( out var decalComp ) ) continue;

          // Your manager doesn't expose the calculated depth, so we fetch it from the component
          float currentDepth = decalComp.Depth;

          var trace = Scene.Trace.Ray( go.WorldPosition, go.WorldPosition + go.WorldTransform.Forward * currentDepth )
             .IgnoreGameObject( go )
             .Run();

          if ( !trace.Hit )
             Log.Warning( $"Decal {go.Id}: Missed geometry. Projection Depth: {currentDepth}" );
          else
             Log.Info( $"Decal {go.Id}: Hitting {trace.GameObject.Name} at {trace.Distance:F2} units." );
       }
    }

    protected override void OnUpdate()
    {
       if ( Target is null || !Target.IsValid ) return;

       // ShowProjectionVolumes logic mapped to existing Manager properties
       if ( ShowProjectionVolumes )
       {
          foreach ( var go in Target.GameObject.Children )
          {
             if ( !go.IsValid || !go.Name.Contains( "Dirt" ) ) continue;
             if ( !go.Components.TryGet<Decal>( out var decal ) ) continue;

             using ( Gizmo.Scope( go.Id.ToString(), go.WorldTransform ) )
             {
                Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.2f );
                
                // s&box Decals project along the X (Forward) axis.
                // We pull the Depth and Size directly from the spawned component.
                var size = new Vector3( decal.Depth, Target.DecalSize, Target.DecalSize );
                var center = new Vector3( decal.Depth * 0.5f, 0, 0 );
                
                Gizmo.Draw.LineBBox( new BBox( center - (size * 0.5f), center + (size * 0.5f) ) );
                
                // Draw a small line indicating the orientation of the projection
                Gizmo.Draw.Line( Vector3.Zero, Vector3.Forward * 0.2f );
             }
          }
       }
    }
}
