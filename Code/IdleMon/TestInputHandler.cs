using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class TestInputHandler : Component
{
    [Property] public AssetGenerator Generator { get; set; }
    [Property] public RosterManager Roster { get; set; }
    
    // Drag the GameObject with the IdleMonDashBoard component here
    [Property] public GameObject DashboardRoot { get; set; } 

    private IdleMonData _currentCandidate;
    private bool _hasCandidate = false;

    // --- UI INSPECTOR ACTION ---

    [Button( "UI: Toggle Dashboard", "visibility" )]
    public void ToggleDashboard()
    {
        if ( !DashboardRoot.IsValid() )
        {
            Log.Warning( "DashboardRoot is not assigned in the Inspector!" );
            return;
        }

        DashboardRoot.Enabled = !DashboardRoot.Enabled;
        Log.Info( $"[UI] Dashboard Visibility: {DashboardRoot.Enabled}" );
    }
    
    [Button( "UI: Toggle Gizmos", "monitor" )]
    public void ToggleGizmos()
    {
	    if ( Roster.IsValid() )
	    {
		    Roster.ShowDebugHUD = !Roster.ShowDebugHUD;
		    Log.Info( $"[GIZMO] HUD is now {(Roster.ShowDebugHUD ? "VISIBLE" : "HIDDEN")}" );
	    }
    }

    // --- EXISTING INSPECTOR ACTIONS ---

    [Button( "DEBUG: Nuke App Data", "delete_forever" )]
    public void ResetAppData()
    {
        var saveSystem = Scene.GetAllComponents<GameSaveSystem>().FirstOrDefault();

        if ( saveSystem.IsValid() )
        {
           saveSystem.ClearIdleMonData();
           var roster = Scene.GetAllComponents<RosterManager>().FirstOrDefault();
           roster?.LoadRoster();

           _hasCandidate = false;
           Log.Info( "--- EDITOR-SIDE WIPE COMPLETE ---" );
        }
        else
        {
           Log.Warning( "Could not find GameSaveSystem in the active scene." );
        }
    }
    
    [Button( "PRINT: Roster Audit", "assignment" )]
    public void PrintRosterAudit()
    {
        if ( !Roster.IsValid() ) return;

        Log.Info( "--- ROSTER AUDIT START ---" );
        Log.Info( $"Total IdleBucks: {Roster.IdleBucks:N2}" );
        Log.Info( $"Total Yield: {Roster.CalculateTotalYield():F2} IB/sec" );
        Log.Info( "--------------------------" );

        for ( int i = 0; i < Roster.ActiveNodes.Count; i++ )
        {
           var node = Roster.ActiveNodes[i];
           if ( node.ID == Guid.Empty )
           {
              Log.Info( $"Slot {i}: [gray]EMPTY[/]" );
              continue;
           }

           string genColor = node.Generation > 1 ? "cyan" : "white";
           Log.Info( $"Slot {i}: [{genColor}]{node.Name}[/] (Gen {node.Generation})" );
           Log.Info( $"   Stats -> A: {node.Addition} | M: {node.Multiplier:F2}x | S: {node.Subtraction:F1}" );
        }
        Log.Info( "--- ROSTER AUDIT END ---" );
    }

    [Button( "Manual Roll", "casino" )]
    public void ManualRoll()
    {
        if ( !Generator.IsValid() ) return;
        _currentCandidate = Generator.RollNewAsset();
        _hasCandidate = true;
        Log.Info( $"Drafting: {_currentCandidate.Name}" );
    }

    protected override void OnUpdate()
    {
        if ( Input.Pressed( "reload" ) ) ManualRoll();

        if ( _hasCandidate )
        {
            for ( int i = 0; i < 6; i++ )
            {
                if ( Input.Pressed( $"slot{i + 1}" ) ) AcceptDraft( i );
            }
        }
    }

    private void AcceptDraft( int index )
    {
        Roster?.SwapNode( index, _currentCandidate );
        _hasCandidate = false;
        Log.Info( $"Node {index} set to {_currentCandidate.Name}" );
    }
}
