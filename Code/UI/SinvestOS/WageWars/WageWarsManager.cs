using Sandbox;
using System.Collections.Generic;

public sealed class WageWarsManager : Component
{
    public static WageWarsManager Instance { get; private set; }

    [Property, Group("Prefabs")] public GameObject ChunkPrefab { get; set; }
    [Property, Group("Prefabs")] public GameObject CubePrefab  { get; set; }

    [Property, Group("Settings")] public Vector2Int WorldSizeInChunks { get; set; } = new( 5, 5 );
    [Property, Group("Settings")] public Vector2Int ChunkSizeInCubes  { get; set; } = new( 16, 16 );
    
    private const float CubeSize = 50f;
    private Dictionary<Vector2Int, WageWarsCube> _cubeMap = new();

    protected override void OnAwake()
    {
        Instance = this;
    }

    protected override void OnStart()
    {
        if ( !Networking.IsHost ) return;

        GenerateWorld();

        // Restore cube ownership from the previous session (if a save exists).
        // LoadWorld() is a no-op when no save file is present, so this is always safe.
        WageWarsWorldSave.Instance?.LoadWorld();
    }

    public void GenerateWorld()
    {
        float stride      = ChunkSizeInCubes.x * CubeSize;
        float totalWidth  = WorldSizeInChunks.x * stride;
        float totalHeight = WorldSizeInChunks.y * stride;
        Vector3 centerOffset = new Vector3( totalWidth / 2f, totalHeight / 2f, 0 );

        for ( int cx = 0; cx < WorldSizeInChunks.x; cx++ )
        {
           for ( int cy = 0; cy < WorldSizeInChunks.y; cy++ )
           {
              Vector3 chunkPos = WorldPosition + new Vector3( cx * stride, cy * stride, 0 ) - centerOffset;
            
              var chunkObj = ChunkPrefab.Clone( chunkPos );
              chunkObj.Name   = $"Chunk_{cx}_{cy}";
              chunkObj.Parent = GameObject;
              chunkObj.LocalRotation = Rotation.Identity;

              var chunkScript = chunkObj.Components.GetOrCreate<WageWarsChunk>();
              chunkScript.ChunkCoords = new Vector2Int( 
                 cx - (WorldSizeInChunks.x / 2), 
                 cy - (WorldSizeInChunks.y / 2) 
              );

              FillChunk( chunkObj, cx, cy );
              chunkObj.NetworkSpawn();
           }
        }
    }

    private void FillChunk( GameObject parent, int cx, int cy )
    {
        for ( int x = 0; x < ChunkSizeInCubes.x; x++ )
        {
            for ( int y = 0; y < ChunkSizeInCubes.y; y++ )
            {
                Vector3 localPos = new Vector3( x * CubeSize, y * CubeSize, 0 );
                var cubeObj = CubePrefab.Clone();
                cubeObj.Parent        = parent;
                cubeObj.LocalPosition = localPos;
                cubeObj.LocalRotation = Rotation.Identity;

                var cubeScript = cubeObj.Components.Get<WageWarsCube>();
                if ( cubeScript.IsValid() )
                {
                    Vector2Int gridPos = new Vector2Int( 
                        (cx * ChunkSizeInCubes.x) + x, 
                        (cy * ChunkSizeInCubes.y) + y 
                    );
                    cubeScript.GridPosition = gridPos;
                    _cubeMap[gridPos]       = cubeScript;
                }
            }
        }
    }

    public WageWarsCube GetCubeAt( Vector2Int pos ) => _cubeMap.GetValueOrDefault( pos );
}
