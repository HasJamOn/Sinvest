using Sandbox; 
using Sinvest;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

[Title( "Recall Rewind" )]
[Category( "Physics" )]
[Icon( "settings_backup_restore" )]
public sealed class RecallRewind : Component, Component.ICollisionListener
{
	[Property, Group( "Timings" )] public float WaitBeforeRewind { get; set; } = 5.0f;
	[Property, Group( "Timings" )] public float MaxRewindDuration { get; set; } = 3.5f;

	[Property, Group( "Setup" )] public RecallArea Area { get; set; }
	[Property, Group( "Setup" )] public float GhostRadius { get; set; } = 50.0f;

	private struct TransformSnapshot
	{
		public GameObject Obj;
		public Vector3 LocalPosition;
		public Rotation LocalRotation;
		public Rigidbody Rb;
		public Collider Col;
	}

	private List<TransformSnapshot> _snapshots = new();
	private Rigidbody _mainRb;
	private float _timer;
	private bool _isRewinding;
	private bool _isAwake;
	private float _rewindStartedTime;
	private Vector3 _worldOriginPos;
	private Rotation _worldOriginRot;

	protected override void OnStart()
	{
		_mainRb = Components.Get<Rigidbody>();
		_timer = WaitBeforeRewind;

		_worldOriginPos = WorldPosition;
		_worldOriginRot = WorldRotation;

		RecordHierarchy();
		
		// Apply the family tag immediately so they never hit each other
		ApplyFamilyTags();
		
		// Start frozen
		SetPhysicsState( false );
	}

	private void RecordHierarchy()
	{
		_snapshots.Clear();
		var descendants = GameObject.GetAllObjects( true );
		foreach ( var obj in descendants )
		{
			_snapshots.Add( new TransformSnapshot
			{
				Obj = obj,
				LocalPosition = obj.LocalPosition,
				LocalRotation = obj.LocalRotation,
				Rb = obj.Components.Get<Rigidbody>(),
				Col = obj.Components.Get<Collider>()
			} );
		}
	}

	private void ApplyFamilyTags()
	{
		foreach ( var s in _snapshots )
		{
			if ( !s.Obj.IsValid() ) continue;
			// By adding this tag to everyone, and setting the project to ignore 
			// recall_family vs recall_family, they will never collide with each other.
			s.Obj.Tags.Add( "recall_family" );
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

		if ( !Area.IsValid() ) return;

		if ( !Area.GetWorldBounds().Contains( WorldPosition ) )
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
		SetPhysicsState( true );
	}

	private void StartRecall()
	{
		_isRewinding = true;
		_rewindStartedTime = Time.Now;
		SetPhysicsState( false );
	}

	private void ProcessRewind()
	{
		float elapsed = Time.Now - _rewindStartedTime;
		float t = elapsed / MaxRewindDuration;
		float distToHome = WorldPosition.Distance( _worldOriginPos );

		if ( t >= 1.0f || distToHome < 1.0f )
		{
			FinishRewind();
			return;
		}

		// FEATHERED AREA LOGIC:
		// If we are within the GhostRadius of our home, disable colliders 
		// so we don't bounce off the floor or nearby objects while snapping into place.
		bool shouldBeGhost = distToHome < GhostRadius;
		UpdateGhostState( shouldBeGhost );

		// Move Root
		WorldPosition = Vector3.Lerp( WorldPosition, _worldOriginPos, Time.Delta * 5.0f );
		WorldRotation = Rotation.Lerp( WorldRotation, _worldOriginRot, Time.Delta * 5.0f );

		// Reassemble Children
		foreach ( var snapshot in _snapshots )
		{
			if ( !snapshot.Obj.IsValid() || snapshot.Obj == GameObject ) continue;
			snapshot.Obj.LocalPosition = Vector3.Lerp( snapshot.Obj.LocalPosition, snapshot.LocalPosition, Time.Delta * 8.0f );
			snapshot.Obj.LocalRotation = Rotation.Lerp( snapshot.Obj.LocalRotation, snapshot.LocalRotation, Time.Delta * 8.0f );
		}
	}

	private void UpdateGhostState( bool ghost )
	{
		foreach ( var s in _snapshots )
		{
			if ( s.Col.IsValid() ) s.Col.Enabled = !ghost;
		}
	}

	private void FinishRewind()
	{
		WorldPosition = _worldOriginPos;
		WorldRotation = _worldOriginRot;

		foreach ( var snapshot in _snapshots )
		{
			if ( !snapshot.Obj.IsValid() ) continue;
			snapshot.Obj.LocalPosition = snapshot.LocalPosition;
			snapshot.Obj.LocalRotation = snapshot.LocalRotation;
		}

		_isRewinding = false;
		_isAwake = false;
		_timer = WaitBeforeRewind;
		
		// Ensure colliders come back on so we can be pushed again
		UpdateGhostState( false );
		SetPhysicsState( false );
	}

	private void SetPhysicsState( bool active )
	{
		foreach ( var snapshot in _snapshots )
		{
			if ( !snapshot.Rb.IsValid() ) continue;
			
			snapshot.Rb.MotionEnabled = active;
			snapshot.Rb.PhysicsBody.GravityEnabled = active;
			
			if ( !active )
			{
				snapshot.Rb.Velocity = Vector3.Zero;
				snapshot.Rb.AngularVelocity = Vector3.Zero;
				snapshot.Rb.PhysicsBody.ClearForces();
			}
		}
	}
	
}
