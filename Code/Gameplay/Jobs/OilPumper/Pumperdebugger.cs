using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

[Title( "Pumper Debugger" )]
[Category( "Sinvest/Debug" )]
public sealed class PumperDebugger : Component
{
    [Property] public OilPumperMatchmaker Matchmaker { get; set; }
    [Property] public GameObject ScreenObject { get; set; }

    private RealTimeSince TimeSinceLastJump;
    private string LastJumpOrigin = "NONE";
    private int JumpCount = 0;

    protected override void OnUpdate()
    {
        if ( !Matchmaker.IsValid() ) return;
        if ( !Scene.IsEditor && !Input.Down( "Menu" ) ) return; 

        DrawNetworkLines();
        DrawOccupancyStatus();
        DrawJumpMonitor();

        if ( ScreenObject.IsValid() )
        {
            DrawScreenStatus();
        }
    }

    private void DrawJumpMonitor()
    {
        var transform = Matchmaker.WorldTransform;
        var startPos = transform.Position + Vector3.Up * 60;
        bool justJumped = TimeSinceLastJump < 2.0f;
        Gizmo.Draw.Color = justJumped ? Color.Yellow : Color.Gray.WithAlpha(0.5f);

        string jumpInfo = $"--- JUMP MONITOR ---\nLast Jump: {LastJumpOrigin}\nTotal Jumps: {JumpCount}\nTurn: {(Matchmaker.IsTurnA ? "A" : "B")}";
        Gizmo.Draw.Text( jumpInfo, transform.WithPosition(startPos) );
    }

    [Button( "Simulate Jump (Current Turn)" )]
    public void SimulateJump()
    {
        if ( !Matchmaker.IsValid() ) return;
        Matchmaker.ExecuteJump( Matchmaker.IsTurnA );
        TrackJump( Matchmaker.IsTurnA ? "DEBUG_SIM_A" : "DEBUG_SIM_B" );
    }

    public void TrackJump( string origin )
    {
        TimeSinceLastJump = 0;
        LastJumpOrigin = origin;
        JumpCount++;
    }

    private void DrawNetworkLines()
    {
        Gizmo.Draw.Color = Color.Cyan;
        if ( Matchmaker.SeatA.IsValid() ) Gizmo.Draw.Line( Matchmaker.WorldPosition, Matchmaker.SeatA.WorldPosition );
        if ( Matchmaker.SeatB.IsValid() ) Gizmo.Draw.Line( Matchmaker.WorldPosition, Matchmaker.SeatB.WorldPosition );
    }

    private void DrawOccupancyStatus()
    {
        var myId = Connection.Local?.Id;
        if ( !myId.HasValue ) return;
        bool sittingA = Matchmaker.SeatA?.Occupant?.Network?.Owner?.Id == myId;
        bool sittingB = Matchmaker.SeatB?.Occupant?.Network?.Owner?.Id == myId;

        if ( sittingA || sittingB )
        {
           Gizmo.Draw.Color = Color.Green;
           Gizmo.Draw.Text( $"LOCAL PLAYER: {(sittingA ? "A" : "B")}", Matchmaker.WorldTransform.WithPosition(Matchmaker.WorldPosition + Vector3.Up * 25) );
        }
    }

    private void DrawScreenStatus()
    {
        var wp = ScreenObject.Components.Get<Sandbox.WorldPanel>();
        var panel = ScreenObject.Components.Get<OilPumperScreenpanel>();
        string status = $"Screen: {ScreenObject.Name}\nWP: {(wp != null && wp.Enabled ? "OK" : "OFF")}\nLogic: {(panel?.Matchmaker != null ? "OK" : "ERR")}";
        Gizmo.Draw.Color = (wp != null && wp.Enabled) ? Color.White : Color.Red;
        Gizmo.Draw.Text( status, ScreenObject.WorldTransform.WithPosition(ScreenObject.WorldPosition + Vector3.Up * 10) );
    }

    [Button( "Toggle Fake Opponent" )]
    public void ToggleFakeOpponent()
    {
        if ( !Matchmaker.IsValid() ) return;

        var myId = Connection.Local?.Id;
        bool inA = Matchmaker.SeatA?.Occupant?.Network?.Owner?.Id == myId;
        var targetSeat = inA ? Matchmaker.SeatB : Matchmaker.SeatA;

        if ( targetSeat == null ) return;

        if ( targetSeat.Occupant.IsValid() && targetSeat.Occupant.Name == "DEBUG_DUMMY" )
        {
           targetSeat.Leave();
           Log.Info( "Debugger: Fake opponent removed." );
           return;
        }

        var dummy = Scene.CreateObject();
        dummy.Name = "DEBUG_DUMMY";
        dummy.WorldPosition = targetSeat.WorldPosition;
        dummy.SetParent( targetSeat.GameObject );
        targetSeat.Sit( dummy );

        Log.Info( $"Debugger: Fake opponent placed in Seat {(inA ? "B" : "A")}" );
    }

    [Button( "Dummy: Force Choice (Random)" )]
    public void ForceDummyChoice()
    {
        if ( !Matchmaker.IsValid() ) return;

        bool dummyIsA = Matchmaker.SeatA?.Occupant?.Name == "DEBUG_DUMMY";
        bool dummyIsB = Matchmaker.SeatB?.Occupant?.Name == "DEBUG_DUMMY";

        if ( !dummyIsA && !dummyIsB )
        {
           Log.Warning( "Debugger: No Dummy found!" );
           return;
        }

        int choice = Random.Shared.Next( 1, 3 );
        Log.Info( $"Debugger: Dummy {(dummyIsA ? "A" : "B")} choosing {choice}" );
    
        if ( dummyIsA ) Matchmaker.PlayerAChoice = choice;
        else Matchmaker.PlayerBChoice = choice;
    }

    [Button( "Force Reset GameState" )]
    public void ResetState()
    {
        if ( Matchmaker.IsValid() )
        {
            Matchmaker.CurrentState = OilPumperMatchmaker.GameState.Waiting;
            Matchmaker.PlayerAChoice = 0;
            Matchmaker.PlayerBChoice = 0;
            Log.Info( "Debugger: Reset Forced." );
        }
    }

    [Button( "Analysis: Economy & Wallet" )]
    public void RunEconomyAnalysis()
    {
        Log.Info( "--- ECONOMY DIAGNOSTIC ---" );
        if ( EconomyManager.Instance.IsValid() ) Log.Info( $"Econ: ${EconomyManager.Instance.CurrentMoney:N2}" );
        if ( GameSaveSystem.Instance?.CurrentCharacter != null ) Log.Info( $"Save: ${GameSaveSystem.Instance.CurrentCharacter.Money:N2}" );
        VerifySync();
    }

    private void VerifySync()
    {
        if ( !EconomyManager.Instance.IsValid() || GameSaveSystem.Instance?.CurrentCharacter == null ) return;
        double diff = Math.Abs( EconomyManager.Instance.CurrentMoney - GameSaveSystem.Instance.CurrentCharacter.Money );
        if ( diff > 0.01 ) Log.Error( "!!! DESYNC DETECTED !!!" );
        else Log.Info( "Sync Check: OK" );
    }
}
