using Sandbox;

namespace Sinvest;

public sealed class DishwasherScrubber : Component
{
	[Property] public float ReachDistance { get; set; } = 500f;
	[Property] public float ScrubRadius { get; set; } = 15f;

	private SceneTraceResult _positionTrace;

	protected override void OnUpdate()
	{
		var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );

		// No longer need UseRenderMeshes — we don't need triangle/UV data at all
		_positionTrace = Scene.Trace
			.Ray( ray, ReachDistance )
			.UsePhysicsWorld()
			.Run();

		if ( _positionTrace.Hit && Input.Down( "attack1" ) )
		{
			if ( _positionTrace.GameObject.Components.TryGet<DishwasherDirtManager>( out var manager ) )
			{
				manager.TryCleanAt( _positionTrace.HitPosition, ScrubRadius );
			}
		}
	}
}
