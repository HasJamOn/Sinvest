using Sandbox;

public sealed class StampLandManager : Component
{
    [Property, Group("Prefabs")] public GameObject ChunkPrefab { get; set; }
    [Property, Group("Prefabs")] public GameObject CubePrefab { get; set; }

    [Property, Group("Settings")] public Vector2Int WorldSizeInChunks { get; set; } = new( 5, 5 );
    [Property, Group("Settings")] public Vector2Int ChunkSizeInCubes { get; set; } = new( 16, 16 );
    
    // Hard-coded to 50 to match the Model and Collider JSON
    private const float CubeSize = 50f;

    protected override void OnStart()
    {
        if ( !Networking.IsHost ) return;
        GenerateWorld();
    }

    public void GenerateWorld()
    {
	    float stride = ChunkSizeInCubes.x * CubeSize;

	    // Calculate the total world dimensions to find the center offset
	    float totalWidth = WorldSizeInChunks.x * stride;
	    float totalHeight = WorldSizeInChunks.y * stride;

	    // Offset centers the grid so (0,0,0) is in the middle of the generated mass
	    Vector3 centerOffset = new Vector3( totalWidth / 2f, totalHeight / 2f, 0 );

	    for ( int cx = 0; cx < WorldSizeInChunks.x; cx++ )
	    {
		    for ( int cy = 0; cy < WorldSizeInChunks.y; cy++ )
		    {
			    // Calculate position relative to the Manager, then subtract offset
			    Vector3 chunkPos = WorldPosition + new Vector3( cx * stride, cy * stride, 0 ) - centerOffset;
            
			    var chunkObj = ChunkPrefab.Clone( chunkPos );
			    chunkObj.Name = $"Chunk_{cx}_{cy}";
			    chunkObj.Parent = GameObject;
            
			    chunkObj.LocalRotation = Rotation.Identity;
			    chunkObj.LocalScale = 1.0f;

			    var chunkScript = chunkObj.Components.GetOrCreate<StampLandChunk>();
			    // Update the coords to reflect actual grid space (negative to positive)
			    chunkScript.ChunkCoords = new Vector2Int( 
				    cx - (WorldSizeInChunks.x / 2), 
				    cy - (WorldSizeInChunks.y / 2) 
			    );

			    FillChunk( chunkObj );
			    chunkObj.NetworkSpawn();
		    }
	    }
    }

    private void FillChunk( GameObject parent )
    {
        for ( int x = 0; x < ChunkSizeInCubes.x; x++ )
        {
            for ( int y = 0; y < ChunkSizeInCubes.y; y++ )
            {
                // Position 0 is the center of the first cube
                // To have cubes touch, they sit exactly 50 units apart
                Vector3 localPos = new Vector3( x * CubeSize, y * CubeSize, 0 );
                
                var cube = CubePrefab.Clone();
                cube.Parent = parent;
                cube.LocalPosition = localPos;
                cube.LocalRotation = Rotation.Identity;
                
                // Keep scale 1.0 since the model is already 50 units
                cube.LocalScale = 1.0f;
            }
        }
    }
}
