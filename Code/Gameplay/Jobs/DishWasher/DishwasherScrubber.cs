using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

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
		// Find the active camera framing this specific workspace instead of defaulting to global player eyes
		var activeCam = Scene.GetAllComponents<CameraComponent>()
			.FirstOrDefault( c => c.Enabled && c.GameObject.WorldPosition.Distance( GameObject.WorldPosition ) < 1000f );

		// Fallback safely to Scene.Camera if no workspace context is found
		var cameraToUse = activeCam ?? Scene.Camera;

		var ray = cameraToUse.ScreenPixelToRay( Mouse.Position );

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

		// PLAY 2D/GLOBAL: Dropping the hit position parameter plays the sound 
		// natively inside the player's headphones without distance dropoff.
		var snd = Sound.Play( ScrubSound );
		
		// Tweak the pitch slightly for each play to make it feel more natural
		snd.Pitch = Game.Random.Float( 0.95f, 1.15f );
	}
}
