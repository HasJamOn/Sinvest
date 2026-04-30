using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text; // Required for Encoding

namespace Sinvest;

public sealed partial class GameSaveSystem
{
    public TransactionResult CommitTransaction( string type, double amount, string note = "" )
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
    
    public async Task SaveActiveSlotAsync()
    {
        var path = GetPath( ActiveSlot );
        var metaEntry = $"META|{CurrentCharacter.Name}|{CurrentCharacter.Modifiers}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}\n";

        if ( !FileSystem.Data.FileExists( path ) )
        {
           FileSystem.Data.WriteAllText( path, metaEntry );
            
           if ( CurrentCharacter.Money > 0 )
           {
              CommitTransaction( "IN", CurrentCharacter.Money, "Initial Balance" );
           }
        }

        await Task.Delay( 100 ); 
    }

    public void LoadActiveSlot()
    {
       var path = GetPath( ActiveSlot );
       // Ensure CurrentCharacter is initialized
       CurrentCharacter = new CharacterSession { Name = "New Character", Money = 0 };

       if ( !FileSystem.Data.FileExists( path ) ) return;

       var content = FileSystem.Data.ReadAllText( path );
       if ( string.IsNullOrWhiteSpace( content ) ) return;

       var lines = content.Split( '\n' );
       foreach ( var line in lines )
       {
          if ( string.IsNullOrWhiteSpace( line ) ) continue;

          var parts = line.Split( '|' );
          if ( parts.Length < 2 ) continue;

          // Standard Ledger Processing
          if ( parts[0] == "META" && parts.Length >= 3 ) {
             CurrentCharacter.Name = parts[1];
             if ( Enum.TryParse<StartingModifiers>( parts[2], out var mod ) )
                CurrentCharacter.Modifiers = mod;
          }
          else if ( parts[0] == "IN" ) CurrentCharacter.Money += double.Parse( parts[1] );
          else if ( parts[0] == "OUT" ) CurrentCharacter.Money -= double.Parse( parts[1] );
          else if ( parts[0] == "SHARE_BUY" ) CurrentCharacter.Shares += double.Parse( parts[1] );
          else if ( parts[0] == "SHARE_SELL" ) CurrentCharacter.Shares -= double.Parse( parts[1] );
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
    }
}
