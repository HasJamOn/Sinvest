using Sandbox;
using System;
using System.Collections.Generic;

namespace Sinvest;

public sealed class DishwasherScrubber : Component
{
	[Property] public float ReachDistance { get; set; } = 500f;
	[Property] public float ScrubRadius { get; set; } = 40f;
	
	/// <summary>
	/// The SoundEvent to play while scrubbing (e.g., sounds/dishwasher/scrub.sound)
	/// </summary>
	[Property] public SoundEvent ScrubSound { get; set; }
	
	/// <summary>
	/// How often to play the scrub sound while moving the mouse.
	/// </summary>
	[Property] public float SoundInterval { get; set; } = 0.15f;

	private readonly HashSet<Guid> _hitSession = new();
	private SceneTraceResult _positionTrace;
	private TimeSince _lastSoundTime;

	protected override void OnUpdate()
	{
		var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );

		_positionTrace = Scene.Trace
			.Ray( ray, ReachDistance )
			.UsePhysicsWorld()
			.WithTag( "solid" )
			.Run();

		bool isFrontFace = _positionTrace.Hit &&
		                   Vector3.Dot( _positionTrace.Normal, ray.Forward ) < 0f;

		if ( isFrontFace && Input.Down( "attack1" ) )
		{
			var manager = _positionTrace.GameObject
				.Components.GetInAncestorsOrSelf<DishwasherDirtManager>();

			if ( manager.IsValid() )
			{
				// Only play sound if we are actually moving the mouse and the interval has passed
				if ( Mouse.Delta.Length > 0.1f && _lastSoundTime > SoundInterval )
				{
					PlayScrubSound();
					_lastSoundTime = 0;
				}

				manager.TryCleanAt( _positionTrace.HitPosition, ScrubRadius, _hitSession );
			}
		}

		if ( !Input.Down( "attack1" ) || !isFrontFace )
		{
			_hitSession.Clear();
		}
	}

	private void PlayScrubSound()
	{
		if ( ScrubSound is null ) return;

		// Play the sound at the hit position
		var snd = Sound.Play( ScrubSound, _positionTrace.HitPosition );
		
		// Optional: Tweak the pitch slightly for each play to make it feel more natural
		snd.Pitch = Game.Random.Float( 0.95f, 1.15f );
	}
}
