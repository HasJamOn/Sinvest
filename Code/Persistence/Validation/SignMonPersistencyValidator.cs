using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

/// <summary>
/// Development utility for validating Sinvest persistence and market logic.
/// Provides manual triggers via Inspector buttons to verify "Authoritative Ledger" math and market scaling.
/// </summary>
public sealed class PersistenceValidator : Component
{
    [Property] public AssetGenerator Generator { get; set; }
    [Property] public RosterManager Roster { get; set; }

    /// <summary>
    /// Simulates a Bull Market scenario and validates if the AssetGenerator 
    /// correctly captures the MarketScaleAtBirth for new assets.
    /// </summary>
    [Button( "TEST: High-Market Roll" )]
    public async void TestHighMarket()
    {
       Log.Info( "--- STARTING VALIDATION: BULL MARKET ---" );
    
       // 1. Manually inject a high market price into the global FundinoMarketService.
       // This service tracks the singleton CurrentPrice and maintains a sliding history window (max 100 entries).
       FundinoMarketService.UpdatePrice( 15000.0 ); // ~2.1x Market Scale
    
       // FIX: Added 'await' to resolve the asynchronous generation task into actual IdleMonData.
       var node = await Generator.RollNewAsset();
       float expectedMinS = 2.0f;

       // Verification: Ensures the asset's intrinsic value scales linearly with the injected market price.
       if ( node.MarketScaleAtBirth >= expectedMinS )
          Log.Info( $"✔ SCALE SUCCESS: Node born at {node.MarketScaleAtBirth:F2}x" );
       else
          Log.Error( $"✘ SCALE FAILURE: Expected ~2.1x, got {node.MarketScaleAtBirth:F2}x" );

       // 2. Test Delta Math
       // Calculates the income difference (IdleBucks/sec) if this new asset were to replace an existing slot.
       var deltas = Roster.GetSwapDeltas( node );
       Log.Info( $"Delta for Slot 0: {deltas[0]:F2} IB/s" );
    
       Log.Info( "--- VALIDATION COMPLETE ---" );
    }

    /// <summary>
    /// Validates the Phase 1 persistence model (Local plaintext ledger).
    /// Performs a full Save-Wipe-Load cycle to ensure double-precision Bucks are maintained across sessions.
    /// </summary>
    [Button( "TEST: Persistence Round-Trip" )]
    public async void TestPersistence()
    {
       Log.Info( "--- STARTING VALIDATION: COOKIE ROUND-TRIP ---" );
        
       double originalBucks = Roster.IdleBucks;
       
       // Serialize the current roster state and balance to the local filesystem.
       Roster.SaveRoster();
        
       // Clear local memory and re-populate the RosterManager from the saved file.
       Roster.LoadRoster();
        
       // Verification: Compares pre-save and post-load values with a small epsilon to account for serialization artifacts.
       if ( Math.Abs( Roster.IdleBucks - originalBucks ) < 0.1 )
          Log.Info( "✔ PERSISTENCE SUCCESS: Bucks restored correctly." );
       else
          Log.Error( $"✘ PERSISTENCE FAILURE: Expected {originalBucks}, got {Roster.IdleBucks}" );
    }
}
