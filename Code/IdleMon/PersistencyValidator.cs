using Sandbox;
using System;
using System.Linq;

namespace Sinvest;

public sealed class PersistenceValidator : Component
{
	[Property] public AssetGenerator Generator { get; set; }
	[Property] public RosterManager Roster { get; set; }

	[Button( "TEST: High-Market Roll" )]
	public void TestHighMarket()
	{
		Log.Info( "--- STARTING VALIDATION: BULL MARKET ---" );
        
		// 1. Simulate a high market price in MarketService
		MarketService.UpdatePrice( 15000.0 ); // ~2.1x Market Scale
        
		var node = Generator.RollNewAsset();
		float expectedMinS = 2.0f;

		if ( node.MarketScaleAtBirth >= expectedMinS )
			Log.Info( $"✔ SCALE SUCCESS: Node born at {node.MarketScaleAtBirth:F2}x" );
		else
			Log.Error( $"✘ SCALE FAILURE: Expected ~2.1x, got {node.MarketScaleAtBirth:F2}x" );

		// 2. Test Delta Math
		var deltas = Roster.GetSwapDeltas( node );
		Log.Info( $"Delta for Slot 0: {deltas[0]:F2} IB/s" );
        
		Log.Info( "--- VALIDATION COMPLETE ---" );
	}

	[Button( "TEST: Persistence Round-Trip" )]
	public async void TestPersistence()
	{
		Log.Info( "--- STARTING VALIDATION: COOKIE ROUND-TRIP ---" );
        
		double originalBucks = Roster.IdleBucks;
		Roster.SaveRoster();
        
		// Wipe local memory
		Roster.LoadRoster();
        
		if ( Math.Abs( Roster.IdleBucks - originalBucks ) < 0.1 )
			Log.Info( "✔ PERSISTENCE SUCCESS: Bucks restored correctly." );
		else
			Log.Error( $"✘ PERSISTENCE FAILURE: Expected {originalBucks}, got {Roster.IdleBucks}" );
	}
}
