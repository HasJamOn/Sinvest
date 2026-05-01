using Sandbox;

public sealed class StampLandManager : Component
{
	[Property] public GameObject CubePrefab { get; set; }
	[Property] public Vector2Int GridSize { get; set; } = new Vector2Int( 10, 10 );
	[Property] public float GridSpacing { get; set; } = 64f;

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		GenerateGrid();
	}

	private void GenerateGrid()
	{
		if ( !CubePrefab.IsValid() ) return;

		var gridContainer = Scene.CreateObject();
		gridContainer.Name = "Grid_Cubes";
		gridContainer.Parent = GameObject; 

		// We calculate the center offset
		// Subtracting 1 from GridSize ensures an even distribution around the center
		Vector3 centerOffset = new Vector3( 
			(GridSize.x - 1) * GridSpacing * 0.5f, 
			(GridSize.y - 1) * GridSpacing * 0.5f, 
			0 
		);

		for ( int x = 0; x < GridSize.x; x++ )
		{
			for ( int y = 0; y < GridSize.y; y++ )
			{
				// Calculate position relative to the manager's WorldPosition
				// Subtracting the centerOffset pulls the grid back into the negative directions
				Vector3 localOffset = new Vector3( x * GridSpacing, y * GridSpacing, 0 );
				Vector3 spawnPos = WorldPosition + localOffset - centerOffset;

				var cube = CubePrefab.Clone( spawnPos );
				cube.Parent = gridContainer;
				cube.NetworkSpawn(); 
			}
		}
	}
}
