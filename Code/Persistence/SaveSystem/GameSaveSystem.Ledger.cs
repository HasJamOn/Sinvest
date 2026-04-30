using Sandbox;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

public partial class GameSaveSystem
{
    public TransactionResult CommitTransaction( string type, double amount, string note = "" )
    {
       if ( amount <= 0 ) return TransactionResult.SystemError;
       
       // Calculate current balance check
       if ( type == "OUT" && CurrentCharacter.Money < amount ) 
           return TransactionResult.InsufficientFunds;

       try
       {
          var path = GetPath( ActiveSlot );
          var entry = $"{type}|{amount}|{note}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

          // FIX: Use FileMode.Append instead of a boolean
          using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
          using ( var writer = new StreamWriter( stream ) )
          {
             writer.Write( entry );
          }

          if ( type == "IN" ) CurrentCharacter.Money += amount;
          else if ( type == "OUT" ) CurrentCharacter.Money -= amount;

          return TransactionResult.Success;
       }
       catch { return TransactionResult.SystemError; }
    }
    
    public async Task SaveActiveSlotAsync()
    {
	    var path = GetPath( ActiveSlot );
        
	    // Prepare the Meta header
	    var metaEntry = $"META|{CurrentCharacter.Name}|{CurrentCharacter.Modifiers}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

	    // If it's a new file, we write META first. 
	    // For a simple authoritative ledger, we can overwrite the whole file 
	    // or just append. Let's ensure META is at the top or present.
	    if ( !FileSystem.Data.FileExists( path ) )
	    {
		    FileSystem.Data.WriteAllText( path, metaEntry );
            
		    // If they started with money (DevMode), log the initial transaction
		    if ( CurrentCharacter.Money > 0 )
		    {
			    CommitTransaction( "IN", CurrentCharacter.Money, "Initial Balance" );
		    }
	    }

	    // Simulate async work for the UI "Processing" state
	    await Task.Delay( 100 ); 
    }

    public void LoadActiveSlot()
    {
       var path = GetPath( ActiveSlot );
       CurrentCharacter = new CharacterSession { Name = "New Character", Money = 0 };

       if ( !FileSystem.Data.FileExists( path ) ) return;

       var lines = FileSystem.Data.ReadAllText( path ).Split( '\n' );
       foreach ( var line in lines )
       {
          var parts = line.Split( '|' );
          if ( parts.Length < 2 ) continue;

          if ( parts[0] == "META" && parts.Length >= 3 ) {
             CurrentCharacter.Name = parts[1];
             if ( Enum.TryParse<StartingModifiers>( parts[2], out var mod ) )
                CurrentCharacter.Modifiers = mod;
          }
          else if ( parts[0] == "IN" ) CurrentCharacter.Money += double.Parse( parts[1] );
          else if ( parts[0] == "OUT" ) CurrentCharacter.Money -= double.Parse( parts[1] );
       }
    }
}
