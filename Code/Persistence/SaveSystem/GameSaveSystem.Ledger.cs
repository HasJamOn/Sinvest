using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

namespace Sinvest;

/// <summary>
/// This partial implements the transactional logic of the ledger.
/// It follows an Event Sourcing pattern: rather than storing a final balance, 
/// it records every individual change to Money, Shares, and Items.
/// </summary>
public sealed partial class GameSaveSystem
{
    /// <summary>
    /// Validates and records a cash transaction (IN/OUT).
    /// </summary>
    public TransactionResult CommitMoneyTransaction( string type, double amount, string note = "" )
    {
       if ( amount <= 0 ) return TransactionResult.SystemError;
    
       // Check for sufficient funds locally before committing to the file
       if ( type == "OUT" && CurrentCharacter.Money < amount ) 
          return TransactionResult.InsufficientFunds;

       try
       {
          var path = GetPath( ActiveSlot );
          var entry = $"{type}|{amount}|{note}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

          // Use raw stream writing to bypass s&box whitelisting restrictions on StreamWriter
          using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
          {
             var bytes = Encoding.UTF8.GetBytes( entry );
             stream.Write( bytes, 0, bytes.Length );
          }

          // Mirror the change in the RAM session for immediate UI feedback
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
    
    /// <summary>
    /// Records the purchase or sale of Fundino shares.
    /// Uses absolute values for the ledger entry and handles the math via specific tags.
    /// </summary>
    public TransactionResult CommitShareTransaction( double amount, string note = "" )
    {
        if ( amount == 0 ) return TransactionResult.SystemError;

        try
        {
           var path = GetPath( ActiveSlot );
           var type = amount > 0 ? "SHARE_BUY" : "SHARE_SELL";
           var entry = $"{type}|{Math.Abs(amount)}|{note}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

           using ( var stream = FileSystem.Data.OpenWrite( path, System.IO.FileMode.Append ) )
           {
              var bytes = System.Text.Encoding.UTF8.GetBytes( entry );
              stream.Write( bytes, 0, bytes.Length );
           }

           CurrentCharacter.Shares += amount;

           NotifyDataChanged();
           return TransactionResult.Success;
        }
        catch 
        { 
           return TransactionResult.SystemError; 
        }
    }
    
    /// <summary>
    /// Ensures the file exists and contains the META header (Character Name and Modifiers).
    /// </summary>
    public async Task SaveActiveSlotAsync()
    {
        var path = GetPath( ActiveSlot );
    
        if ( !FileSystem.Data.FileExists( path ) )
        {
           // Store Modifiers as an integer bitmask for easy parsing
           var metaEntry = $"META|{CurrentCharacter.Name}|{(int)CurrentCharacter.Modifiers}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";
       
           FileSystem.Data.WriteAllText( path, metaEntry );
       
           Log.Info( $"[SAVE] Created new ledger header for {CurrentCharacter.Name} in Slot {ActiveSlot}" );
        }

        await Task.Delay( 100 ); 
    }

    /// <summary>
    /// Reads the entire ledger file and replays every event to build the current state.
    /// This is the "Source of Truth" for Balance, Shares, and Progression.
    /// </summary>
    public void LoadActiveSlot()
    {
       var path = GetPath( ActiveSlot );
       
       // Reset to a clean state before replaying the history
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

          // Reconstruct session data based on ledger tags
          if ( parts[0] == "META" && parts.Length >= 3 ) 
          {
             CurrentCharacter.Name = parts[1];
             if ( Enum.TryParse<StartingModifiers>( parts[2], out var mod ) )
                CurrentCharacter.Modifiers = mod;
          }
          else if ( parts[0] == "IN" ) CurrentCharacter.Money += double.Parse( parts[1] );
          else if ( parts[0] == "OUT" ) CurrentCharacter.Money -= double.Parse( parts[1] );
          else if ( parts[0] == "SHARE_BUY" ) CurrentCharacter.Shares += double.Parse( parts[1] );
          else if ( parts[0] == "SHARE_SELL" ) CurrentCharacter.Shares -= double.Parse( parts[1] );
          else if ( parts[0] == "ITEM" ) 
          {
             CurrentCharacter.UnlockedItems.Add( parts[1] );
          }
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

    /// <summary>
    /// Records a permanent item unlock in the ledger.
    /// </summary>
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

          CurrentCharacter.UnlockedItems.Add( itemSlug );
          NotifyDataChanged();
       }
       catch ( Exception e )
       {
          Log.Error( $"[SAVE] Failed to commit item: {e.Message}" );
       }
    }
}
