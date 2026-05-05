using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sinvest;

/// <summary>
/// Manages the active team of IdleMons, handles economic yield calculations, 
/// and orchestrates persistence via the GameSaveSystem.
/// </summary>
public sealed class RosterManager : Component
{
    public static RosterManager Instance { get; private set; }
    
    private static readonly JsonSerializerOptions _jsonOptions = new() { IncludeFields = true };
    
    [Property] public bool ShowDebugHUD { get; set; } = true;
    [Property] public double BaseGeneration { get; set; } = 0.0; // Starting PPS before modifiers
    
    protected override void OnAwake()
    {
       Instance = this;
    }

    [Property, ReadOnly] public List<IdleMonData> ActiveNodes { get; set; } = new();
    [Property, ReadOnly] public double IdleBucks { get; private set; } = 0;
    
    private double _pendingBucks = 0;
    private TimeSince _timeSinceLastFlush = 0;
    [Property] public float FlushInterval { get; set; } = 1.0f;

    private RealTimeSince _timeSinceLastChange;
    private bool _isDirty = false;
    private const float SYNC_DELAY = 10.0f; // Seconds to wait before auto-syncing changes

    public bool IsDirty => _isDirty;
    private bool _hasInitialized = false;

    /// <summary>
    /// THE CORE MATH ENGINE (GDD Section 3)
    /// Calculates current Profit Per Second (PPS) across the roster.
    /// Order: (Additive Phase) -> (Divisive Phase) -> (Multiplicative Phase)
    /// Formula: G = [ max(0, ((B + sumA) - sumS) / prodD) ] * prodM * prodT
    /// </summary>
    public double CalculateTotalYield()
    {
        if ( ActiveNodes == null || ActiveNodes.Count == 0 ) return 0;

        // 1. ACCUMULATION PHASE
        // We aggregate traits across all 6 slots simultaneously.
        double sumA = 0;    // Sum of all Addition stats
        double sumS = 0;    // Sum of all Subtraction stats
        double prodD = 1.0; // Product of all Division stats
        double prodM = 1.0; // Product of all Multipliers
        double prodT = 1.0; // Product of Team Bonuses

        foreach ( var node in ActiveNodes )
        {
           if ( node.ID == Guid.Empty ) continue;

           sumA += node.Addition;
           sumS += node.Subtraction;
            
           // Using 1.0 as identity (Empty slots have no impact on products)
           prodD *= Math.Max( 1.0, node.Division );
           prodM *= Math.Max( 0, node.Multiplier ); 
           prodT *= Math.Max( 0, node.TeamBonus );
        }

        // 2. THE FORMULA ASSEMBLY
        // Base Income calculation
        double additiveBase = (BaseGeneration + sumA) - sumS;

        // Efficiency reduction applied BEFORE multipliers to dampen growth curves
        double quotient = additiveBase / prodD;

        // Economic Floor: Prevents "Negative Yield" from depleting funds
        double clampedBase = Math.Max( 0, quotient );

        // 3. GLOBAL SYNERGY
        // Final scaling applied at the end of the chain
        return clampedBase * prodM * prodT;
    }

    /// <summary>
    /// Deducts funds and marks the ledger as "Dirty" to trigger an eventual sync.
    /// </summary>
    public void ConsumeIdleBucks( double amount )
    {
        IdleBucks -= amount;
        SaveRoster();
        _isDirty = true; 
        _timeSinceLastChange = 0;
    }

    /// <summary>
    /// Utility for UI feedback. Simulates replacing each slot with a candidate 
    /// to determine which swap yields the highest PPS increase/decrease.
    /// </summary>
    public double[] GetSwapDeltas( IdleMonData candidate )
    {
        double[] deltas = new double[6];
        if ( ActiveNodes == null || ActiveNodes.Count < 6 ) return deltas;

        double currentYield = CalculateTotalYield();

        for ( int i = 0; i < 6; i++ )
        {
            var originalMon = ActiveNodes[i];
            ActiveNodes[i] = candidate;
            
            deltas[i] = CalculateTotalYield() - currentYield;
            
            ActiveNodes[i] = originalMon; // Restore state
        }
        return deltas;
    }

    protected override void OnUpdate()
    {
        if ( ShowDebugHUD ) DrawDebugHUD();

        // Lazy-loading sequence once GameSaveSystem is ready
        if ( !_hasInitialized && GameSaveSystem.Instance.IsValid() )
        {
            var save = GameSaveSystem.Instance;
    
            if ( save.CurrentCharacter == null )
            {
               save.LoadActiveSlot(); 
            }

            LoadRoster();
            CalculateOfflineGains();
            _hasInitialized = true;
        }

        if ( !_hasInitialized ) return;

        ProcessEconomy();

        // Auto-save logic: Commits changes if no new changes occurred during SYNC_DELAY
        if ( _isDirty && _timeSinceLastChange > SYNC_DELAY )
        {
            _isDirty = false;
            SaveRoster();
        }
    }

    /// <summary>
    /// Integrates yield over time. Bucks are stored in a pending buffer and 
    /// "flushed" to the main balance at set intervals to reduce save frequency.
    /// </summary>
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
                // Immediate update to local save buffer for data safety
                GameSaveSystem.Instance?.SetStoredStat( "idlemon_bucks", IdleBucks );
            }
            _timeSinceLastFlush = 0;
        }
    }

    private void DrawDebugHUD()
    {
        double currentG = CalculateTotalYield();
        Gizmo.Draw.ScreenText(
            $"--- IDLEMON.INO SYSTEM ---\n" +
            $"IdleBucks: {IdleBucks:F0} IB\n" +
            $"Current Yield: {currentG:F2} IB/sec\n" +
            $"Active Nodes: {ActiveNodes.Count( x => x.ID != Guid.Empty )}/6\n" +
            $"Sync Status: {(_isDirty ? "SYNC PENDING" : "SECURED")}",
            new Vector2( 50, 50 ), "Consolas", 18, TextFlag.Left
        );
    }

    /// <summary>
    /// Primary entry point for roster modification. Triggers an immediate disk commit.
    /// </summary>
    public void SwapNode( int index, IdleMonData candidate )
    {
        if ( index < 0 || index >= ActiveNodes.Count ) return;
    
        ActiveNodes[index] = candidate;
        SaveRoster();
    
        _isDirty = false;
        _timeSinceLastChange = 0;
    
        Log.Info( $"[ROSTER] Slot {index} updated. Transaction committed." );
    }

    /// <summary>
    /// Serializes current state to the persistent storage.
    /// Handles both detailed JSON (roster) and flat stats (leaderboards).
    /// </summary>
    public void SaveRoster()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

        // 1. FULL DATA (The "Source of Truth" for the roster)
        string json = JsonSerializer.Serialize( ActiveNodes, _jsonOptions );
        save.SetStoredString( "idlemon_roster", json );

        // 2. COMPETITIVE STATS (Standardized keys for Global Stats API)
        double yield = CalculateTotalYield();
        save.SetStoredStat( "idlemon_bucks", IdleBucks );
        save.SetStoredStat( "current_yield_pps", yield ); 
    
        double maxLuck = ActiveNodes.Count > 0 ? ActiveNodes.Max( x => x.Luck ) : 0;
        save.SetStoredStat( "roster_max_luck", maxLuck );

        // Timestamp for calculating offline gains upon next load
        save.SetStoredString( "idlemon_last_timestamp", DateTime.UtcNow.ToString("O") );
        
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
              var loaded = JsonSerializer.Deserialize<List<IdleMonData>>( json, _jsonOptions );
              if ( loaded != null )
              {
                 ActiveNodes = loaded;
                 // Ensure we always have exactly 6 slots
                 while (ActiveNodes.Count < 6) ActiveNodes.Add(IdleMonData.Empty);
                 return;
              }
           }
           catch ( Exception e ) 
           { 
              Log.Error( $"[ROSTER] Load failed - schema mismatch: {e.Message}" ); 
           }
        }
        ResetToGenesis();
    }

    private void ResetToGenesis()
    {
        ActiveNodes = new List<IdleMonData>();
        for ( int i = 0; i < 6; i++ ) ActiveNodes.Add( IdleMonData.Empty );
    }

    /// <summary>
    /// Calculates catch-up income based on the last recorded session timestamp.
    /// </summary>
    private void CalculateOfflineGains()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

        string lastTimestampStr = save.GetStoredString( "idlemon_last_timestamp" );
        if ( DateTime.TryParse( lastTimestampStr, out DateTime lastSeen ) )
        {
            TimeSpan gap = DateTime.UtcNow - lastSeen;
            if ( gap.TotalSeconds > 1 ) 
            {
                double yield = CalculateTotalYield();
                double gains = yield * gap.TotalSeconds;
                IdleBucks += gains;
                Log.Info( $"[ROSTER] Offline for {gap.TotalMinutes:F1} mins. Yielded {gains:F0} IB." );
            }
        }
    }
    
    protected override void OnDisabled()
    {
        // Safety flush on shutdown
        if ( _isDirty )
        {
           SaveRoster();
           Log.Info( "[ROSTER] Emergency Save triggered on Component Disable." );
        }
    }
}
