using Sandbox;
using System;
using System.IO;
using System.Collections.Generic;

namespace Sinvest;

public partial class GameSaveSystem
{
    /// <summary>
    /// Writes a specific IdleMon data point to the authoritative ledger.
    /// Format: IMON_STAT|key|value|timestamp
    /// </summary>
    public void SetStoredStat( string key, double value )
    {
        AppendLedgerEntry( "IMON_STAT", key, value.ToString() );
        
        // Update the local session cache so RosterManager sees it immediately
        if ( CurrentCharacter != null )
        {
            CurrentCharacter.IdleMonStats[key] = value;
        }
    }

    /// <summary>
    /// Writes a specific IdleMon string (like JSON roster) to the ledger.
    /// Format: IMON_STR|key|value|timestamp
    /// </summary>
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

    /// <summary>
    /// Updates the security signature (Placeholder for your Phase 1 local validation)
    /// </summary>
    public void UpdateSecuritySignature()
    {
        // For Phase 1, we can append a integrity check or simply log the sync
        Log.Info( "[SAVE] Security signature updated for local ledger." );
    }

    private void AppendLedgerEntry( string type, string key, string value )
    {
        try
        {
            var path = GetPath( ActiveSlot );
            var entry = $"{type}|{key}|{value}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

            using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
            using ( var writer = new StreamWriter( stream ) )
            {
                writer.Write( entry );
            }
        }
        catch ( Exception e )
        {
            Log.Error( $"[SAVE] Failed to append {type}: {e.Message}" );
        }
    }
}
