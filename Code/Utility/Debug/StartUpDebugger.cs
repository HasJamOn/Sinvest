using Sandbox;
using Sandbox.UI;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest.Utility.Debug;

public sealed class StartupDebugger : Component
{
    [Property] public int TestSlot { get; set; } = 99;

    [Button( "Execute Startup Test" )]
    public void RunFullSequenceTest()
    {
        Log.Info( "--- STARTING INTEGRATED STARTUP TEST ---" );
        
        var gss = GameSaveSystem.Instance;
        if ( !gss.IsValid() )
        {
            Log.Error( "[DEBUG] GameSaveSystem Instance not found!" );
            return;
        }

        // 1. SETUP CLEAN STATE
        gss.ActiveSlot = TestSlot;
        gss.DeleteSlot( TestSlot );
        Log.Info( $"[DEBUG] Slot {TestSlot} wiped." );

        // 2. SIMULATE NEW CHARACTER LOAD
        Log.Info( "[DEBUG] Triggering LoadActiveSlot (Creating fresh session)..." );
        gss.LoadActiveSlot(); 

        var sessionBefore = gss.CurrentCharacter;
        // Ensure we are testing with specific modifiers (None = should get starter kit)
        sessionBefore.Modifiers = StartingModifiers.None; 
        
        Log.Info( $"[DEBUG] Session ID: {sessionBefore.GetHashCode()} | Initial Money: {sessionBefore.Money}" );

        // 3. SIMULATE STARTUP MANAGER LOGIC
        Log.Info( "[DEBUG] Applying Starter Kit Logic via Transactions..." );
        
        if ( !sessionBefore.Modifiers.HasFlag( StartingModifiers.Destitute ) )
        {
            // Testing the new persistent transaction methods
            var resMoney = gss.CommitMoneyTransaction( "IN", 1000, "Starter Kit: Cash" );
            var resShares = gss.CommitShareTransaction( 1, "Starter Kit: Share" );
            
            Log.Info( $"[DEBUG] Money Result: {resMoney} | Share Result: {resShares}" );
        }

        // 4. VERIFY FILE PERSISTENCE (THE LEDGER)
        string path = $"slot_{TestSlot}.txt";
        if ( FileSystem.Data.FileExists( path ) )
        {
            var content = FileSystem.Data.ReadAllText( path );
            var lines = content.Split( '\n' ).Where( l => !string.IsNullOrWhiteSpace( l ) ).ToList();
            
            Log.Info( $"[DEBUG] Ledger file '{path}' contains {lines.Count} entries:" );
            foreach( var line in lines )
            {
                Log.Info( $" >> {line}" );
            }
        }
        else
        {
            Log.Error( "[DEBUG] FAILURE: Ledger file was never created on disk!" );
        }

        // 5. THE "RELOAD" TEST (Persistence Verification)
        Log.Info( "[DEBUG] Simulating Re-Load (Clearing RAM to parse file)..." );
        gss.LoadActiveSlot();
        
        Log.Info( $"[DEBUG] Final Balance after Parse: {gss.CurrentCharacter.Money}" );
        Log.Info( $"[DEBUG] Final Shares after Parse: {gss.CurrentCharacter.Shares}" );

        // Final Validation
        bool moneyValid = Math.Abs( gss.CurrentCharacter.Money - 1000 ) < 0.001;
        bool sharesValid = Math.Abs( gss.CurrentCharacter.Shares - 1 ) < 0.001;

        if ( moneyValid && sharesValid )
        {
            Log.Info( "--- TEST SUCCESSFUL: Data persisted through reload ---" );
        }
        else
        {
            Log.Error( $"--- TEST FAILED ---" );
            if ( !moneyValid ) Log.Error( $"Expected Money: 1000, Got: {gss.CurrentCharacter.Money}" );
            if ( !sharesValid ) Log.Error( $"Expected Shares: 1, Got: {gss.CurrentCharacter.Shares}" );
        }
    }
}
