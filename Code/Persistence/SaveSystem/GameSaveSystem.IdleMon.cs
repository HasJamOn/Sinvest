using Sandbox;
using System;
using System.Collections.Generic;
using System.Text; // Required for Encoding

namespace Sinvest;

// Must match the main file exactly
public sealed partial class GameSaveSystem
{
	public void SetStoredStat( string key, double value )
	{
		AppendLedgerEntry( "IMON_STAT", key, value.ToString() );
        
		if ( CurrentCharacter != null )
		{
			CurrentCharacter.IdleMonStats[key] = value;
		}
	}

	public void SetStoredString( string key, string value )
	{
		AppendLedgerEntry( "IMON_STR", key, value );

		if ( CurrentCharacter != null )
		{
			CurrentCharacter.IdleMonMetadata[key] = value;
		}
	}

	public double GetStoredStat( string key )
	{
		if ( CurrentCharacter != null && CurrentCharacter.IdleMonStats.TryGetValue( key, out var val ) )
			return val;
		return 0;
	}

	public string GetStoredString( string key )
	{
		if ( CurrentCharacter != null && CurrentCharacter.IdleMonMetadata.TryGetValue( key, out var val ) )
			return val;
		return string.Empty;
	}

	private void AppendLedgerEntry( string type, string key, string value )
	{
		try
		{
			var path = GetPath( ActiveSlot );
			var entry = $"{type}|{key}|{value}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

			// FIX: Removed StreamWriter to satisfy s&box whitelist
			using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
			{
				var bytes = Encoding.UTF8.GetBytes( entry );
				stream.Write( bytes, 0, bytes.Length );
			}
		}
		catch ( Exception e )
		{
			Log.Error( $"[SAVE] Failed to append {type}: {e.Message}" );
		}
	}
}
