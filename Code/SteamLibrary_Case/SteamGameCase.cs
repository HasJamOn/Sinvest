using Sandbox;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Linq;

public class LoadedGame
{
    public string Title;
    public Texture Cover;
    public Texture Hero;
    public long AppId; // Added to track uniqueness in history
}

public sealed class SteamGameCase : Component
{
    public enum StartBehavior { ClassicRandom, InstantShuffle }

    [Property, Group( "References" )] public ModelRenderer TargetRenderer { get; set; }
    [Property, Group( "Settings" )] public StartBehavior OnStartMode { get; set; } = StartBehavior.ClassicRandom;
    
    [Property, Group( "Settings" )] public int MaxHistory { get; set; } = 30;

    [Property, Group( "Tooltip" )] public string TooltipTitle { get; set; } = "Steam Game Case";
    [Property, Group( "Tooltip" )] public string TooltipIcon { get; set; } = "videogame_asset";
    [Property, Group( "Tooltip" )] public string TooltipDescription { get; set; } = "Press E to Shuffle";
           
    private Dictionary<long, string> _appIdPool = new();
    private LinkedList<(long Id, string Name)> _processingQueue = new();
    private List<int> _bootstrapIds = new();

    // --- BUFFER SYSTEM ---
    private Queue<LoadedGame> _readyBuffer = new();
    private int _bufferTargetSize = 20;
    private bool _isRefilling = false;

    // --- HISTORY SYSTEM (Fallback) ---
    private List<LoadedGame> _historyPool = new();
    private int _historyIndex = 0;

    protected override void OnStart()
    {
        LoadBootstrapFromFile( "SteamLibrary_Case/data/bootstrap_ids.txt" );
        _bootstrapIds = _bootstrapIds.OrderBy( x => Guid.NewGuid() ).ToList();

        _ = InitializeSystem();
    }

    private async Task InitializeSystem()
    {
	    // s&box AppID: 590830
	    if ( OnStartMode == StartBehavior.ClassicRandom )
	    {
		    // 1. Load GMod (The classic fallback)
		    var gmod = await LoadGameData( 4000, "Garry's Mod" );
		    if ( gmod != null ) 
		    {
			    ApplyToRenderer( gmod );
			    AddToHistory( gmod );
		    }

		    // 2. Load s&box into the pool immediately
		    var sbox = await LoadGameData( 590830, "s&box" );
		    if ( sbox != null ) AddToHistory( sbox );
        
		    await RefreshAppPool();
		    _ = FillBuffer();
	    }
	    else
	    {
		    // For InstantShuffle, we'll try to get s&box in there first 
		    // before the random SteamSpy clutter fills the buffer
		    var sbox = await LoadGameData( 590830, "s&box" );
		    if ( sbox != null ) 
		    {
			    _readyBuffer.Enqueue( sbox );
			    AddToHistory( sbox );
		    }

		    await RefreshAppPool();
		    await FillBuffer();
		    Shuffle(); 
	    }
    }

    public void Shuffle()
    {
	    // 1. Try the fresh buffer first
	    if ( _readyBuffer.Count > 0 )
	    {
		    var game = _readyBuffer.Dequeue();
		    ApplyToRenderer( game );
		    _ = FillBuffer(); // Keep the factory moving
		    return;
	    }

	    // 2. Fallback to cycling through the last 30 games
	    if ( _historyPool.Count > 0 )
	    {
		    // Cycle through history (Oldest -> Newest)
		    var cachedGame = _historyPool[_historyIndex];
		    ApplyToRenderer( cachedGame );

		    _historyIndex = ( _historyIndex + 1 ) % _historyPool.Count;
        
		    Log.Info( $"[SteamGameCase] Buffer empty. Cycling history ({_historyIndex}/{_historyPool.Count})" );
		    return;
	    }

	    Log.Warning( "Shuffle pressed: No games loaded yet!" );
    }

    private void AddToHistory( LoadedGame game )
    {
	    // Don't add the same game twice
	    if ( _historyPool.Any( x => x.AppId == game.AppId ) ) return;

	    _historyPool.Add( game );

	    // Trim pool if it exceeds your defined MaxHistory
	    if ( _historyPool.Count > MaxHistory )
	    {
		    _historyPool.RemoveAt( 0 ); // Remove the oldest item
	    }

	    // Reset cycle index whenever we get fresh data
	    _historyIndex = 0;
    }

    private void ApplyToRenderer( LoadedGame game )
    {
        if ( !this.IsValid || TargetRenderer?.SceneObject == null ) return;

        TargetRenderer.SceneObject.Attributes.Set( "CoverArt", game.Cover );
        TargetRenderer.SceneObject.Attributes.Set( "BackArt", game.Hero );
        TooltipTitle = game.Title;
    }

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
                AddToHistory( loaded ); // Save to history as they become ready
            }
        }

        _isRefilling = false;
    }

    // [Rest of your candidate and loading logic remains the same...]
    private (long Id, string Name) GetNextCandidate()
    {
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

    private async Task<LoadedGame> LoadGameData( long appId, string title )
    {
        string cdn = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps";
        string finalTitle = title == "Steam Game" ? await FetchGameNameFromSteam( appId ) : title;

        var coverTex = await Texture.LoadAsync( $"{cdn}/{appId}/library_600x900.jpg" );
        var heroTex = await Texture.LoadAsync( $"{cdn}/{appId}/library_hero.jpg" );

        if ( coverTex == null || heroTex == null || coverTex.Width < 100 ) return null;

        await Task.Delay( 200 );

        return new LoadedGame { AppId = appId, Title = finalTitle, Cover = coverTex, Hero = heroTex };
    }

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
