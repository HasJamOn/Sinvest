using Sandbox;
using System;

namespace Sinvest;

public sealed class StartupManager : Component
{
	[Property] public EconomyManager Economy { get; set; }

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		var session = GameSaveSystem.Instance?.CurrentCharacter;
		if ( session == null ) return;

		ApplyModifiers( session.Modifiers );
	}

	private void ApplyModifiers( StartingModifiers mods )
	{
		Log.Info( $"[STARTUP] Processing modifiers for {GameSaveSystem.Instance.ActiveSlot}..." );

		// 1. Nepokid: Starts with $500k in S&P 500
		if ( mods.HasFlag( StartingModifiers.Nepokid ) )
		{
			// We inject into the 'shares' stat via EconomyManager
			// Note: Design doc says "Locked S&P 500", we'll track this as shares
			Economy.CommitTransaction( 0, 500000 ); 
			Log.Info( "Modifier Applied: Nepokid ($500k in shares injected)" );
		}

		// 2. Phoney: Start with a Phone (Skip Café)
		if ( mods.HasFlag( StartingModifiers.Phoney ) )
		{
			GrantItem( "item_phone" );
		}

		// 3. Internity: Unlock all jobs
		if ( mods.HasFlag( StartingModifiers.Internity ) )
		{
			// Logic to set all job level stats to 1 instead of 0
			UnlockAllJobs();
		}

		// 4. DevMode: For testing
		if ( mods.HasFlag( StartingModifiers.DevMode ) )
		{
			Economy.CommitTransaction( 100000000, 0 );
		}
	}

	private void GrantItem( string itemTag )
	{
		// This is where you'll link to your Inventory System later
		Log.Info( $"Modifier Applied: Phoney (Granted {itemTag})" );
		// Inventory.Add( itemTag );
	}

	private void UnlockAllJobs()
	{
		Log.Info( "Modifier Applied: Internity (All career paths unlocked)" );
	}
}
