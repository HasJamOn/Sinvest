using Sandbox;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class CareerTransitionDebugger : Component
{
	[Property] public GameSaveSystem SaveSystem { get; set; }

	protected override void OnStart()
	{
		if ( SaveSystem == null )
			SaveSystem = Scene.GetAllComponents<GameSaveSystem>().FirstOrDefault();
	}

	[Button( "Force Start Transition" )]
	public async void DebugManualTransition()
	{
		Log.Info( "--- STARTING TRANSITION DEBUG ---" );

		if ( SaveSystem == null )
		{
			Log.Error( "DEBUG: GameSaveSystem reference is missing!" );
			return;
		}

		// 1. Check Scene Reference
		if ( SaveSystem.WorldScene == null )
		{
			Log.Error( "DEBUG: WorldScene property is NULL in the Inspector!" );
		}
		else
		{
			Log.Info( $"DEBUG: WorldScene detected: {SaveSystem.WorldScene.ResourcePath}" );
		}

		// 2. Check Session Data
		Log.Info( $"DEBUG: Active Slot: {SaveSystem.ActiveSlot}" );
		Log.Info( $"DEBUG: Character: {SaveSystem.CurrentCharacter?.Name ?? "NULL"}" );

		// 3. Test the Save
		Log.Info( "DEBUG: Testing File I/O..." );
		await SaveSystem.SaveActiveSlotAsync();
		Log.Info( "DEBUG: File I/O Completed." );

		// 4. Test the Load
		Log.Info( "DEBUG: Triggering Scene Load..." );
        
		try 
		{
			SaveSystem.StartGame();
			Log.Info( "DEBUG: StartGame() call finished execution." );
		}
		catch ( System.Exception e )
		{
			Log.Error( $"DEBUG: Scene Load Failed: {e.Message}" );
		}
	}
}
