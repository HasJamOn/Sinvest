using Sandbox;
using System.Linq;

namespace Sinvest;

/// <summary>
/// This partial provides utility methods for inspecting save files on the disk.
/// These methods are "Read-Only" and do not modify the current active session, 
/// making them ideal for populating Save/Load menus.
/// </summary>
public partial class GameSaveSystem
{
    /// <summary>
    /// Scans a specific save slot to extract the character's name from the META header.
    /// Returns a placeholder string if the file is missing or corrupted.
    /// </summary>
    public string GetStoredNameForSlot( int slot )
    {
       var path = GetPath( slot );
       if ( !FileSystem.Data.FileExists( path ) ) return "Empty Slot";

       // Read the entire file and look for the first line starting with the META tag
       var content = FileSystem.Data.ReadAllText( path );
       var metaLine = content.Split( '\n' ).FirstOrDefault( l => l.StartsWith( "META" ) );
        
       if ( string.IsNullOrEmpty( metaLine ) ) return "Unknown Character";

       // Split the META line (Format: META|Name|Modifiers|Timestamp)
       var parts = metaLine.Split( '|' );
       return parts.Length > 1 ? parts[1] : "Unknown";
    }

    /// <summary>
    /// Performs a lightweight "Dry Run" of the ledger replay for a specific slot.
    /// This calculates totals (like Money) for display purposes without changing the active game state.
    /// </summary>
    /// <param name="slot">The file slot index to inspect.</param>
    /// <param name="stat">The identifier for the data needed (e.g., "money" or "active").</param>
    public double GetStoredStatForSlot( int slot, string stat )
    {
       var path = GetPath( slot );
       if ( !FileSystem.Data.FileExists( path ) ) return 0;

       double balance = 0;
       var lines = FileSystem.Data.ReadAllText( path ).Split( '\n' );
        
       // Replay the financial entries in the file to determine the current balance
       foreach ( var line in lines )
       {
          var parts = line.Split( '|' );
          if ( parts.Length < 2 ) continue;

          // Standard Inflow/Outflow calculation
          if ( parts[0] == "IN" ) balance += double.Parse( parts[1] );
          if ( parts[0] == "OUT" ) balance -= double.Parse( parts[1] );
       }

       // Return the calculated cash balance
       if ( stat == "money" ) return balance;
        
       // Use "active" to verify if the file contains a valid identity header
       if ( stat == "active" ) return lines.Any( l => l.StartsWith( "META" ) ) ? 1 : 0;
        
       return 0;
    }
}
