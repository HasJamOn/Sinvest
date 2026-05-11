using Sandbox; 
using Sinvest;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Linq;

/// <summary>
/// Data structure representing a fully resolved Steam game with pre-loaded textures.
/// </summary>
public class LoadedGame
{
    public string Title;
    public Texture Cover;
    public Texture Hero;
    public long AppId;
}

/// <summary>
/// Manages a physical game case that dynamically displays Steam library assets.
/// Utilizes a background buffering system to ensure zero-latency texture swapping.
/// </summary>
public sealed class SteamGameCase : Component
{
    public enum StartBehavior { ClassicRandom, InstantShuffle }

    [Property, Group( "References" )] public ModelRenderer TargetRenderer { get; set; }
    
    private string _swapSound = "sounds/ui/shuffle.sound";
    
    [Property, Group( "Audio" ), ResourceType( "sound" )] 
    public string SwapSound { get; set; } = "sounds/ui/shuffle.sound";

    [Property, Group( "Settings" )] public StartBehavior OnStartMode { get; set; } = StartBehavior.ClassicRandom;
    [Property, Group( "Settings" )] public int MaxHistory { get; set; } = 30;

    [Property, Group( "Tooltip" )] public string TooltipTitle { get; set; } = "Steam Game Case";
    [Property, Group( "Tooltip" )] public string TooltipIcon { get; set; } = "videogame_asset";
    [Property, Group( "Tooltip" )] public string TooltipDescription { get; set; } = "Press E to Shuffle";
           
    // Stores discovered AppIDs and their associated names
    private Dictionary<long, string> _appIdPool = new();
    // Queue for IDs currently being processed for asset downloads
    private LinkedList<(long Id, string Name)> _processingQueue = new();
    // High-priority IDs loaded from local bootstrap file
    private List<int> _bootstrapIds = new();

    // --- BUFFER SYSTEM ---
    // Stores fully loaded games ready for immediate display
    private Queue<LoadedGame> _readyBuffer = new();
    private int _bufferTargetSize = 20;
    private bool _isRefilling = false;

    // --- HISTORY SYSTEM (Fallback) ---
    // Cache of previously displayed games to use if the buffer is empty
    private List<LoadedGame> _historyPool = new();
    private int _historyIndex = 0;
    private bool _isInitialized = false;

    protected override void OnStart()
    {
        string internalPath = "shaders/data/bootstrap_ids.txt";
    
        LoadBootstrapFromFile( internalPath );
        
        // Randomize seeds to prevent repetitive start-up sequences
        _bootstrapIds = _bootstrapIds.OrderBy( x => Guid.NewGuid() ).ToList();

        _ = InitializeSystem();
    }

    /// <summary>
    /// Prepares the initial pool and fills the buffer before marking the system as live.
    /// </summary>
    private async Task InitializeSystem()
    {
        if ( OnStartMode == StartBehavior.ClassicRandom )
        {
           // Explicitly load staple games for the classic experience
           var gmod = await LoadGameData( 4000, "Garry's Mod" );
           if ( gmod != null ) 
           {
              ApplyToRenderer( gmod );
              AddToHistory( gmod );
           }

           var sbox = await LoadGameData( 590830, "sbox" );
           if ( sbox != null ) AddToHistory( sbox );
    
           await RefreshAppPool();
           _ = FillBuffer();
        }
        else
        {
           // Load sbox immediately then trigger a shuffle once the buffer is ready
           var sbox = await LoadGameData( 590830, "sbox" );
           if ( sbox != null ) 
           {
              _readyBuffer.Enqueue( sbox );
              AddToHistory( sbox );
           }

           await RefreshAppPool();
           await FillBuffer();
           Shuffle(); 
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Primary interaction point. Pulls from the buffer or cycles history if the buffer is dry.
    /// </summary>
    public void Shuffle()
    {
        // Priority 1: Use fresh, pre-loaded data
        if ( _readyBuffer.Count > 0 )
        {
           var game = _readyBuffer.Dequeue();
           ApplyToRenderer( game );
           _ = FillBuffer(); // Async refill trigger
           return;
        }

        // Priority 2: Cycle through local cache
        if ( _historyPool.Count > 0 )
        {
           var cachedGame = _historyPool[_historyIndex];
           ApplyToRenderer( cachedGame );

           _historyIndex = ( _historyIndex + 1 ) % _historyPool.Count;
           return;
        }

        Log.Warning( "Shuffle pressed: No games loaded yet!" );
    }

    private void AddToHistory( LoadedGame game )
    {
        if ( _historyPool.Any( x => x.AppId == game.AppId ) ) return;

        _historyPool.Add( game );

        // Maintain fixed memory footprint by trimming old history
        if ( _historyPool.Count > MaxHistory )
        {
           _historyPool.RemoveAt( 0 );
        }

        _historyIndex = 0;
    }

    /// <summary>
    /// Updates Material Attributes on the SceneObject. 
    /// This avoids creating unique material instances and improves performance.
    /// </summary>
    private void ApplyToRenderer( LoadedGame game )
    {
	    if ( !this.IsValid || TargetRenderer?.SceneObject == null ) return;

	    // 1. Update the visual textures
	    TargetRenderer.SceneObject.Attributes.Set( "CoverArt", game.Cover );
	    TargetRenderer.SceneObject.Attributes.Set( "BackArt", game.Hero );

	    // 2. Update the tooltip metadata IMMEDIATELY
	    TooltipTitle = game.Title; 

	    if ( _isInitialized )
	    {
		    Sound.Play( SwapSound, WorldPosition );
	    }
    }

    /// <summary>
    /// Continuous background task that populates the buffer until the target size is met.
    /// </summary>
    private async Task FillBuffer()
    {
        if ( _isRefilling || _readyBuffer.Count >= _bufferTargetSize ) return;
        _isRefilling = true;

        while ( _readyBuffer.Count < _bufferTargetSize )
        {
            var next = GetNextCandidate();
            
            if ( next.Id == 0 ) 
            {
                await RefreshAppPool();
                await Task.Delay( 500 );
                continue;
            }

            var loaded = await LoadGameData( next.Id, next.Name );
            
            if ( loaded != null )
            {
                _readyBuffer.Enqueue( loaded );
                AddToHistory( loaded ); 
            }
        }

        _isRefilling = false;
    }

    /// <summary>
    /// Logic for selecting the next ID to load. 
    /// Prioritizes bootstrap IDs before falling back to the random web-scraped pool.
    /// </summary>
    private (long Id, string Name) GetNextCandidate()
    {
        // 50% chance to pull from bootstrap first to ensure "quality" results appear often
        if ( _bootstrapIds.Count > 0 && Game.Random.Int( 0, 1 ) == 0 )
        {
            var id = _bootstrapIds[0];
            _bootstrapIds.RemoveAt( 0 );
            return (id, "Steam Game");
        }

        lock ( _processingQueue )
        {
            if ( _processingQueue.Count > 0 )
            {
                var item = _processingQueue.First.Value;
                _processingQueue.RemoveFirst();
                return item;
            }
        }

        if ( _bootstrapIds.Count > 0 )
        {
            var id = _bootstrapIds[0];
            _bootstrapIds.RemoveAt( 0 ); 
            return (id, "Steam Game");
        }

        if ( _appIdPool.Count > 0 )
        {
            var randomId = Game.Random.FromList( _appIdPool.Keys.ToList() );
            return (randomId, _appIdPool[randomId]);
        }

        return (0, "");
    }

    /// <summary>
    /// Asynchronously fetches textures and metadata from Steam's CDN and API.
    /// </summary>
    private async Task<LoadedGame> LoadGameData( long appId, string title )
    {
        string cdn = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps";
        string finalTitle = title == "Steam Game" ? await FetchGameNameFromSteam( appId ) : title;

        var coverTex = await Texture.LoadAsync( $"{cdn}/{appId}/library_600x900.jpg" );
        var heroTex = await Texture.LoadAsync( $"{cdn}/{appId}/library_hero.jpg" );

        // Validation: Some apps (DLC/Tools) lack these specific library assets
        if ( coverTex == null || heroTex == null || coverTex.Width < 100 ) return null;

        // Artificial delay to prevent aggressive burst requests
        await Task.Delay( 200 );

        return new LoadedGame { AppId = appId, Title = finalTitle, Cover = coverTex, Hero = heroTex };
    }

    /// <summary>
    /// Scrapes SteamSpy for a random genre to refresh the local discovery pool.
    /// </summary>
    private async Task RefreshAppPool()
    {
        string[] genres = { "Action", "Strategy", "RPG", "Indie", "Adventure", "Simulation" };
        var url = $"https://steamspy.com/api.php?request=genre&genre={Game.Random.FromArray( genres )}";
        var response = await Http.RequestAsync( url );
        if ( !response.IsSuccessStatusCode ) return;

        var node = JsonNode.Parse( await response.Content.ReadAsStringAsync() );
        if ( node is not JsonObject obj ) return;

        foreach ( var entry in obj )
        {
            if ( long.TryParse( entry.Key, out var id ) )
            {
                var name = entry.Value?["name"]?.ToString() ?? "Unknown Game";
                lock ( _processingQueue ) { _processingQueue.AddFirst( (id, name) ); }
                if ( !_appIdPool.ContainsKey( id ) ) _appIdPool.Add( id, name );
            }
        }
    }

    /// <summary>
    /// Resolves a human-readable name from Steam's Store API for anonymous IDs.
    /// </summary>
    private async Task<string> FetchGameNameFromSteam( long appId )
    {
        try
        {
            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
            var response = await Http.RequestAsync( url );
            if ( !response.IsSuccessStatusCode ) return $"Game #{appId}";

            var jsonText = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse( jsonText );
            var name = node?[appId.ToString()]?["data"]?["name"]?.ToString();
            return !string.IsNullOrEmpty( name ) ? name : $"Game #{appId}";
        }
        catch { return $"Game #{appId}"; }
    }

    private void LoadBootstrapFromFile( string path )
    {
        if ( !FileSystem.Mounted.FileExists( path ) ) return;
        string rawContent = FileSystem.Mounted.ReadAllText( path );
        _bootstrapIds = rawContent.Split( new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries )
            .Select( s => s.Trim() ).Where( s => int.TryParse( s, out _ ) ).Select( int.Parse ).ToList();
    }
}
