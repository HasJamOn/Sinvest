namespace Sinvest;

public sealed partial class GameSaveSystem : Component
{
	public static GameSaveSystem Instance { get; private set; }

	[Property] public int ActiveSlot { get; set; } = 1;
	[Property] public SceneFile WorldScene { get; set; }
    
	public CharacterSession CurrentCharacter { get; private set; } = new();
    
	private string GetPath( int slot ) => $"slot_{slot}.txt";

	protected override void OnAwake()
	{
		Instance = this;
	}

	public void DeleteSlot( int slot )
	{
		var path = GetPath( slot );
		if ( FileSystem.Data.FileExists( path ) )
		{
			FileSystem.Data.DeleteFile( path );
			Log.Info( $"Deleted save file: {path}" );
		}
	}
}
