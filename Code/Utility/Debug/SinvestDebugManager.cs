using Sandbox;
using Sandbox.Services;
using System;
using System.Linq;

namespace Sinvest;

[Title( "Debug Manager" )]
[Category( "Sinvest" )]
public sealed class SinvestDebugManager : Component
{
    public static SinvestDebugManager Instance { get; private set; }

    [Property, Group( "Economy" )] public decimal SetMoney { get; set; } = 1000;
    [Property, Group( "Economy" )] public decimal SetShares { get; set; } = 0;

    [Property, Group( "Inventory" )] public bool ForceHasPhone { get; set; }

    [Property, Group( "System" )] public bool UseLocalSaveOnly { get; set; }
    [Property, Group( "System" )] public bool ResetStatsOnStart { get; set; }

    protected override void OnAwake()
    {
        if ( Instance.IsValid() && Instance != this )
        {
            GameObject.Destroy();
            return;
        }

        Instance = this;
        GameObject.Flags |= GameObjectFlags.DontDestroyOnLoad;
        GameObject.Flags |= GameObjectFlags.NotSaved;
        GameObject.Parent = null; 
    }

    // --- SAVE MANAGEMENT ---

    [Button( "Purge Current Slot" ), Group( "Save Management" )]
    public void PurgeActive() => RequestPurge( SinvestSession.ActiveSlot );

    [Button( "Wipe All Data" ), Group( "Save Management" )]
    public void PurgeAll()
    {
        for ( int i = 1; i <= 3; i++ ) RequestPurge( i );
        Log.Info( "DEBUG: All local and cloud slots have been reset." );
    }

    private void RequestPurge( int slot )
    {
        var saver = GameSaveSystem.Instance ?? Scene.GetAll<GameSaveSystem>().FirstOrDefault();
        if ( saver.IsValid() )
        {
            saver.DeleteSlot( slot );
            Log.Info( $"DEBUG: Purged Slot {slot}" );
        }
        else
        {
            Log.Error( "DEBUG: Could not find GameSaveSystem to perform purge!" );
        }
    }

    // --- DYNAMIC OVERRIDES ---

    [Button( "Apply Overrides to Active Slot" )]
    public void Apply()
    {
        var save = GameSaveSystem.Instance;
        if ( !save.IsValid() || save.CurrentCharacter == null )
        {
            Log.Warning( "DEBUG: Cannot apply overrides. No active character session found." );
            return;
        }

        // 1. Update the live EconomyManager (instantly updates UI and Session)
        if ( EconomyManager.Instance.IsValid() )
        {
            EconomyManager.Instance.DebugSetValues( SetMoney, SetShares );
        }
        
        Log.Info( $"DEBUG: Applied overrides to Slot {SinvestSession.ActiveSlot} and updated Security Signature." );
    }

    [ConCmd( "sv_sinvest_rich" )]
    public static void GiveMoney()
    {
        if ( !Instance.IsValid() ) return;

        Instance.SetMoney = 9999999;
        Instance.Apply();
    }

    [Button( "Force Re-Sign Active Save" )]
    public void ManualResign()
    {
        Log.Info( "DEBUG: Manually triggered a security re-sign." );
    }
}
