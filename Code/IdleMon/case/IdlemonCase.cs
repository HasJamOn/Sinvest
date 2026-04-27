using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class IdlemonCase : Component
{
    [Property, Group( "References" )] public ModelRenderer TargetRenderer { get; set; }
    
    // We store the IDs here after loading from the txt
    public List<int> IdPool { get; private set; } = new();
    private int _currentIndex = 0;

    protected override void OnStart()
    {
        // Path adjusted to your specific solution structure
        LoadPool( "shaders/data/bootstrap_ids.txt" );
        
        // Optional: Shuffle the list on start so it's not the same order every time
        IdPool = IdPool.OrderBy( x => Guid.NewGuid() ).ToList();

        if ( IdPool.Count > 0 )
        {
            _ = ApplyStatsFromId( IdPool[0] );
        }
    }
    
    protected override void OnUpdate()
    {
	    // Ensure we keep the shader clock ticking even after swapping textures
	    if ( TargetRenderer.IsValid && TargetRenderer.SceneObject != null )
	    {
		    // Set a custom time parameter to avoid the 'pop' during swaps
		    TargetRenderer.SceneObject.Attributes.Set( "ShaderTime", RealTime.Now );
	    }
    }

    public void CycleIdlemon()
    {
        if ( IdPool.Count == 0 ) return;

        int id = IdPool[_currentIndex];
        _currentIndex = ( _currentIndex + 1 ) % IdPool.Count;

        _ = ApplyStatsFromId( id );
    }

    private int _lastAppliedId = -1;

    public async Task ApplyStatsFromId( int id )
    {
	    if ( id == _lastAppliedId ) return; // Stop redundant reloads
	    _lastAppliedId = id;
        if ( !TargetRenderer.IsValid || TargetRenderer.SceneObject == null ) return;

        // 1. Set DNA Attributes (Deterministic based on ID)
        var random = new Random( id );
        var attributes = TargetRenderer.SceneObject.Attributes;

        attributes.Set( "g_flValueRarity", (float)random.NextDouble() );
        attributes.Set( "g_flEfficiency", (float)(random.NextDouble() * 5.0) );
        attributes.Set( "g_flDivision", (float)(1.0 + random.NextDouble() * 4.0) );
        attributes.Set( "g_flCursedAmount", (float)(random.NextDouble() * 100.0) );
        attributes.Set( "g_flAdditionGlow", (float)(random.NextDouble() * 100.0) );

        // 2. Fetch the "Cover Art" (Vertical 600x900 style)
        // This CDN path is the most reliable for Library Assets
        string cdn = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps";
        string url = $"{cdn}/{id}/library_600x900.jpg";

        try
        {
	        var tex = await Texture.LoadAsync( url );
            if ( tex != null )
            {
                // Push to the Shader Attribute we set up
                attributes.Set( "Artwork", tex );
            }
        }
        catch ( Exception e )
        {
            Log.Warning( $"[Idlemon] Failed to pull CoverArt for {id}: {e.Message}" );
        }

        Log.Info( $"[Idlemon] ID: {id} | Pool Position: {_currentIndex}/{IdPool.Count}" );
    }

    private void LoadPool( string path )
    {
        if ( !FileSystem.Mounted.FileExists( path ) )
        {
            Log.Error( $"[Idlemon] Bootstrap file NOT FOUND at: {path}" );
            return;
        }

        string content = FileSystem.Mounted.ReadAllText( path );
        IdPool = content.Split( new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries )
            .Select( s => s.Trim() )
            .Where( s => int.TryParse( s, out _ ) )
            .Select( int.Parse )
            .ToList();

        Log.Info( $"[Idlemon] Loaded {IdPool.Count} valid IDs from bootstrap." );
    }
}
