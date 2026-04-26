using Sandbox;
using Sandbox.Services;
using System;
using System.Linq;

namespace Sinvest;

[Title( "Debug Manager" )]
[Category( "Sinvest" )]
public sealed class SinvestDebugManager : Component
{
    // --- THE FIX: Define the static instance ---
    public static SinvestDebugManager Instance { get; private set; }

    [Property, Group( "Economy" )] public double SetMoney { get; set; } = 1000;
    [Property, Group( "Economy" )] public double SetShares { get; set; } = 0;

    [Property, Group( "Inventory" )] public bool ForceHasPhone { get; set; }

    [Property, Group( "System" )] public bool UseLocalSaveOnly { get; set; }
    [Property, Group( "System" )] public bool ResetStatsOnStart { get; set; }

    protected override void OnAwake()
    {
        // 1. Singleton Guard
        if ( Instance.IsValid() && Instance != this )
        {
            GameObject.Destroy();
            return;
        }

        Instance = this;

        // 2. Apply your specific persistence flags
        GameObject.Flags |= GameObjectFlags.DontDestroyOnLoad;
        GameObject.Flags |= GameObjectFlags.NotSaved;
        GameObject.Parent = null; 
    }

    protected override void OnStart()
    {
        if ( IsProxy ) return;

        if ( ResetStatsOnStart )
        {
            Log.Warning( "DEBUG: Resetting Cloud Stats for this session!" );
            // Use your session keys
            Stats.SetValue( SinvestSession.GetSlotKey( "money" ), 0 );
            Stats.SetValue( SinvestSession.GetSlotKey( "fundino_shares" ), 0 );
            Stats.SetValue( SinvestSession.GetSlotKey( "has_phone" ), 0 );
            
            SetMoney = 0;
            SetShares = 0;
            ForceHasPhone = false;
        }
    }

    [Button( "Apply Debug Overrides" )]
    public void Apply()
    {
        // Now 'EconomyManager.Instance' will work if EconomyManager also has this static setup
        if ( EconomyManager.Instance.IsValid() )
        {
            EconomyManager.Instance.DebugSetValues( SetMoney, SetShares );
        }
        else
        {
            Log.Warning( "DEBUG: EconomyManager.Instance is still NULL or not in scene!" );
        }
    }

    [ConCmd( "sv_sinvest_rich" )]
    public static void GiveMoney()
    {
        // Option A: Use the newly created Instance
        if ( Instance.IsValid() )
        {
            Instance.SetMoney = 9999999;
            Instance.Apply();
            return;
        }

        // Option B: Fallback search if Instance is somehow lost
        var dbg = Game.ActiveScene.GetAll<SinvestDebugManager>().FirstOrDefault();
        if ( dbg.IsValid() )
        {
            dbg.SetMoney = 9999999;
            dbg.Apply();
        }
    }
}
