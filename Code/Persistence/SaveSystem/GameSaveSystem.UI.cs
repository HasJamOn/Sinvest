using Sandbox;
using System.Linq;

namespace Sinvest;

public partial class GameSaveSystem
{
	/// <summary>
	/// Peeks into a save file to get the character name without loading the slot into memory.
	/// </summary>
	public string GetStoredNameForSlot( int slot )
	{
		var path = GetPath( slot );
		if ( !FileSystem.Data.FileExists( path ) ) return "Empty Slot";

		var content = FileSystem.Data.ReadAllText( path );
		var metaLine = content.Split( '\n' ).FirstOrDefault( l => l.StartsWith( "META" ) );
        
		if ( string.IsNullOrEmpty( metaLine ) ) return "Unknown Character";

		var parts = metaLine.Split( '|' );
		return parts.Length > 1 ? parts[1] : "Unknown";
	}

	/// <summary>
	/// Calculates a specific stat (like total balance) from the ledger for UI display.
	/// </summary>
	public double GetStoredStatForSlot( int slot, string stat )
	{
		var path = GetPath( slot );
		if ( !FileSystem.Data.FileExists( path ) ) return 0;

		double balance = 0;
		var lines = FileSystem.Data.ReadAllText( path ).Split( '\n' );
        
		// Quick scan of the ledger to calculate balance
		foreach ( var line in lines )
		{
			var parts = line.Split( '|' );
			if ( parts.Length < 2 ) continue;

			if ( parts[0] == "IN" ) balance += double.Parse( parts[1] );
			if ( parts[0] == "OUT" ) balance -= double.Parse( parts[1] );
		}

		if ( stat == "money" ) return balance;
        
		// "active" just checks if the file has a META header
		if ( stat == "active" ) return lines.Any( l => l.StartsWith( "META" ) ) ? 1 : 0;
        
		return 0;
	}
}
