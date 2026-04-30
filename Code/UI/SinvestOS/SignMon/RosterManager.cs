using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sinvest;

public sealed class RosterManager : Component
{
    public static RosterManager Instance { get; private set; }
    
    private static readonly JsonSerializerOptions _jsonOptions = new() { IncludeFields = true };
    
    [Property] public bool ShowDebugHUD { get; set; } = true;
    [Property] public double BaseGeneration { get; set; } = 0.0; // Variable 'B' from GDD
    
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
    private const float SYNC_DELAY = 10.0f; 

    public bool IsDirty => _isDirty;
    private bool _hasInitialized = false;

    /// <summary>
    /// GDD Section 3: The Core Math Engine.
    /// Formula: G = [ max(0, ((B + sumA) - sumS) / prodD) ] * prodM * prodT
    /// </summary>
    public double CalculateTotalYield()
    {
	    if ( ActiveNodes == null || ActiveNodes.Count == 0 ) return 0;

	    // 1. Accumulation Phase
	    double sumA = 0;
	    double sumS = 0;
	    double prodD = 1.0;
	    double prodM = 1.0;
	    double prodT = 1.0;

	    foreach ( var node in ActiveNodes )
	    {
		    if ( node.ID == Guid.Empty ) continue;

		    sumA += node.Addition;
		    sumS += node.Subtraction;
            
		    // Apply products using 1.0 as identity (empty/default slots do nothing)
		    prodD *= Math.Max( 1.0, node.Division );
		    prodM *= Math.Max( 0, node.Multiplier ); 
		    prodT *= Math.Max( 0, node.TeamBonus );
	    }

	    // 2. The Formula Assembly
	    // Additive base: (Base + Addition - Subtraction)
	    double additiveBase = (BaseGeneration + sumA) - sumS;

	    // Divide: Reduces the base before multipliers are applied
	    double quotient = additiveBase / prodD;

	    // Clamp: Prevents "Cursed" nodes from generating negative money
	    double clampedBase = Math.Max( 0, quotient );

	    // Final Global Multipliers (Synergy)
	    return clampedBase * prodM * prodT;
    }

    public void ConsumeIdleBucks( double amount )
    {
        IdleBucks -= amount;
        SaveRoster();
        _isDirty = true; // Mark dirty so we sync the new balance
        _timeSinceLastChange = 0;
    }

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
            
            ActiveNodes[i] = originalMon; // Restore
        }
        return deltas;
    }

    protected override void OnUpdate()
    {
        if ( ShowDebugHUD ) DrawDebugHUD();

        if ( !_hasInitialized && GameSaveSystem.Instance.IsValid() )
        {
	        var save = GameSaveSystem.Instance;
    
	        // Ensure the save system actually has a session ready
	        if ( save.CurrentCharacter == null )
	        {
		        save.LoadActiveSlot(); 
	        }

	        // REMOVE the check for "New Character". 
	        // We want to load data even if the player hasn't renamed themselves yet.
	        LoadRoster();
	        CalculateOfflineGains();
	        _hasInitialized = true;
        }

        if ( !_hasInitialized ) return;

        ProcessEconomy();

        if ( _isDirty && _timeSinceLastChange > SYNC_DELAY )
        {
            _isDirty = false;
            SaveRoster();
        }
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
                // We save stats to the local buffer immediately
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

    public void SwapNode( int index, IdleMonData candidate )
    {
	    if ( index < 0 || index >= ActiveNodes.Count ) return;
    
	    ActiveNodes[index] = candidate;

	    // Trigger an immediate commit to disk
	    SaveRoster();
    
	    // Reset the dirty flag since we just saved
	    _isDirty = false;
	    _timeSinceLastChange = 0;
    
	    Log.Info( $"[ROSTER] Slot {index} updated. Transaction committed to FileSystem." );
    }

    public void SaveRoster()
    {
	    var save = GameSaveSystem.Instance;
	    if ( !save.IsValid() ) return;

	    // 1. COMPLEX DATA (Cookies)
	    string json = JsonSerializer.Serialize( ActiveNodes, _jsonOptions );
	    save.SetStoredString( "idlemon_roster", json );

	    // 2. COMPETITIVE STATS (Numeric Stats for Leaderboards)
	    double yield = CalculateTotalYield();
	    save.SetStoredStat( "idlemon_bucks", IdleBucks );
	    save.SetStoredStat( "current_yield_pps", yield ); // Global Yield Ranking
    
	    // Track the highest Luck in the roster for a "Luckiest Player" board
	    double maxLuck = ActiveNodes.Max( x => x.Luck );
	    save.SetStoredStat( "roster_max_luck", maxLuck );

	    save.SetStoredString( "idlemon_last_timestamp", DateTime.UtcNow.ToString("O") );
	    
	    save.UpdateSecuritySignature();
	    
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
			    // Use _jsonOptions here as well
			    var loaded = JsonSerializer.Deserialize<List<IdleMonData>>( json, _jsonOptions );
			    if ( loaded != null )
			    {
				    ActiveNodes = loaded;
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

    private void CalculateOfflineGains()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

        string lastTimestampStr = save.GetStoredString( "idlemon_last_timestamp" );
        if ( DateTime.TryParse( lastTimestampStr, out DateTime lastSeen ) )
        {
            // GDD Section 5: Offline Gains = G * (Current - Last)
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
	    // If the component is turned off or the game stops, try to save if dirty
	    if ( _isDirty )
	    {
		    SaveRoster();
		    Log.Info( "[ROSTER] Emergency Save triggered on Component Disable." );
	    }
    }
    
    
}
