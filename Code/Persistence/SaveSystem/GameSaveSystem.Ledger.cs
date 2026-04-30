using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text; // Required for Encoding

namespace Sinvest;

public sealed partial class GameSaveSystem
{
    public TransactionResult CommitMoneyTransaction( string type, double amount, string note = "" )
    {
       if ( amount <= 0 ) return TransactionResult.SystemError;
    
       if ( type == "OUT" && CurrentCharacter.Money < amount ) 
          return TransactionResult.InsufficientFunds;

       try
       {
          var path = GetPath( ActiveSlot );
          var entry = $"{type}|{amount}|{note}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

          // FIX: Use raw stream writing to bypass the StreamWriter whitelist error
          using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
          {
             var bytes = Encoding.UTF8.GetBytes( entry );
             stream.Write( bytes, 0, bytes.Length );
          }

          if ( type == "IN" ) CurrentCharacter.Money += amount;
          else if ( type == "OUT" ) CurrentCharacter.Money -= amount;

          NotifyDataChanged();

          return TransactionResult.Success;
       }
       catch 
       { 
          return TransactionResult.SystemError; 
       }
    }
    
    public TransactionResult CommitShareTransaction( double amount, string note = "" )
    {
	    if ( amount == 0 ) return TransactionResult.SystemError;

	    try
	    {
		    var path = GetPath( ActiveSlot );
		    // Determine the ledger tag based on positive/negative amount
		    var type = amount > 0 ? "SHARE_BUY" : "SHARE_SELL";
		    var entry = $"{type}|{Math.Abs(amount)}|{note}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

		    // Write to the ledger file
		    using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
		    {
			    var bytes = System.Text.Encoding.UTF8.GetBytes( entry );
			    stream.Write( bytes, 0, bytes.Length );
		    }

		    // Update the live session variable
		    CurrentCharacter.Shares += amount;

		    NotifyDataChanged();
		    return TransactionResult.Success;
	    }
	    catch 
	    { 
		    return TransactionResult.SystemError; 
	    }
    }
    
    public async Task SaveActiveSlotAsync()
    {
	    var path = GetPath( ActiveSlot );
    
	    // We only write the META tag if the file is brand new.
	    // We no longer inject money here; we let StartupManager handle transactions.
	    if ( !FileSystem.Data.FileExists( path ) )
	    {
		    // Cast Modifiers to int to ensure the enum stores correctly in plaintext
		    var metaEntry = $"META|{CurrentCharacter.Name}|{(int)CurrentCharacter.Modifiers}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";
       
		    FileSystem.Data.WriteAllText( path, metaEntry );
       
		    Log.Info( $"[SAVE] Created new ledger header for {CurrentCharacter.Name} in Slot {ActiveSlot}" );
	    }

	    // Small delay to ensure FileSystem IO has a moment to breathe
	    await Task.Delay( 100 ); 
    }

    public void LoadActiveSlot()
	{
	   var path = GetPath( ActiveSlot );
	   
	   // Initialize fresh session in RAM
	   CurrentCharacter = new CharacterSession 
	   { 
	      Name = "New Character", 
	      Money = 0,
	      Shares = 0,
	      UnlockedItems = new System.Collections.Generic.HashSet<string>() 
	   };

	   if ( !FileSystem.Data.FileExists( path ) ) return;

	   var content = FileSystem.Data.ReadAllText( path );
	   if ( string.IsNullOrWhiteSpace( content ) ) return;

	   var lines = content.Split( '\n' );
	   foreach ( var line in lines )
	   {
	      if ( string.IsNullOrWhiteSpace( line ) ) continue;

	      var parts = line.Split( '|' );
	      if ( parts.Length < 2 ) continue;

	      // --- Meta & Identity ---
	      if ( parts[0] == "META" && parts.Length >= 3 ) 
	      {
	         CurrentCharacter.Name = parts[1];
	         if ( Enum.TryParse<StartingModifiers>( parts[2], out var mod ) )
	            CurrentCharacter.Modifiers = mod;
	      }
	      
	      // --- Currency (Labor & Capital) ---
	      else if ( parts[0] == "IN" ) CurrentCharacter.Money += double.Parse( parts[1] );
	      else if ( parts[0] == "OUT" ) CurrentCharacter.Money -= double.Parse( parts[1] );
	      
	      // --- Investment (Equity) ---
	      else if ( parts[0] == "SHARE_BUY" ) CurrentCharacter.Shares += double.Parse( parts[1] );
	      else if ( parts[0] == "SHARE_SELL" ) CurrentCharacter.Shares -= double.Parse( parts[1] );
	      
	      // --- Persistence: Items & Upgrades ---
	      else if ( parts[0] == "ITEM" ) 
	      {
	         // Record item in the session's hashset
	         CurrentCharacter.UnlockedItems.Add( parts[1] );
	      }
	      
	      // --- Persistence: IdleMon Stats ---
	      else if ( parts[0] == "IMON_STAT" && parts.Length >= 3 ) 
	      {
	          if ( double.TryParse( parts[2], out var val ) )
	             CurrentCharacter.IdleMonStats[parts[1]] = val;
	      }
	      else if ( parts[0] == "IMON_STR" && parts.Length >= 3 ) 
	      {
	          CurrentCharacter.IdleMonMetadata[parts[1]] = parts[2];
	      }
	   }
	   
	   Log.Info( $"[SAVE] Loaded {CurrentCharacter.Name}: ${CurrentCharacter.Money}, Items: {CurrentCharacter.UnlockedItems.Count}" );
	}
	public void CommitItemTransaction( string itemSlug )
	{
		try
		{
			var path = GetPath( ActiveSlot );
			var entry = $"ITEM|{itemSlug}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

			using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
			{
				var bytes = System.Text.Encoding.UTF8.GetBytes( entry );
				stream.Write( bytes, 0, bytes.Length );
			}

			// Update the live session and notify UI
			CurrentCharacter.UnlockedItems.Add( itemSlug );
			NotifyDataChanged();
		}
		catch ( Exception e )
		{
			Log.Error( $"[SAVE] Failed to commit item: {e.Message}" );
		}
	}
}
