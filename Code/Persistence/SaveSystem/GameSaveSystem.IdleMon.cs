using Sandbox;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sinvest;

/// <summary>
/// IdleMon Persistence & Key-Value Ledger Extensions.
/// This partial provides a flexible way to store monster stats and strings 
/// by appending keyed entries to the authoritative ledger.
/// </summary>
public sealed partial class GameSaveSystem
{
	/// <summary>
	/// Saves a numerical stat for an IdleMon (e.g., "Level", "XP", "Strength").
	/// This writes to the disk ledger and updates the RAM cache simultaneously.
	/// </summary>
	public void SetStoredStat( string key, double value )
	{
		AppendLedgerEntry( "IMON_STAT", key, value.ToString() );
        
		if ( CurrentCharacter != null )
		{
			CurrentCharacter.IdleMonStats[key] = value;
		}
	}

	/// <summary>
	/// Saves a string-based metadata point for an IdleMon (e.g., "Nickname", "Type").
	/// </summary>
	public void SetStoredString( string key, string value )
	{
		AppendLedgerEntry( "IMON_STR", key, value );

		if ( CurrentCharacter != null )
		{
			CurrentCharacter.IdleMonMetadata[key] = value;
		}
	}

	/// <summary>
	/// Retrieves a numerical stat from the session cache.
	/// Returns 0 if the key does not exist.
	/// </summary>
	public double GetStoredStat( string key )
	{
		if ( CurrentCharacter != null && CurrentCharacter.IdleMonStats.TryGetValue( key, out var val ) )
			return val;
		return 0;
	}

	/// <summary>
	/// Retrieves a string value from the session cache.
	/// Returns an empty string if the key does not exist.
	/// </summary>
	public string GetStoredString( string key )
	{
		if ( CurrentCharacter != null && CurrentCharacter.IdleMonMetadata.TryGetValue( key, out var val ) )
			return val;
		return string.Empty;
	}

	/// <summary>
	/// Internal helper to write a keyed entry to the end of the ledger file.
	/// Uses raw byte writing to remain compatible with s&box's FileSystem restrictions.
	/// Format: TYPE | KEY | VALUE | TIMESTAMP
	/// </summary>
	private void AppendLedgerEntry( string type, string key, string value )
	{
		try
		{
			var path = GetPath( ActiveSlot );
			var entry = $"{type}|{key}|{value}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

			// We use FileMode.Append to ensure we never overwrite previous history.
			// This preserves the 'Audit Trail' of the character's progress.
			using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
			{
				var bytes = Encoding.UTF8.GetBytes( entry );
				stream.Write( bytes, 0, bytes.Length );
			}
		}
		catch ( Exception e )
		{
			Log.Error( $"[SAVE] Failed to append {type} for key '{key}': {e.Message}" );
		}
	}
}
