using Sandbox;
using System;
using System.Linq;

namespace Sandbox;

[Title( "Recall Rewind" )]
[Category( "Physics" )]
[Icon( "settings_backup_restore" )]
public sealed class RecallRewind : Component, Component.ICollisionListener
{
	[Property, Group( "Timings" )] public float WaitBeforeRewind { get; set; } = 5.0f;
	[Property, Group( "Timings" )] public float MaxRewindDuration { get; set; } = 3.5f;

	[Property, Group( "Leash" )] public GameObject LeashObject { get; set; }

	[Property, Group( "Leash" ), ShowIf( nameof( LeashObject ), null )] 
	public Vector3 LeashPosition { get; set; }
	
	[Property, Group( "Leash" ), ShowIf( nameof( LeashObject ), null )] 
	public Rotation LeashRotation { get; set; } = Rotation.Identity;
	
	[Property, Group( "Leash" )]
	public Vector3 BoxSize { get; set; } = 100f;

	private Transform _originTransform;
	private Rigidbody _rb;
	private float _timer;
	private bool _isRewinding;
	private bool _isAwake;
	private float _rewindStartedTime;

	private Vector3 GetEffectiveLeashPos() => LeashObject.IsValid() ? LeashObject.WorldPosition : LeashPosition;

	private Rotation GetEffectiveLeashRot()
	{
		if ( LeashObject.IsValid() ) return LeashObject.WorldRotation;
		return WorldRotation.Inverse * LeashRotation;
	}

	private BBox GetLeashBounds()
	{
		return new BBox( 
			new Vector3( -BoxSize.x * 0.5f, -BoxSize.y * 0.5f, 0 ), 
			new Vector3( BoxSize.x * 0.5f, BoxSize.y * 0.5f, BoxSize.z ) 
		);
	}

	protected override void OnStart()
	{
		_originTransform = WorldTransform;
		_rb = Components.Get<Rigidbody>();
		_timer = WaitBeforeRewind;

		if ( !LeashObject.IsValid() && LeashPosition == Vector3.Zero )
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

		Vector3 relativePos = WorldPosition - GetEffectiveLeashPos();
		Vector3 localPos = GetEffectiveLeashRot().Inverse * relativePos;
		BBox localCheck = GetLeashBounds();

		if ( !localCheck.Contains( localPos ) )
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

		GameObject.Tags.Add( "rewinding" );
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

		if ( elapsed >= MaxRewindDuration )
		{
			FinishRewind( true );
			return;
		}

		float dist = WorldPosition.Distance( _originTransform.Position );

		if ( dist > 5.0f )
		{
			Vector3 dir = (_originTransform.Position - WorldPosition).Normal;
			if ( _rb.IsValid() ) _rb.Velocity = dir * 250.0f;
			WorldRotation = Rotation.Lerp( WorldRotation, _originTransform.Rotation, Time.Delta * 5.0f );
		}
		else
		{
			FinishRecallSuccess();
		}
	}

	private void FinishRecallSuccess()
	{
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

		Vector3 center = GetEffectiveLeashPos();
		Rotation rot = GetEffectiveLeashRot();

		using ( Gizmo.Scope( "leash_volume", new Transform( center, rot ) ) )
		{
			BBox localBounds = GetLeashBounds();
			Gizmo.Draw.Color = _isAwake ? Color.Orange : Color.Cyan.WithAlpha( 0.2f );
			Gizmo.Draw.SolidBox( localBounds );
			Gizmo.Draw.Color = _isAwake ? Color.Orange : Color.Cyan;
			Gizmo.Draw.LineBBox( localBounds );
		}

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
		Gizmo.Draw.Line( center, WorldPosition );
	}
}
