using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Sinvest;

public sealed class RosterManager : Component
{
    // [JsonIgnore] prevents the Scene from saving its own version of this list
    [Property, ReadOnly] public List<IdleMonData> ActiveNodes { get; set; } = new();

    protected override void OnStart() { }

    public void SaveRoster()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

        // Use IncludeFields because we are using a Struct
        var options = new JsonSerializerOptions { IncludeFields = true };
        string json = JsonSerializer.Serialize( ActiveNodes, options );

        save.SetStoredString( "idlemon_roster", json );
        save.SetStoredString( "idlemon_last_timestamp", DateTime.UtcNow.ToString() );

        // We call this to ensure the slot is actually registered
        _ = save.SaveActiveSlotAsync();

        Log.Info( $"[IDLEMON] Roster Saved: {ActiveNodes.Count( x => x.ID != Guid.Empty )} Mons active." );
    }

    public void LoadRoster()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

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
                    Log.Info( $"[IDLEMON] Disk Load Success: Found {ActiveNodes.Count( x => x.ID != Guid.Empty )} existing Mons." );
                    return;
                }
            }
            catch ( Exception e )
            {
                Log.Error( $"[IDLEMON] Load Fail: {e.Message}" );
            }
        }
        
        Log.Warning( "[IDLEMON] No valid save on disk. Resetting to Genesis." );
        ResetToGenesis();
    }

    private void ResetToGenesis()
    {
        ActiveNodes = new List<IdleMonData>();
        for ( int i = 0; i < 6; i++ )
        {
            ActiveNodes.Add( IdleMonData.Empty );
        }
    }

    private bool _hasInitialized = false;

    protected override void OnUpdate()
    {
        if ( !_hasInitialized && GameSaveSystem.Instance.IsValid() )
        {
            var save = GameSaveSystem.Instance;

            if ( save.CurrentCharacter == null )
            {
                save.LoadActiveSlot();
            }

            if ( save.CurrentCharacter != null )
            {
                // FORCE LOAD HERE
                LoadRoster();
                CalculateOfflineGains();
                _hasInitialized = true;
            }
        }

        if ( !_hasInitialized ) return;

        double currentG = CalculateTotalYield();

        Gizmo.Draw.ScreenText(
           $"--- IDLEMON.INO DEBUG ---\n" +
           $"Yield (G): {currentG:F2} bucks/sec\n" +
           $"Active Nodes: {ActiveNodes.Count( x => x.ID != Guid.Empty )}/6",
           new Vector2( 50, 50 )
        );
    }

    public void SwapNode( int index, IdleMonData candidate )
    {
        if ( index < 0 || index >= ActiveNodes.Count ) return;

        ActiveNodes[index] = candidate;
        Log.Info( $"[ROSTER] Swapped Slot {index} for {candidate.Name}" );

        SaveRoster();
    }

    public double CalculateTotalYield()
    {
        if ( ActiveNodes == null || ActiveNodes.Count == 0 ) return 0;

        double sumA = ActiveNodes.Sum( n => n.Addition );
        double sumS = ActiveNodes.Sum( n => n.Subtraction );

        double prodD = ActiveNodes.Aggregate( 1.0, ( acc, n ) =>
           acc * ( n.ID == Guid.Empty || n.Division == 0 ? 1.0 : n.Division ) );

        double prodM = ActiveNodes.Aggregate( 1.0, ( acc, n ) =>
           acc * ( n.ID == Guid.Empty ? 1.0 : n.Multiplier ) );

        double prodT = ActiveNodes.Aggregate( 1.0, ( acc, n ) =>
           acc * ( n.ID == Guid.Empty ? 1.0 : n.TeamBonus ) );

        double basePool = Math.Max( 0, sumA - sumS );
        return ( basePool / prodD ) * prodM * prodT;
    }

    private void CalculateOfflineGains()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() ) return;

        string lastTimestampStr = save.GetStoredString( "idlemon_last_timestamp" );

        if ( DateTime.TryParse( lastTimestampStr, out DateTime lastSeen ) )
        {
            TimeSpan gap = DateTime.UtcNow - lastSeen;
            if ( gap.TotalSeconds > 5 ) // Only award if away for more than 5s
            {
                double yield = CalculateTotalYield();
                double offlineGains = yield * gap.TotalSeconds;

                if ( offlineGains > 0 && EconomyManager.Instance.IsValid() )
                {
                    EconomyManager.Instance.CommitTransaction( offlineGains, 0 );
                    Log.Info( $"[IDLEMON] Offline Gains: +{offlineGains:F2} Bucks" );
                }
            }
        }
    }

    [Button( "Wipe Roster Save", "delete" )]
    public void ClearSaveFile()
    {
        ResetToGenesis();
        SaveRoster();
        Log.Info( "[IDLEMON] Roster Wiped and Identity Disk written." );
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
}
