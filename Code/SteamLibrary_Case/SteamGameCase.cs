using Sandbox;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public sealed class SteamGameCase : Component
{
    [Property, Group( "References" )] public ModelRenderer TargetRenderer { get; set; }
    
    [Property, Group( "Tooltip" )] public string TooltipTitle { get; set; } = "Steam Game Case";
    [Property, Group( "Tooltip" )] public string TooltipIcon { get; set; } = "videogame_asset";
    [Property, Group( "Tooltip" )] public string TooltipDescription { get; set; } = "Press E to Shuffle";

    // UPDATED: Use a Dictionary to store the AppID (Key) and Game Name (Value)
    private Dictionary<long, string> _appIdPool = new();
    private bool _isBusy = false;

    protected override void OnStart()
    {
       if ( Game.Random.Int( 0, 1 ) == 0 )
          _ = LoadSteamAssets( 4000, "Garry's Mod" );
       else
          _ = LoadSteamAssets( 590830, "s&box" );
    
       _ = RefreshAppPool();
    }

    public async Task Shuffle()
    {
       if ( _isBusy ) return;

       if ( _appIdPool.Count < 5 )
       {
          _ = RefreshAppPool();
       }

       if ( _appIdPool.Count > 0 )
       {
          // UPDATED: Pick a random Key (ID) from the dictionary keys
          var keys = new List<long>( _appIdPool.Keys );
          var randomId = Game.Random.FromList( keys );
          var gameName = _appIdPool[randomId];

          _appIdPool.Remove( randomId );
          
          await LoadSteamAssets( randomId, gameName );
       }
    }

    private async Task RefreshAppPool()
    {
       string[] genres = { "Action", "Strategy", "RPG", "Indie", "Adventure", "Simulation", "Early Access" };
       string randomGenre = Game.Random.FromArray( genres );

       var url = $"https://steamspy.com/api.php?request=genre&genre={randomGenre}";
       var response = await Http.RequestAsync( url );
    
       if ( !response.IsSuccessStatusCode ) return;

       var jsonString = await response.Content.ReadAsStringAsync();
       var node = JsonNode.Parse( jsonString );

       if ( node is not JsonObject obj ) return;

       foreach ( var entry in obj )
       {
          if ( long.TryParse( entry.Key, out var id ) )
          {
             // UPDATED: Extract the "name" property for this entry
             var name = entry.Value?["name"]?.ToString() ?? "Unknown Game";

             if ( !_appIdPool.ContainsKey( id ) )
                _appIdPool.Add( id, name );
          }
       }

       Log.Info( $"[SteamGameCase] Refreshed pool with {randomGenre} games. Total: {_appIdPool.Count}" );
    }

    private async Task LoadSteamAssets( long appId, string title )
    {
       _isBusy = true;

       string cdn = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps";
    
       var coverTex = await Texture.LoadAsync( $"{cdn}/{appId}/library_600x900.jpg" );
       var heroTex = await Texture.LoadAsync( $"{cdn}/{appId}/library_hero.jpg" );

       if ( coverTex == null || heroTex == null || coverTex.Width < 100 )
       {
          Log.Warning( $"[SteamGameCase] Asset fetch failed for {appId} ({title}). Retrying..." );
          await Task.Delay( 2000 );
          _isBusy = false;
          _ = Shuffle(); 
          return;
       }

       if ( this.IsValid && TargetRenderer?.SceneObject != null )
       {
          TargetRenderer.SceneObject.Attributes.Set( "CoverArt", coverTex );
          TargetRenderer.SceneObject.Attributes.Set( "BackArt", heroTex );
       
          // SUCCESS: Now the tooltip will show the actual game name!
          TooltipTitle = title;
       }

       await Task.Delay( 1000 );
       _isBusy = false;
    }
}
