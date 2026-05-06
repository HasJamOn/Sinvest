using Sandbox; 
using Sinvest;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

/// <summary>
/// A physics utility component that monitors an object's position relative to a designated area.
/// If the object remains outside the area for a set duration, it "rewinds" its hierarchy 
/// back to its original start-up transform.
/// </summary>
[Title( "Recall Rewind" )]
[Category( "Physics" )]
[Icon( "settings_backup_restore" )]
public sealed class RecallRewind : Component, Component.ICollisionListener
{
    [Property, Group( "Timings" )] public float WaitBeforeRewind { get; set; } = 5.0f;
    [Property, Group( "Timings" )] public float MaxRewindDuration { get; set; } = 3.5f;

    [Property, Group( "Setup" )] public RecallArea Area { get; set; }
    [Property, Group( "Setup" )] public float GhostRadius { get; set; } = 50.0f;

    /// <summary>
    /// Captures the initial state of every object in the hierarchy for restoration.
    /// </summary>
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

       // Store the home position and rotation in world space
       _worldOriginPos = WorldPosition;
       _worldOriginRot = WorldRotation;

       // Cache all child objects and their relative offsets
       RecordHierarchy();
       
       // Apply tags to ensure children don't collide with each other during physics or rewind
       ApplyFamilyTags();
       
       // Initial state: Frozen until an external force (collision) wakes it up
       SetPhysicsState( false );
    }

    /// <summary>
    /// Recursively crawls the GameObject hierarchy to populate the snapshot list.
    /// </summary>
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

    /// <summary>
    /// Tags all objects in the recall group to allow for project-level collision filtering.
    /// </summary>
    private void ApplyFamilyTags()
    {
       foreach ( var s in _snapshots )
       {
          if ( !s.Obj.IsValid() ) continue;
          s.Obj.Tags.Add( "recall_family" );
       }
    }

    protected override void OnFixedUpdate()
    {
       // If the object hasn't been hit/moved yet, do nothing
       if ( !_isAwake ) return;

       if ( _isRewinding )
       {
          ProcessRewind();
          return;
       }

       if ( !Area.IsValid() ) return;

       // Check if the object has left its designated safe zone
       if ( !Area.GetWorldBounds().Contains( WorldPosition ) )
       {
          _timer -= Time.Delta;
          if ( _timer <= 0 ) StartRecall();
       }
       else
       {
          // Reset timer if it moves back into the area
          _timer = WaitBeforeRewind;
       }
    }

    /// <summary>
    /// Wakes up the physics system when something strikes the object.
    /// </summary>
    public void OnCollisionStart( Collision other )
    {
       if ( _isAwake || _isRewinding ) return;

       _isAwake = true;
       SetPhysicsState( true );
    }

    /// <summary>
    /// Begins the interpolation process back to the origin.
    /// </summary>
    private void StartRecall()
    {
       _isRewinding = true;
       _rewindStartedTime = Time.Now;
       // Freeze physics so the Lerp can control the transform without jitter
       SetPhysicsState( false );
    }

    /// <summary>
    /// Handles the visual and transform interpolation during the rewind phase.
    /// </summary>
    private void ProcessRewind()
    {
       float elapsed = Time.Now - _rewindStartedTime;
       float t = elapsed / MaxRewindDuration;
       float distToHome = WorldPosition.Distance( _worldOriginPos );

       // Safety break: end rewind if time expires or we are practically home
       if ( t >= 1.0f || distToHome < 1.0f )
       {
          FinishRewind();
          return;
       }

       // Ghosting: Disable collisions near home to prevent the object 
       // from getting snagged on the environment while snapping.
       bool shouldBeGhost = distToHome < GhostRadius;
       UpdateGhostState( shouldBeGhost );

       // Smoothly move the root object back to origin
       WorldPosition = Vector3.Lerp( WorldPosition, _worldOriginPos, Time.Delta * 5.0f );
       WorldRotation = Rotation.Lerp( WorldRotation, _worldOriginRot, Time.Delta * 5.0f );

       // Smoothly reassemble children to their starting local offsets
       foreach ( var snapshot in _snapshots )
       {
          if ( !snapshot.Obj.IsValid() || snapshot.Obj == GameObject ) continue;
          snapshot.Obj.LocalPosition = Vector3.Lerp( snapshot.Obj.LocalPosition, snapshot.LocalPosition, Time.Delta * 8.0f );
          snapshot.Obj.LocalRotation = Rotation.Lerp( snapshot.Obj.LocalRotation, snapshot.LocalRotation, Time.Delta * 8.0f );
       }
    }

    /// <summary>
    /// Toggles collider states across the entire hierarchy.
    /// </summary>
    private void UpdateGhostState( bool ghost )
    {
       foreach ( var s in _snapshots )
       {
          if ( s.Col.IsValid() ) s.Col.Enabled = !ghost;
       }
    }

    /// <summary>
    /// Hard-sets final transforms and resets logic flags.
    /// </summary>
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
       
       UpdateGhostState( false );
       SetPhysicsState( false );
    }

    /// <summary>
    /// Mass-toggles Rigidbody states. When inactive, velocities are stripped to ensure a dead stop.
    /// </summary>
    private void SetPhysicsState( bool active )
    {
       foreach ( var snapshot in _snapshots )
       {
          if ( !snapshot.Rb.IsValid() ) continue;
          
          snapshot.Rb.MotionEnabled = active;
          // PhysicsBody properties should be accessed for lower-level control like Gravity
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
