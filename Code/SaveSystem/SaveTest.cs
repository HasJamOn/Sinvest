using Sandbox;

public sealed class SaveTest : Component
{
	protected override void OnStart()
	{
		// Give the SaveSystem one frame to Awake and set its Instance
		Log.Info("SaveTest: Waiting for SaveSystem...");
		CheckSave();
	}

	async void CheckSave()
	{
		await Task.Delay( 100 ); // Wait 100ms for safety

		if ( GameSaveSystem.Instance == null )
		{
			Log.Error( "SaveTest: GameSaveSystem.Instance is NULL! Is it in the scene?" );
			return;
		}

		GameSaveSystem.Instance.ActiveSlot = 3;
		GameSaveSystem.Instance.LoadActiveSlot();
        
		Log.Info( $"SaveTest Success: Current Name is {GameSaveSystem.Instance.CurrentCharacter.Name}" );
	}
}
