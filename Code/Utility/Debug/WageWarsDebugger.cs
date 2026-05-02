using Sandbox;
using System;
using System.Linq;
using System.IO;

namespace Sinvest.Utility.Debug;

public sealed class WageWarsDebugger : Component
{
    private const string GlobalWorldPath = "wagewars_global_world.json";

    [Button( "DIAGNOSE WAGEWARS ACTIVITY" )]
    public void RunDiagnostic()
    {
       Log.Info( "--- STARTING WAGEWARS ACTIVITY REPORT ---" );

       // 1. Singleton Check
       var wwSave = WageWarsWorldSave.Instance;
       if ( wwSave == null )
       {
          Log.Error( "WWS: WageWarsWorldSave.Instance is NULL. Ensure the component is active in the scene!" );
          return;
       }

       // 2. Host Authority Check
       // If this is False, SaveWorld() exits before doing anything.
       Log.Info( $"NETWORKING: IsHost={Networking.IsHost}" );
       if ( !Networking.IsHost )
       {
          Log.Warning( "NETWORKING ALERT: You are a Client. You cannot write the global world file." );
       }

       // 3. Data Activity Census
       var allCubes = Scene.GetAllComponents<WageWarsCube>().ToList();
       var candidateCubes = allCubes.Where( c => c.Value > 0 && c.OwnerSteamId != 0 ).ToList();
       
       // Check for "Invalid" activity (Value set but no SteamId)
       var brokenCubes = allCubes.Where( c => c.Value > 0 && c.OwnerSteamId == 0 ).ToList();

       Log.Info( $"CUBE ACTIVITY: Total={allCubes.Count} | Saveable={candidateCubes.Count} | Broken={brokenCubes.Count}" );

       if ( brokenCubes.Count > 0 )
       {
          Log.Error( $"LOGIC ERROR: {brokenCubes.Count} cubes have Value but NO SteamId. They will be ignored by SaveWorld." );
          Log.Info( $"BROKEN SAMPLE: Grid={brokenCubes[0].GridPosition} | Value={brokenCubes[0].Value}" );
       }

       if ( candidateCubes.Count > 0 )
       {
          Log.Info( $"ACTIVE SAMPLE: Grid={candidateCubes[0].GridPosition} | Owner={candidateCubes[0].OwnerSteamId} | Value={candidateCubes[0].Value}" );
       }

       // 4. FileSystem IO Status
       bool fileExists = FileSystem.Data.FileExists( GlobalWorldPath );
       Log.Info( $"FILESYSTEM: {GlobalWorldPath} Exists={fileExists}" );

       if ( fileExists )
       {
          string content = FileSystem.Data.ReadAllText( GlobalWorldPath );
          Log.Info( $"FILESYSTEM: File Size={content.Length} bytes." );
       }

       // 5. Trigger Test
       if ( Networking.IsHost && candidateCubes.Count > 0 )
       {
          Log.Info( "ACTION: Manually triggering SaveWorld now..." );
          wwSave.SaveWorld();
          
          if ( FileSystem.Data.FileExists( GlobalWorldPath ) )
             Log.Info( "RESULT: Save successful. File is now on disk." );
          else
             Log.Error( "RESULT: Save failed. File still missing after manual trigger." );
       }

       Log.Info( "--- DIAGNOSTIC COMPLETE ---" );
    }
}
