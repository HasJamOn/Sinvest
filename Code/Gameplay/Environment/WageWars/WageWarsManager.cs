using System;
using Sandbox;
using System.Collections.Generic;

/// <summary>
/// Root controller for the Wage Wars world generation. 
/// Handles the instantiation of chunks and cubes into a coordinate-mapped grid.
/// </summary>
public sealed class WageWarsManager : Component
{
    public static WageWarsManager Instance { get; private set; }

    [Property, Group( "Prefabs" )] public GameObject ChunkPrefab { get; set; }
    [Property, Group( "Prefabs" )] public GameObject CubePrefab { get; set; }

    [Property, Group( "Settings" )] public int WorldSize { get; set; } = 5; // Total chunks along one axis
    [Property, Group( "Settings" )] public int ChunkSize { get; set; } = 16; // Cubes per chunk axis

    private const float CubeSize = 50f;
    
    // Global lookup for neighbor checks (Upgrading/Attacking logic)
    private Dictionary<Vector2Int, WageWarsCube> _cubeMap = new();
    private bool _generated = false;

    protected override void OnAwake()
    {
        Instance = this;
    }

    protected override void OnStart()
    {
        // Generation is host-authoritative to ensure consistent GridPositions across the network.
        if ( !Networking.IsHost || _generated ) return;

        _generated = true;
        GenerateWorld();
        
        // Restoration of saved state occurs immediately after geometry is instantiated.
        WageWarsWorldSave.Instance?.LoadWorld();
    }

    /// <summary>
    /// Calculates the world bounds and instantiates Chunk objects in a centered grid.
    /// </summary>
    public void GenerateWorld()
    {
        float stride = ChunkSize * CubeSize;
        float totalDimension = WorldSize * stride;

        // Calculates the starting corner so the entire world is centered on the Manager's GameObject.
        float startOffset = (-totalDimension / 2f) + (CubeSize / 2f);

        for ( int cx = 0; cx < WorldSize; cx++ )
        {
           for ( int cy = 0; cy < WorldSize; cy++ )
           {
              Vector3 localChunkPos = new Vector3( 
                 startOffset + (cx * stride), 
                 startOffset + (cy * stride), 
                 startOffset 
              );

              var chunkObj = ChunkPrefab.Clone();
              chunkObj.Name = $"Chunk_{cx}_{cy}";
            
              // Hierarchy parenting ensures the chunk moves/rotates with the Manager.
              chunkObj.Parent = GameObject;
              chunkObj.LocalPosition = localChunkPos;
              chunkObj.LocalRotation = Rotation.Identity;

              // Assigns logical coordinates to the chunk for LOD/Visibility tracking.
              var chunkScript = chunkObj.Components.GetOrCreate<WageWarsChunk>();
              chunkScript.ChunkCoords = new Vector2Int( cx - (WorldSize / 2), cy - (WorldSize / 2) );

              FillChunk( chunkObj, cx, cy );
        
              // Synchronizes the chunk object across the network.
              chunkObj.NetworkSpawn();
           }
        }

        Log.Info( $"WageWars: Generated {WorldSize * WorldSize} chunks centered at {WorldPosition}" );
    }

    /// <summary>
    /// Populates a specific chunk with Cube objects and registers them in the global map.
    /// </summary>
    private void FillChunk( GameObject parent, int cx, int cy )
    {
        for ( int x = 0; x < ChunkSize; x++ )
        {
            for ( int y = 0; y < ChunkSize; y++ )
            {
                Vector3 localPos = new Vector3( x * CubeSize, y * CubeSize, 0 );
                var cubeObj = CubePrefab.Clone();
                cubeObj.Parent = parent;
                cubeObj.LocalPosition = localPos;
                cubeObj.LocalRotation = Rotation.Identity;

                var cubeScript = cubeObj.Components.Get<WageWarsCube>();
                if ( cubeScript.IsValid() )
                {
                    // Maps the local chunk index to a unique global grid coordinate.
                    Vector2Int gridPos = new Vector2Int( (cx * ChunkSize) + x, (cy * ChunkSize) + y );
                    cubeScript.GridPosition = gridPos;
                    _cubeMap[gridPos] = cubeScript;
                }
            }
        }
    }

    /// <summary>
    /// Visualizes the intended world boundaries within the s&box Editor.
    /// </summary>
    protected override void DrawGizmos()
    {
        float totalDimension = WorldSize * ChunkSize * CubeSize;
        Vector3 boxSize = new Vector3( totalDimension, totalDimension, totalDimension );

        Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.5f );
        Gizmo.Draw.LineThickness = 2f;
    
        // Outer boundary box representation.
        Gizmo.Draw.LineBBox( new BBox( -boxSize / 2f, boxSize / 2f ) );

        // Ground plane highlight at the base of the world volume.
        float bottomZ = -totalDimension / 2f;
    
        Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.05f );
        Gizmo.Draw.SolidBox( new BBox( 
           new Vector3( -totalDimension / 2f, -totalDimension / 2f, bottomZ - 1f ), 
           new Vector3( totalDimension / 2f, totalDimension / 2f, bottomZ + 1f ) 
        ) );
    }

    /// <summary>
    /// Retrieves a cube component by its grid coordinates for cross-component communication.
    /// </summary>
    public WageWarsCube GetCubeAt( Vector2Int pos ) => _cubeMap.GetValueOrDefault( pos );
}
