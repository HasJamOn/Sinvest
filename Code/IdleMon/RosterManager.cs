using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sinvest;

public sealed class RosterManager : Component
{
    [Property, ReadOnly] public List<IdleMonData> ActiveNodes { get; set; } = new();
    
    [Property, ReadOnly] public double IdleBucks { get; private set; } = 0;
    private double _pendingBucks = 0;
    private TimeSince _timeSinceLastFlush = 0;
    [Property] public float FlushInterval { get; set; } = 1.0f; 

    private bool _hasInitialized = false;

    protected override void OnUpdate()
    {
        // --- 1. DRAW HUD FIRST ---
        // This ensures the screen isn't black even if loading fails
        DrawDebugHUD();

        // --- 2. INITIALIZATION ---
        // Inside RosterManager.cs -> OnUpdate()
        if ( !_hasInitialized && GameSaveSystem.Instance.IsValid() )
        {
	        var save = GameSaveSystem.Instance;

	        // FIX: If we are in the editor/test scene and haven't loaded a character, force it.
	        if ( save.CurrentCharacter == null )
	        {
		        Log.Warning( "[ROSTER] No character session found. Forcing LoadActiveSlot for Test Scene." );
		        save.LoadActiveSlot(); 
	        }

	        // Now check again
	        if ( save.CurrentCharacter != null && save.CurrentCharacter.Name != "New Character" )
	        {
		        LoadRoster();
		        CalculateOfflineGains();
		        _hasInitialized = true;
	        }
	        else
	        {
		        // If we still have no character, it's a fresh save.
		        ResetToGenesis();
		        _hasInitialized = true;
	        }
        }

        if ( !_hasInitialized ) return;

        // --- 3. CORE LOGIC ---
        ProcessEconomy();
    }

    private void DrawDebugHUD()
    {
        double currentG = CalculateTotalYield();
        var econ = EconomyManager.Instance;
        
        Gizmo.Draw.ScreenText(
            $"--- IDLEMON.INO TEST HARNESS ---\n" +
            $"OS Cash: ${(econ.IsValid() ? econ.CurrentMoney : 0):C}\n" +
            $"IdleBucks: {IdleBucks:F2} IB\n" +
            $"Current Yield: {currentG:F2} IB/sec\n" +
            $"Active Nodes: {ActiveNodes.Count( x => x.ID != Guid.Empty )}/6\n" +
            $"[R] Roll | [1-6] Swap",
            new Vector2( 50, 50 ), "Consolas", 18, TextFlag.Left
        );
    }

    private void ProcessEconomy()
    {
        double yield = CalculateTotalYield();
        _pendingBucks += yield * Time.Delta;

        if ( _timeSinceLastFlush > FlushInterval )
        {
            if ( _pendingBucks > 0 )
            {
                IdleBucks += _pendingBucks;
                _pendingBucks = 0;
                GameSaveSystem.Instance?.SetStoredStat( "idlemon_bucks", IdleBucks );
            }
            _timeSinceLastFlush = 0;
        }
    }

    public void SwapNode( int index, IdleMonData candidate )
    {
        if ( index < 0 || index >= ActiveNodes.Count ) return;
        
        ActiveNodes[index] = candidate;
        Log.Info( $"[ROSTER] Node {index} updated to {candidate.Name}" );
        SaveRoster();
    }

    public double[] GetSwapDeltas( IdleMonData candidate )
    {
        double[] deltas = new double[6];
        if ( ActiveNodes == null || ActiveNodes.Count < 6 ) return deltas;

        double currentYield = CalculateTotalYield();
        for ( int i = 0; i < 6; i++ )
        {
            var previousMon = ActiveNodes[i];
            ActiveNodes[i] = candidate;
            deltas[i] = CalculateTotalYield() - currentYield;
            ActiveNodes[i] = previousMon; 
        }
        return deltas;
    }

    public double CalculateTotalYield()
    {
        if ( ActiveNodes == null || ActiveNodes.Count == 0 ) return 0;

        double sumA = ActiveNodes.Sum( n => n.Addition );
        double sumS = ActiveNodes.Sum( n => n.Subtraction );
        double prodD = ActiveNodes.Aggregate( 1.0, ( acc, n ) => acc * ( n.ID == Guid.Empty || n.Division == 0 ? 1.0 : n.Division ) );
        double prodM = ActiveNodes.Aggregate( 1.0, ( acc, n ) => acc * ( n.ID == Guid.Empty ? 1.0 : n.Multiplier ) );
        double prodT = ActiveNodes.Aggregate( 1.0, ( acc, n ) => acc * ( n.ID == Guid.Empty ? 1.0 : n.TeamBonus ) );

        return ( Math.Max( 0, sumA - sumS ) / prodD ) * prodM * prodT;
    }

    public void SaveRoster()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

        var options = new JsonSerializerOptions { IncludeFields = true };
        string json = JsonSerializer.Serialize( ActiveNodes, options );

        save.SetStoredString( "idlemon_roster", json );
        save.SetStoredStat( "idlemon_bucks", IdleBucks );
        save.SetStoredString( "idlemon_last_timestamp", DateTime.UtcNow.ToString() );
        _ = save.SaveActiveSlotAsync();
    }

    public void LoadRoster()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

        IdleBucks = save.GetStoredStat( "idlemon_bucks" );
        string json = save.GetStoredString( "idlemon_roster" );
        
        if ( !string.IsNullOrEmpty( json ) && json != "[]" )
        {
            try
            {
                var options = new JsonSerializerOptions { IncludeFields = true };
                var loaded = JsonSerializer.Deserialize<List<IdleMonData>>( json, options );
                if ( loaded != null && loaded.Count == 6 )
                {
                    ActiveNodes = loaded;
                    return;
                }
            }
            catch ( Exception e ) { Log.Error( $"Load Fail: {e.Message}" ); }
        }
        ResetToGenesis();
    }

    private void ResetToGenesis()
    {
        ActiveNodes = new List<IdleMonData>();
        for ( int i = 0; i < 6; i++ ) ActiveNodes.Add( IdleMonData.Empty );
    }

    private void CalculateOfflineGains()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

        string lastTimestampStr = save.GetStoredString( "idlemon_last_timestamp" );
        if ( DateTime.TryParse( lastTimestampStr, out DateTime lastSeen ) )
        {
            TimeSpan gap = DateTime.UtcNow - lastSeen;
            if ( gap.TotalSeconds > 2 ) 
            {
                double yield = CalculateTotalYield();
                IdleBucks += (yield * gap.TotalSeconds);
            }
        }
    }
}
