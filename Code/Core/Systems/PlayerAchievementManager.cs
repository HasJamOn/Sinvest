using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

/// <summary>
/// Monitors the active session for achievement triggers and manages the local unlock flow.
/// </summary>
public sealed class PlayerAchievementManager : Component
{
	[Property] public bool DebugLogUnlocks { get; set; } = true;

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		// Subscribe to the global data event to check conditions whenever the ledger changes.
		GameSaveSystem.OnDataChanged += OnStatsUpdated;
        
		Log.Info( "[ACHIEVEMENTS] Manager initialized locally." );
	}

	protected override void OnDestroy()
	{
		// Cleanup subscription to prevent memory leaks or calling on destroyed objects.
		GameSaveSystem.OnDataChanged -= OnStatsUpdated;
	}

	/// <summary>
	/// Event handler called whenever Money, Shares, or Stats are updated.
	/// </summary>
	private void OnStatsUpdated()
	{
		var save = GameSaveSystem.Instance;
		if ( save?.CurrentCharacter == null ) return;

		// Achievement: "Rock Bottom"
		// Condition: Player has effectively zero liquid and equity assets.
		if ( save.CurrentCharacter.Money <= 0.01 && save.CurrentCharacter.Shares <= 0 )
		{
			TryUnlock( "rock_bottom" );
		}
	}

	/// <summary>
	/// Validates the unlock against the current session cache before committing to the ledger.
	/// </summary>
	public void TryUnlock( string achievementId )
	{
		var save = GameSaveSystem.Instance;
		if ( save == null ) return;

		// Prevent duplicate "ACH" entries in the plaintext ledger.
		if ( !save.UnlockedAchievements.Contains( achievementId ) )
		{
			save.LocalUnlockAchievement( achievementId );
            
			if ( DebugLogUnlocks ) 
				Log.Info( $"[ACHIEVEMENT] Local Unlock Triggered: {achievementId}" );
		}
	}
}
