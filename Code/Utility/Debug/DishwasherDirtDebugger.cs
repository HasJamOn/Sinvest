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
	[Property] public bool LiveUpdate { get; set; } = true;

	/// <summary>
	/// Click this button in the inspector to run a full diagnostic in the console.
	/// </summary>
	[Button( "Run Visibility Diagnostic" )]
	public void RunDiagnostic()
	{
		if ( Target is null ) return;

		Log.Info( "--- Starting Decal Visibility Diagnostic ---" );

		var decals = Target.GameObject.Children.Where( x => x.Name.Contains( "Dirt" ) ).ToList();
		
		if ( !decals.Any() )
		{
			Log.Warning( "No decals found under the Target. Check if SpawnDirt() was called." );
			return;
		}

		foreach ( var go in decals )
		{
			var decalComp = go.Components.Get<Decal>();
			
			// Check 1: Component Existence
			if ( decalComp is null )
			{
				Log.Error( $"Decal Object {go.Id} is missing the Decal Component!" );
				continue;
			}

			// Check 2: Asset Assignment
			if ( decalComp.Decals == null || decalComp.Decals.Count == 0 )
			{
				Log.Error( $"Decal Object {go.Id} has no DecalDefinition assigned." );
			}

			// Check 3: Depth vs Surface (Trace Test)
			// Trace from the decal back toward its projection source to see if it hits the plate
			var trace = Scene.Trace.Ray( go.WorldPosition, go.WorldPosition + go.WorldTransform.Forward * Target.DecalDepth )
				.IgnoreGameObject( go )
				.Run();

			if ( !trace.Hit )
			{
				Log.Warning( $"Decal {go.Id}: Projection volume does NOT intersect any geometry. Try increasing DecalDepth or moving the Spawn offset." );
			}
			else
			{
				Log.Info( $"Decal {go.Id}: Successfully hitting {trace.GameObject.Name} at distance {trace.Distance:F2}" );
			}
		}

		Log.Info( "--- Diagnostic Complete ---" );
	}

	private float _lastSize;
	private float _lastDepth;

	protected override void OnUpdate()
	{
		if ( Target is null || !Target.IsValid ) return;

		if ( LiveUpdate && (Target.DecalSize != _lastSize || Target.DecalDepth != _lastDepth) )
		{
			Target.ApplyChanges();
			_lastSize = Target.DecalSize;
			_lastDepth = Target.DecalDepth;
		}

		// Draw logic remains for visual confirmation
		if ( ShowProjectionVolumes )
		{
			foreach ( var go in Target.GameObject.Children )
			{
				if ( !go.IsValid || !go.Name.Contains( "Dirt" ) ) continue;

				using ( Gizmo.Scope( go.Name, go.WorldTransform ) )
				{
					Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.2f );
					var size = new Vector3( Target.DecalDepth, Target.DecalSize, Target.DecalSize );
					var center = new Vector3( Target.DecalDepth * 0.5f, 0, 0 );
					Gizmo.Draw.LineBBox( new BBox( center - (size * 0.5f), center + (size * 0.5f) ) );
				}
			}
		}
	}
}
