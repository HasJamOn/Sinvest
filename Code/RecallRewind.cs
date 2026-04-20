using Sandbox;
using System;

namespace Sandbox;

[Title( "Recall Rewind" )]
[Category( "Physics" )]
[Icon( "settings_backup_restore" )]
public sealed class RecallRewind : Component, Component.ICollisionListener
{
	[Property, Group( "Timings" )] public float WaitBeforeRewind { get; set; } = 5.0f;
	[Property, Group( "Timings" )] public float RewindFlightTime { get; set; } = 2.0f;
	[Property, Group( "Timings" )] public float MaxRewindDuration { get; set; } = 3.5f;

	[Property, Group( "Leash" )] public Vector3 LeashPosition { get; set; }
	[Property, Group( "Leash" )] public Vector3 BoxSize { get; set; } = new Vector3( 200, 200, 200 );
	[Property, Group( "Leash" )] public bool UseObjectStartAsLeashCenter { get; set; } = true;

	private Transform _originTransform;
	private Rigidbody _rb;
	private float _timer;
	private bool _isRewinding;
	private bool _isAwake;
	private float _rewindStartedTime;

	protected override void OnStart()
	{
		_originTransform = WorldTransform;
		_rb = Components.Get<Rigidbody>();
		_timer = WaitBeforeRewind;

		if ( UseObjectStartAsLeashCenter && LeashPosition == Vector3.Zero )
		{
			LeashPosition = WorldPosition;
		}

		if ( _rb.IsValid() )
		{
			_rb.PhysicsBody.GravityEnabled = false;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !_isAwake ) return;

		if ( _isRewinding )
		{
			ProcessRewind();
			return;
		}

		var leashBounds = BBox.FromPositionAndSize( LeashPosition, BoxSize );
		if ( !leashBounds.Contains( WorldPosition ) )
		{
			_timer -= Time.Delta;
			if ( _timer <= 0 ) StartRecall();
		}
		else
		{
			_timer = WaitBeforeRewind;
		}
	}

	public void OnCollisionStart( Collision other )
	{
		if ( _isAwake || _isRewinding ) return;

		// Wake up when touched
		_isAwake = true;
		if ( _rb.IsValid() )
		{
			_rb.PhysicsBody.GravityEnabled = true;
		}
	}

	private void StartRecall()
	{
		_isRewinding = true;
		_rewindStartedTime = Time.Now;

		// This is the tag you created in Project Settings
		GameObject.Tags.Add( "rewinding" );
		// We remove 'solid' briefly to reset interactions, 
		// but the physics engine handles the actual wall-blocking via the Tag Matrix
		GameObject.Tags.Remove( "solid" ); 
		GameObject.Tags.Add( "solid" ); 

		if ( _rb.IsValid() )
		{
			_rb.PhysicsBody.GravityEnabled = false;
			_rb.PhysicsBody.AutoSleep = false;
		}
	}

	private void ProcessRewind()
	{
		float elapsed = Time.Now - _rewindStartedTime;

		// TELEPORT FALLBACK
		if ( elapsed >= MaxRewindDuration )
		{
			FinishRewind( true );
			return;
		}

		float dist = WorldPosition.Distance( _originTransform.Position );

		if ( dist > 5.0f )
		{
			// PHYSICALLY PULL HOME
			// Using velocity makes the object stop at walls naturally
			Vector3 dir = (_originTransform.Position - WorldPosition).Normal;
			float pullSpeed = 250.0f; 
			
			_rb.Velocity = dir * pullSpeed;
			WorldRotation = Rotation.Lerp( WorldRotation, _originTransform.Rotation, Time.Delta * 5.0f );
		}
		else
		{
			FinishRecallSuccess();
		}
	}

	private void FinishRecallSuccess()
	{
		// Snap to exact original spot for perfection
		WorldTransform = _originTransform;
		FinishRewind( false );
	}

	private void FinishRewind( bool teleport )
	{
		if ( teleport ) WorldTransform = _originTransform;

		_isRewinding = false;
		_isAwake = false;
		_timer = WaitBeforeRewind;

		GameObject.Tags.Remove( "rewinding" );

		if ( _rb.IsValid() )
		{
			_rb.Velocity = Vector3.Zero;
			_rb.AngularVelocity = Vector3.Zero;
			_rb.PhysicsBody.GravityEnabled = false;
		}
	}

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected ) return;
		var bounds = BBox.FromPositionAndSize( LeashPosition, BoxSize );
		Gizmo.Draw.Color = _isAwake ? Color.Orange : Color.Cyan.WithAlpha( 0.2f );
		Gizmo.Draw.SolidBox( bounds );
		Gizmo.Draw.LineBBox( bounds );
	}
}
