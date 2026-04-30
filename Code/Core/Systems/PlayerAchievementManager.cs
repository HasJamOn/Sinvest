using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public sealed class PlayerAchievementManager : Component
{
	[Property] public bool DebugLogUnlocks { get; set; } = true;

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		// Hook into the Save System's data event
		GameSaveSystem.OnDataChanged += OnStatsUpdated;
        
		Log.Info( "[ACHIEVEMENTS] Manager initialized locally." );
	}

	protected override void OnDestroy()
	{
		GameSaveSystem.OnDataChanged -= OnStatsUpdated;
	}

	private void OnStatsUpdated()
	{
		var save = GameSaveSystem.Instance;
		if ( save?.CurrentCharacter == null ) return;

		// --- ROCK BOTTOM CHECK ---
		// Condition: Exactly 0 money and 0 shares
		if ( save.CurrentCharacter.Money <= 0.01 && save.CurrentCharacter.Shares <= 0 )
		{
			TryUnlock( "rock_bottom" );
		}
	}

	public void TryUnlock( string achievementId )
	{
		var save = GameSaveSystem.Instance;
		if ( save == null ) return;

		// Check if already unlocked this session to prevent spamming the ledger
		if ( !save.UnlockedAchievements.Contains( achievementId ) )
		{
			save.LocalUnlockAchievement( achievementId );
            
			if ( DebugLogUnlocks ) 
				Log.Info( $"[ACHIEVEMENT] Local Unlock Triggered: {achievementId}" );
		}
	}
}
