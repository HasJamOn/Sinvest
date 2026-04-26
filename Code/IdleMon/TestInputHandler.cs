using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class TestInputHandler : Component
{
    [Property] public AssetGenerator Generator { get; set; }
    [Property] public RosterManager Roster { get; set; }

    private IdleMonData _currentCandidate;
    private bool _hasCandidate = false;

    // --- INSPECTOR ACTIONS ---
    // These must be PUBLIC to show as buttons

    [Button( "DEBUG: Nuke App Data", "delete_forever" )]
    public void ResetAppData()
    {
	    // 1. Find the GameSaveSystem in the scene manually (Editor-Safe)
	    var saveSystem = Scene.GetAllComponents<GameSaveSystem>().FirstOrDefault();

	    if ( saveSystem.IsValid() )
	    {
		    // 2. Call the clear logic
		    saveSystem.ClearIdleMonData();
        
		    // 3. Force the Roster to reload its state from the now-empty cookies
		    var roster = Scene.GetAllComponents<RosterManager>().FirstOrDefault();
		    roster?.LoadRoster();

		    _hasCandidate = false;
        
		    Log.Info( "--- EDITOR-SIDE WIPE COMPLETE ---" );
	    }
	    else
	    {
		    Log.Warning( "Could not find GameSaveSystem in the active scene to perform wipe." );
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
        
		    // Check if the slot is empty (assuming empty slots have a specific ID or Name)
		    if ( node.ID == Guid.Empty )
		    {
			    Log.Info( $"Slot {i}: [gray]EMPTY[/]" );
			    continue;
		    }

		    // Color coding based on Generation
		    string genColor = node.Generation > 1 ? "cyan" : "white";

		    Log.Info( $"Slot {i}: [{genColor}]{node.Name}[/] (Gen {node.Generation})" );
		    Log.Info( $"   Stats -> A: {node.Addition} | M: {node.Multiplier:F2}x | S: {node.Subtraction:F1}" );
		    Log.Info( $"   Born at Market Scale: {node.MarketScaleAtBirth:F2}x" );
	    }

	    Log.Info( "--- ROSTER AUDIT END ---" );
    }

    [Button( "Manual Roll", "casino" )]
    public void ManualRoll()
    {
        if ( !Generator.IsValid() ) return;

        _currentCandidate = Generator.RollNewAsset();
        _hasCandidate = true;
        
        Log.Info( $"Drafting: {_currentCandidate.Name} | S: {_currentCandidate.MarketScaleAtBirth:F2}" );
    }

    // --- COMPONENT LOGIC ---

    protected override void OnUpdate()
    {
        // Keep the keyboard shortcuts for quick testing
        if ( Input.Pressed( "reload" ) ) ManualRoll();

        if ( _hasCandidate )
        {
            if ( Input.Pressed( "slot1" ) ) AcceptDraft( 0 );
            if ( Input.Pressed( "slot2" ) ) AcceptDraft( 1 );
            if ( Input.Pressed( "slot3" ) ) AcceptDraft( 2 );
            if ( Input.Pressed( "slot4" ) ) AcceptDraft( 3 );
            if ( Input.Pressed( "slot5" ) ) AcceptDraft( 4 );
            if ( Input.Pressed( "slot6" ) ) AcceptDraft( 5 );
        }
    }

    private void AcceptDraft( int index )
    {
        Roster?.SwapNode( index, _currentCandidate );
        _hasCandidate = false;
        Log.Info( $"Node {index} set to {_currentCandidate.Name}" );
    }
}
