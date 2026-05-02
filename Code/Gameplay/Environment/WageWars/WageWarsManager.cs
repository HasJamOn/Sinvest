using System;
using Sandbox;
using System.Collections.Generic;

public sealed class WageWarsManager : Component
{
    public static WageWarsManager Instance { get; private set; }

    [Property, Group( "Prefabs" )] public GameObject ChunkPrefab { get; set; }
    [Property, Group( "Prefabs" )] public GameObject CubePrefab { get; set; }

    // Single inputs for uniform sizes
    [Property, Group( "Settings" )] public int WorldSize { get; set; } = 5;
    [Property, Group( "Settings" )] public int ChunkSize { get; set; } = 16;

    private const float CubeSize = 50f;
    private Dictionary<Vector2Int, WageWarsCube> _cubeMap = new();
    private bool _generated = false;

    protected override void OnAwake()
    {
        Instance = this;
    }

    protected override void OnStart()
    {
        if ( !Networking.IsHost || _generated ) return;

        _generated = true;
        GenerateWorld();
        WageWarsWorldSave.Instance?.LoadWorld();
    }

    public void GenerateWorld()
    {
	    float stride = ChunkSize * CubeSize;
	    float totalDimension = WorldSize * stride;

	    // 1. Shift the start offset inward by half a cube so outer edges align with the Gizmo BBox
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

			    // 2. Clone directly without world-space conversion
			    var chunkObj = ChunkPrefab.Clone();
			    chunkObj.Name = $"Chunk_{cx}_{cy}";
            
			    // 3. Parent first, then apply local transforms to match Gizmo's local space exactly
			    chunkObj.Parent = GameObject;
			    chunkObj.LocalPosition = localChunkPos;
			    chunkObj.LocalRotation = Rotation.Identity;

			    var chunkScript = chunkObj.Components.GetOrCreate<WageWarsChunk>();
			    chunkScript.ChunkCoords = new Vector2Int( cx - (WorldSize / 2), cy - (WorldSize / 2) );

			    FillChunk( chunkObj, cx, cy );
        
			    // Critical for multiplayer visibility
			    chunkObj.NetworkSpawn();
		    }
	    }

	    Log.Info( $"WageWars: Generated {WorldSize * WorldSize} chunks centered at {WorldPosition}" );
    }

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
                    Vector2Int gridPos = new Vector2Int( (cx * ChunkSize) + x, (cy * ChunkSize) + y );
                    cubeScript.GridPosition = gridPos;
                    _cubeMap[gridPos] = cubeScript;
                }
            }
        }
    }

    // Correct method name for s&box Components
    protected override void DrawGizmos()
    {
	    float totalDimension = WorldSize * ChunkSize * CubeSize;
	    Vector3 boxSize = new Vector3( totalDimension, totalDimension, totalDimension );

	    Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.5f );
	    Gizmo.Draw.LineThickness = 2f;
    
	    // Draw the boundary cube
	    Gizmo.Draw.LineBBox( new BBox( -boxSize / 2f, boxSize / 2f ) );

	    // Draw the ground plane highlight at the bottom of the box instead of the middle
	    float bottomZ = -totalDimension / 2f;
    
	    Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.05f );
	    Gizmo.Draw.SolidBox( new BBox( 
		    new Vector3( -totalDimension / 2f, -totalDimension / 2f, bottomZ - 1f ), 
		    new Vector3( totalDimension / 2f, totalDimension / 2f, bottomZ + 1f ) 
	    ) );
    }

    public WageWarsCube GetCubeAt( Vector2Int pos ) => _cubeMap.GetValueOrDefault( pos );
}
