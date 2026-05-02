using Sandbox;
using System;
using System.Linq;
using System.IO;

namespace Sinvest.Utility.Debug;

public sealed class StartupDebugger : Component
{
    [Property] public int TargetSlot { get; set; } = 3;

    [Button( "RUN HANG DIAGNOSTIC" )]
    public void RunDiagnostic()
    {
       Log.Info( $"--- STARTING HANG DIAGNOSTIC (SLOT {TargetSlot}) ---" );

       var gss = GameSaveSystem.Instance;
       var path = $"slot_{TargetSlot}.txt";

       // 1. Singleton & Lifecycle Integrity
       if ( gss == null )
       {
          Log.Error( "CRITICAL: GameSaveSystem.Instance is NULL. The 'Syncing' state cannot resolve." );
          return;
       }

       // Fixed: Checking Scene and GameObject properties correctly
       Log.Info( $"COMPONENT STATE: Active={this.Active}, Enabled={this.Enabled}, IsValid={this.IsValid}" );
       Log.Info( $"SCENE STATE: Name={Scene?.Name}, IsActive={GameObject?.Scene != null}" );

       // 2. GSS State
       Log.Info( $"GSS POINTER: ActiveSlot={gss.ActiveSlot}, CurrentCharName={gss.CurrentCharacter?.Name ?? "NULL"}" );

       // 3. FileSystem Content & Logic Check
       if ( !FileSystem.Data.FileExists( path ) )
       {
          Log.Warning( $"FILE SYSTEM: {path} not found. A new file will be created on save." );
       }
       else
       {
          string content = FileSystem.Data.ReadAllText( path );
          bool hasKit = content.Contains( "Starter Kit" );
          Log.Info( $"FILE CONTENT: Length={content.Length}, Has 'Starter Kit'={hasKit}" );
       }

       // 4. Character Data & Modifiers
       if ( gss.CurrentCharacter != null )
       {
          var charData = gss.CurrentCharacter;
          Log.Info( $"RAM DATA: {charData.Name} | ${charData.Money} | {charData.Shares} Shares" );
          Log.Info( $"MODIFIERS: {charData.Modifiers}" );
       }
       else
       {
          Log.Error( "RAM DATA: CurrentCharacter is NULL. This is why the UI hangs on 'Syncing'." );
       }

       // 5. Dependency Check (Fixed to match your class names)
       var economy = Scene.GetAllComponents<EconomyManager>().FirstOrDefault();
       var startup = Scene.GetAllComponents<PlayerStartupManager>().FirstOrDefault();

       Log.Info( $"DEPENDENCY: EconomyManager={(economy != null ? "FOUND" : "MISSING")}" );
       Log.Info( $"DEPENDENCY: PlayerStartupManager={(startup != null ? "FOUND" : "MISSING")}" );

       if ( startup != null && !startup.Enabled )
          Log.Warning( "WARNING: PlayerStartupManager is disabled on the player object." );

       Log.Info( "--- DIAGNOSTIC COMPLETE ---" );
    }
}
