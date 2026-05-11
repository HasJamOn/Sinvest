using Sandbox;
using System;
using System.Collections.Generic;

namespace Sinvest;

public sealed class DishwasherScrubber : Component
{
	[Property] public float ReachDistance { get; set; } = 500f;
	[Property] public float ScrubRadius { get; set; } = 40f;

	private readonly HashSet<Guid> _hitSession = new();
	private SceneTraceResult _positionTrace;

	protected override void OnUpdate()
	{
		var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );

		_positionTrace = Scene.Trace
			.Ray( ray, ReachDistance )
			.UsePhysicsWorld()
			.WithTag( "solid" )
			.Run();

		// Use ray.Forward here
		bool isFrontFace = _positionTrace.Hit &&
		                   Vector3.Dot( _positionTrace.Normal, ray.Forward ) < 0f;

		if ( isFrontFace && Input.Down( "attack1" ) )
		{
			var manager = _positionTrace.GameObject
				.Components.GetInAncestorsOrSelf<DishwasherDirtManager>();

			if ( manager.IsValid() )
			{
				manager.TryCleanAt( _positionTrace.HitPosition, ScrubRadius, _hitSession );
			}
		}

		if ( !Input.Down( "attack1" ) || !isFrontFace )
		{
			_hitSession.Clear();
		}
	}
}
