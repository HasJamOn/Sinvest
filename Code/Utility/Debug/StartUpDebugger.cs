using Sandbox;
using System;
using System.Linq;
using System.IO;

namespace Sinvest.Utility.Debug;

public sealed class StartupDebugger : Component
{
	[Property] public int TargetSlot { get; set; } = 3;

	[Button( "RUN DIAGNOSTIC" )]
	public void RunDiagnostic()
	{
		Log.Info( $"--- STARTING STARTUP DIAGNOSTIC (SLOT {TargetSlot}) ---" );

		var gss = GameSaveSystem.Instance;
		var path = $"slot_{TargetSlot}.txt";

		// 1. Check Singleton Status
		if ( !gss.IsValid() )
		{
			Log.Error( "CRITICAL: GameSaveSystem.Instance is NULL. Polling will fail forever." );
			return;
		}

		// 2. Check FileSystem Presence
		if ( !FileSystem.Data.FileExists( path ) )
		{
			Log.Warning( $"FILE SYSTEM: {path} does not exist. StartupManager should trigger kit injection." );
		}
		else
		{
			// 3. Deep Content Inspection
			string content = FileSystem.Data.ReadAllText( path );
			Log.Info( $"FILE CONTENT:\n{content}" );

			bool hasKitString = content.Contains( "Starter Kit" );
			Log.Info( $"LOGIC CHECK: Contains 'Starter Kit'? {hasKitString}" );

			if ( hasKitString )
				Log.Warning( "RESULT: StartupManager will SKIP injection because 'Starter Kit' was found in text." );
			else
				Log.Info( "RESULT: StartupManager SHOULD inject kit." );
		}

		// 4. Inspect RAM State (The "Ghost" Session Check)
		if ( gss.CurrentCharacter == null )
		{
			Log.Error( "RAM STATE: CurrentCharacter is NULL. This is why injection fails." );
		}
		else
		{
			Log.Info( $"RAM STATE: Name={gss.CurrentCharacter.Name}, Money={gss.CurrentCharacter.Money}, Shares={gss.CurrentCharacter.Shares}" );
			Log.Info( $"MODIFIERS: {gss.CurrentCharacter.Modifiers} (Raw: {(int)gss.CurrentCharacter.Modifiers})" );
			
			if ( gss.CurrentCharacter.Modifiers.HasFlag( StartingModifiers.Destitute ) )
			{
				Log.Warning( "MODIFIER ALERT: 'Destitute' flag is active. This explicitly blocks the $1000/1 Share." );
			}
		}

		// 5. Check Economy Manager
		var economy = Scene.GetAllComponents<EconomyManager>().FirstOrDefault();
		if ( economy == null )
		{
			Log.Error( "ECONOMY: No EconomyManager found in scene. StartupManager will abort." );
		}

		Log.Info( "--- DIAGNOSTIC COMPLETE ---" );
	}
}
