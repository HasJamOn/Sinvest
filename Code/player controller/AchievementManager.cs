using Sandbox;
using Sandbox.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

public sealed class AchievementManager : Component
{
	[Property] public bool DebugLogUnlocks { get; set; } = true;

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		// Hook into the Save System's data event
		GameSaveSystem.OnDataChanged += OnStatsUpdated;
        
		Log.Info( "[ACHIEVEMENTS] Manager initialized on Player." );
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

		if ( save.ShouldUseCloud )
		{
			// According to the decompiled source, we can check 'All' 
			// to see if it's already unlocked before firing the RPC/Service call.
			var ach = Achievements.All.FirstOrDefault( x => x.Name == achievementId );
            
			if ( ach != null && !ach.IsUnlocked )
			{
				// Call the static Unlock method from the decompiled source
				Achievements.Unlock( achievementId );
                
				if ( DebugLogUnlocks ) 
					Log.Info( $"[ACHIEVEMENT] Cloud Unlock Triggered: {achievementId}" );
			}
		}
		else
		{
			// Local fallback for dev/offline mode
			save.DebugUnlockAchievement( achievementId );
		}
	}
}
