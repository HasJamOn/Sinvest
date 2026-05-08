using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class OilPumperMatchmaker : Component, Component.ITriggerListener, Component.ICollisionListener
{
    [Property] public PumperSeat SeatA { get; set; }
    [Property] public PumperSeat SeatB { get; set; }
    [Property] public Rigidbody Body { get; set; }

    [Sync] public float BonusPool { get; set; } = 0;
    
    public enum GameState { Waiting, Choosing, Countdown, Playing }
    
    [Sync] public GameState CurrentState { get; set; } = GameState.Waiting;
    [Sync] public float CountdownTime { get; set; } = 3.0f;

    [Sync] public int PlayerAChoice { get; set; } = 0;
    [Sync] public int PlayerBChoice { get; set; } = 0;

    // --- TILT PROPERTIES ---
    [Sync] public float SeesawTilt { get; set; } = -1.0f; 
    [Sync] public bool IsTurnA { get; set; } = true;      
    
    [Property] public float MaxTiltAngle { get; set; } = 20.0f;
    [Property] public float TiltSmoothness { get; set; } = 10.0f;

    protected override void OnUpdate()
    {
        UpdateVisuals();

        if ( IsProxy ) return;
        UpdateLogic();
    }

    private void UpdateVisuals()
    {
        if ( !Body.IsValid() ) return;

        bool isActive = CurrentState == GameState.Playing || CurrentState == GameState.Countdown;
        float targetPitch = isActive ? (-SeesawTilt * MaxTiltAngle) : 0f;

        var currentAngles = Body.LocalRotation.Angles();
    
        // FORCE Pitch to our game logic, FORCE Roll to 0, KEEP current Yaw from physics
        var forcedAngles = new Angles( targetPitch, currentAngles.yaw, 0 );

        Body.LocalRotation = Rotation.Lerp( 
           Body.LocalRotation, 
           forcedAngles.ToRotation(), 
           Time.Delta * TiltSmoothness 
        );

        // Keep the Rigidbody upright by killing Pitch/Roll velocity, 
        // but preserve Z velocity so it can still spin.
        Body.AngularVelocity = Body.AngularVelocity.WithX( 0 ).WithY( 0 );
    }

    /// <summary>
    /// Physics callback to filter who can spin the plank
    /// </summary>
    void ICollisionListener.OnCollisionStart( Collision collision )
    {
        FilterOccupantPhysics( collision.Other.GameObject );
    }

    void ICollisionListener.OnCollisionUpdate( Collision collision )
    {
        FilterOccupantPhysics( collision.Other.GameObject );
    }

    private void FilterOccupantPhysics( GameObject other )
    {
        if ( !Body.IsValid() ) return;

        // Check if the object touching the plank is one of the seated players
        bool isOccupantA = SeatA.IsValid() && SeatA.IsOccupied && SeatA.Occupant == other;
        bool isOccupantB = SeatB.IsValid() && SeatB.IsOccupied && SeatB.Occupant == other;

        if ( isOccupantA || isOccupantB )
        {
            // If they are seated, kill the Yaw velocity they are trying to apply
            Body.AngularVelocity = Body.AngularVelocity.WithZ( 0 );
        }
    }

    private void UpdateLogic()
    {
	    bool bothSeated = SeatA.IsValid() && SeatA.IsOccupied && 
	                      SeatB.IsValid() && SeatB.IsOccupied;

	    // Transition: Waiting -> Choosing
	    if ( CurrentState == GameState.Waiting && bothSeated )
	    {
		    CurrentState = GameState.Choosing;
	    }
	    // Transition: Choosing -> Countdown (This is where we resolve money!)
	    else if ( CurrentState == GameState.Choosing && PlayerAChoice != 0 && PlayerBChoice != 0 )
	    {
		    ResolveChoices();
		    CurrentState = GameState.Countdown;
		    CountdownTime = 3.0f;
		    SeesawTilt = -1.0f;
		    IsTurnA = true;
	    }
	    else if ( CurrentState == GameState.Countdown )
	    {
		    CountdownTime -= Time.Delta;
		    if ( CountdownTime <= 0 ) CurrentState = GameState.Playing;
	    }

	    // Reset if someone leaves
	    if ( !bothSeated && CurrentState != GameState.Waiting )
	    {
		    CurrentState = GameState.Waiting;
		    PlayerAChoice = 0;
		    PlayerBChoice = 0;
		    SeesawTilt = 0;
		    BonusPool = 1000f; // Reset pool for next round
	    }
    }
    
    private void ResolveChoices()
    {
	    if ( !Networking.IsHost ) return;

	    float total = BonusPool;
	    float payoutA = 0;
	    float payoutB = 0;

	    // Prisoner's Dilemma Logic
	    if ( PlayerAChoice == 1 && PlayerBChoice == 1 ) { payoutA = total / 2f; payoutB = total / 2f; } // Share/Share
	    else if ( PlayerAChoice == 1 && PlayerBChoice == 2 ) { payoutB = total; }                       // A Shares, B Steals
	    else if ( PlayerAChoice == 2 && PlayerBChoice == 1 ) { payoutA = total; }                       // A Steals, B Shares
	    // Steal/Steal results in 0 for both.

	    if ( payoutA > 0 ) IssuePayout( SeatA, payoutA );
	    if ( payoutB > 0 ) IssuePayout( SeatB, payoutB );

	    BonusPool = 0;
    }

    private void IssuePayout( PumperSeat seat, float amount )
    {
	    if ( !seat.IsValid() || !seat.IsOccupied ) return;
	    var targetConn = seat.Occupant.Network.Owner;
	    if ( targetConn == null ) return;

	    // Target ONLY the person receiving the money
	    using ( Rpc.FilterInclude( targetConn ) )
	    {
		    ClientReceivePayout( amount );
	    }
    }

    [Rpc.Broadcast]
    private void ClientReceivePayout( float amount )
    {
	    // 1. Update the local EconomyManager using the Labor Income path
	    if ( EconomyManager.Instance.IsValid() )
	    {
		    // This correctly hits GameSaveSystem.Instance.CommitMoneyTransaction( "IN", ... )
		    EconomyManager.Instance.AddLaborIncome( (double)amount, "Oil Pumper Payout" );
	    }

	    // 2. Trigger the floating +$1000 (or amount) on the UI Screen
	    var screen = Scene.GetAllComponents<OilPumperScreenpanel>().FirstOrDefault();
	    if ( screen.IsValid() )
	    {
		    screen.TriggerPayoutEffect( amount );
	    }
    }

    [Rpc.Broadcast]
    public void ExecuteJump( bool fromSeatA )
    {
	    if ( CurrentState != GameState.Playing ) return;
	    if ( fromSeatA != IsTurnA ) return;

	    // We change the state for EVERYONE so the Lerp in UpdateVisuals picks it up instantly
	    SeesawTilt = fromSeatA ? 1.0f : -1.0f;
	    IsTurnA = !IsTurnA;

	    // Only the Host handles the actual money/logic growth
	    if ( !IsProxy )
	    {
		    BonusPool += 25.0f; 
		    Log.Info( $"Matchmaker: {(fromSeatA ? "Player A" : "Player B")} Jumped!" );
	    }
    }

    [Rpc.Broadcast]
    public void MakeChoice( bool isSeatA, int choice )
    {
        var seat = isSeatA ? SeatA : SeatB;
        if ( !seat.IsValid() || !seat.IsOccupied ) return;
        
        var occupantOwnerId = seat.Occupant.Network.OwnerId;

        if ( Rpc.Caller.Id == occupantOwnerId || (occupantOwnerId == Guid.Empty && Networking.IsHost) )
        {
            if ( isSeatA ) PlayerAChoice = choice;
            else PlayerBChoice = choice;
        }
    }
}
