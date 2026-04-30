using System;
using System.Collections.Generic; // Added for HashSet
using Sandbox; // Ensure Sandbox namespace is used for FileSystem

namespace Sinvest;

public sealed partial class GameSaveSystem : Component
{
	public static GameSaveSystem Instance { get; private set; }

	[Property] public int ActiveSlot { get; set; } = 1;
	public CharacterSession CurrentCharacter { get; private set; } = new();
	[Property] public SceneFile WorldScene { get; set; }
    
	[Property, Group("Phase 2")] public bool ShouldUseCloud { get; set; } = false;
    
	public static System.Action OnDataChanged { get; set; }

	public void NotifyDataChanged() => OnDataChanged?.Invoke();

	public HashSet<string> UnlockedAchievements { get; private set; } = new();
    
	// Note: Ensure CharacterSession is defined elsewhere in your project
	// public CharacterSession CurrentCharacter { get; private set; } = new();
    
	private string GetPath( int slot ) => $"slot_{slot}.txt";

	protected override void OnAwake()
	{
		Instance = this;
	}

	public void DeleteSlot( int slot )
	{
		var path = GetPath( slot );
		if ( FileSystem.Data.FileExists( path ) )
		{
			FileSystem.Data.DeleteFile( path );
			Log.Info( $"Deleted save file: {path}" );
		}
	}
    
	public void LocalUnlockAchievement( string id )
	{
		if ( UnlockedAchievements.Add( id ) )
		{
			var path = GetPath( ActiveSlot );
			var entry = $"ACH|{id}|0|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";
            
			// Read existing text (returns empty string if file doesn't exist)
			string existingData = FileSystem.Data.ReadAllText( path ) ?? "";
        
			// Write it all back
			FileSystem.Data.WriteAllText( path, existingData + entry );
            
			Log.Info( $"[ACHIEVEMENT] Locally Unlocked: {id}" );
		}
	}
}
