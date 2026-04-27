using Sandbox;
using System.Collections.Generic;

namespace Sinvest;

public sealed partial class GameSaveSystem
{
	/// <summary>
	/// IdleMon Bridge: Saves a string to the Cloud Data service.
	/// </summary>
	public void SetStoredString( string key, string val )
	{
		// 1. Local Backup (Always do this)
		string fileName = $"{key}{SlotSuffix}.json";
		FileSystem.Data.WriteAllText( fileName, val );

		// 2. Cloud Sync via Cookies
		// S&box Cookies are automatically synced to the cloud by Steam.
		if ( ShouldUseCloud )
		{
			string cookieKey = $"{key}{SlotSuffix}";
			Game.Cookies.Set( cookieKey, val );
			Log.Info( $"[SAVE] {cookieKey} synced to Cloud Cookies." );
		}

		OnDataChanged?.Invoke();
	}

	/// <summary>
	/// IdleMon Bridge: Retrieves a string from the Cloud (falls back to local).
	/// </summary>
	public string GetStoredString( string key, string defaultVal = "" )
	{
		// 1. Try Cloud Cookies first if enabled
		if ( ShouldUseCloud )
		{
			string cookieKey = $"{key}{SlotSuffix}";
			string cloudVal = Game.Cookies.Get( cookieKey, "" );
        
			if ( !string.IsNullOrEmpty( cloudVal ) )
			{
				return cloudVal;
			}
		}

		// 2. Fallback to local file
		string fileName = $"{key}{SlotSuffix}.json";
		if ( !FileSystem.Data.FileExists( fileName ) ) return defaultVal;
    
		return FileSystem.Data.ReadAllText( fileName );
	}

	/// <summary>
	/// Clears all IdleMon-related data for the current slot.
	/// Use this for debugging fresh starts.
	/// </summary>
	public void ClearIdleMonData()
	{
		string[] keysToClear = { 
			"idlemon_roster", 
			"idlemon_bucks", 
			"idlemon_last_timestamp" 
		};

		foreach ( var key in keysToClear )
		{
			string slotKey = $"{key}{SlotSuffix}";
			// In S&Box Cookies, setting to null or empty effectively clears it
			Game.Cookies.Set( slotKey, "" );
		}

		Log.Info( $"[SAVE] IdleMon data wiped for slot: {ActiveSlot}" );
		OnDataChanged?.Invoke();
	}
}
