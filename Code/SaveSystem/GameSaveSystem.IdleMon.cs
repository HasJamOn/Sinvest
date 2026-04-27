using Sandbox;
using System.Collections.Generic;

namespace Sinvest;

public sealed partial class GameSaveSystem
{
	/// <summary>
	/// IdleMon Bridge: Saves a string (like JSON) to the current save slot.
	/// </summary>
	public void SetStoredString( string key, string val )
	{
		string fileName = $"{key}{SlotSuffix}.json";
		FileSystem.Data.WriteAllText( fileName, val ); // Save to actual local file
		OnDataChanged?.Invoke();
	}

	/// <summary>
	/// IdleMon Bridge: Retrieves a string from the current save slot.
	/// </summary>
	public string GetStoredString( string key, string defaultVal = "" )
	{
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
