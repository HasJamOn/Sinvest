using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sinvest;

public sealed class RosterManager : Component
{
    public static RosterManager Instance { get; private set; }
    
    protected override void OnAwake()
    {
       Instance = this;
    }

    [Property, ReadOnly] public List<IdleMonData> ActiveNodes { get; set; } = new();
    [Property, ReadOnly] public double IdleBucks { get; private set; } = 0;
    
    private double _pendingBucks = 0;
    private TimeSince _timeSinceLastFlush = 0;
    [Property] public float FlushInterval { get; set; } = 1.0f; 

    private bool _hasInitialized = false;

    // --- NEW: MARKET GENERATION LOGIC ---

    /// <summary>
    /// Deducts IdleBucks and flushes the change to the save system.
    /// </summary>
    public void ConsumeIdleBucks( double amount )
    {
        IdleBucks -= amount;
        GameSaveSystem.Instance?.SetStoredStat( "idlemon_bucks", IdleBucks );
    }

    /// <summary>
    /// Predicts the change in Yield (G) for a specific slot.
    /// </summary>
    public double GetSwapDelta( int slotIndex, IdleMonData candidate )
    {
        if ( slotIndex < 0 || slotIndex >= ActiveNodes.Count ) return 0;

        double currentYield = CalculateTotalYield();
        var previousMon = ActiveNodes[slotIndex];
        
        ActiveNodes[slotIndex] = candidate;
        double projectedYield = CalculateTotalYield();
        
        ActiveNodes[slotIndex] = previousMon; // Revert
        
        return projectedYield - currentYield;
    }
    
    /// <summary>
    /// Calculates yield deltas for all 6 slots at once.
    /// </summary>
    public double[] GetSwapDeltas( IdleMonData candidate )
    {
	    double[] deltas = new double[6];
	    // Ensure we have 6 slots to compare against
	    if ( ActiveNodes == null || ActiveNodes.Count < 6 ) return deltas;

	    double currentYield = CalculateTotalYield();

	    for ( int i = 0; i < 6; i++ )
	    {
		    var originalMon = ActiveNodes[i];
        
		    // Temporarily swap
		    ActiveNodes[i] = candidate;
		    double projectedYield = CalculateTotalYield();
        
		    // Calculate the difference
		    deltas[i] = projectedYield - currentYield;
        
		    // Restore
		    ActiveNodes[i] = originalMon;
	    }

	    return deltas;
    }

    // --- CORE LOOP ---

    protected override void OnUpdate()
    {
        DrawDebugHUD();

        if ( !_hasInitialized && GameSaveSystem.Instance.IsValid() )
        {
            var save = GameSaveSystem.Instance;

            if ( save.CurrentCharacter == null )
            {
               Log.Warning( "[ROSTER] No character session found. Forcing Load for Test Scene." );
               save.LoadActiveSlot(); 
            }

            if ( save.CurrentCharacter != null && save.CurrentCharacter.Name != "New Character" )
            {
               LoadRoster();
               CalculateOfflineGains();
               _hasInitialized = true;
            }
            else
            {
               ResetToGenesis();
               _hasInitialized = true;
            }
        }

        if ( !_hasInitialized ) return;

        ProcessEconomy();
    }

    private void DrawDebugHUD()
    {
        double currentG = CalculateTotalYield();
        var econ = EconomyManager.Instance;
        
        // Added Market Scale to HUD for testing
        float s = (float)(MarketService.CurrentPrice / 7126.0);

        Gizmo.Draw.Color = Color.Yellow;
        Gizmo.Draw.ScreenText(
            $"--- IDLEMON.INO SYSTEM ---\n" +
            $"Market Scale (S): {s:F2}x\n" +
            $"OS Cash: ${(econ.IsValid() ? econ.CurrentMoney : 0):C}\n" +
            $"IdleBucks: {IdleBucks:F0} IB\n" +
            $"Current Yield: {currentG:F2} IB/sec\n" +
            $"Active Nodes: {ActiveNodes.Count( x => x.ID != Guid.Empty )}/6",
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
        Log.Info( $"[ROSTER] Slot {index} replaced with {candidate.Name}" );
        SaveRoster();
    }

    public double CalculateTotalYield()
    {
        if ( ActiveNodes == null || ActiveNodes.Count == 0 ) return 0;

        double sumA = ActiveNodes.Sum( n => n.Addition );
        double sumS = ActiveNodes.Sum( n => n.Subtraction );
        
        // Aggregate logic modified to match GDD: ΠD, ΠM, ΠT
        double prodD = ActiveNodes.Aggregate( 1.0, ( acc, n ) => acc * ( n.ID == Guid.Empty || n.Division <= 0 ? 1.0 : n.Division ) );
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
            if ( gap.TotalSeconds > 5 ) 
            {
                double yield = CalculateTotalYield();
                double gains = yield * gap.TotalSeconds;
                IdleBucks += gains;
                Log.Info( $"[ROSTER] Welcome back! You earned {gains:F0} IdleBucks while away." );
            }
        }
    }
}
