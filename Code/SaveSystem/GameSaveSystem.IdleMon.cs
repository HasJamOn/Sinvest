using Sandbox;
using System.Collections.Generic;

namespace Sinvest;

// We use 'partial' so this file effectively 'merges' into your main GameSaveSystem
public sealed partial class GameSaveSystem
{
	/// <summary>
	/// IdleMon Bridge: Saves a string (like JSON) to the current save slot.
	/// </summary>
	public void SetStoredString( string key, string val )
	{
		string slotKey = $"{key}{SlotSuffix}";
    
		// We use Game.Cookies here because it writes to your PC's storage.
		// _localCookies only lives as long as the game is running.
		Game.Cookies.Set( slotKey, val );

		OnDataChanged?.Invoke();
	}

	/// <summary>
	/// IdleMon Bridge: Retrieves a string from the current save slot.
	/// </summary>
	public string GetStoredString( string key, string defaultVal = "" )
	{
		string slotKey = $"{key}{SlotSuffix}";
    
		// Always check the actual file on the hard drive
		return Game.Cookies.Get( slotKey, defaultVal );
	}
}
