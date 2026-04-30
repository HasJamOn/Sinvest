using System;
using System.IO;

namespace Sinvest;

public sealed partial class GameSaveSystem : Component
{
	public static GameSaveSystem Instance { get; private set; }

	[Property] public int ActiveSlot { get; set; } = 1;
	[Property] public SceneFile WorldScene { get; set; }
	
	[Property, Group("Phase 2")] public bool ShouldUseCloud { get; set; } = false;
	
	// Action to notify systems like achievements when money/shares change
	public static System.Action OnDataChanged { get; set; }

	public void NotifyDataChanged() => OnDataChanged?.Invoke();

	// Local achievement tracking for the current session
	public HashSet<string> UnlockedAchievements { get; private set; } = new();
    
	public CharacterSession CurrentCharacter { get; private set; } = new();
    
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
			// In Phase 1, you can append this to your ledger file
			var path = GetPath( ActiveSlot );
			var entry = $"ACH|{id}|0|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";
            
			using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
			using ( var writer = new StreamWriter( stream ) )
			{
				writer.Write( entry );
			}
            
			Log.Info( $"[ACHIEVEMENT] Locally Unlocked: {id}" );
		}
	}
}
