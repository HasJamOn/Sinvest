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

	private List<long> _appIdPool = new();
	private bool _isBusy = false;

	protected override void OnStart()
	{
		_ = LoadSteamAssets( 4000, "Garry's Mod" );
		_ = RefreshAppPool();
	}

	/// <summary>
	/// Handles the interaction. The _isBusy check at the top acts as the request lock.
	/// </summary>
	public async Task Shuffle()
	{
		// STAGE 1: The Lockout Gate
		// If we are currently loading or in the cooldown period, ignore all inputs.
		if ( _isBusy ) return;

		// If the pool is getting low, trigger a background refresh
		if ( _appIdPool.Count < 5 )
		{
			_ = RefreshAppPool();
		}

		if ( _appIdPool.Count > 0 )
		{
			var randomId = Game.Random.FromList( _appIdPool );
			_appIdPool.Remove( randomId );
			
			// STAGE 2: Execute the Load (which manages the busy state)
			await LoadSteamAssets( randomId, $"App ID: {randomId}" );
		}
	}

	private async Task RefreshAppPool()
	{
		// Remove the try/catch entirely. s&box will catch and log 
		// top-level exceptions automatically without crashing the game.
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
				if ( !_appIdPool.Contains( id ) )
					_appIdPool.Add( id );
			}
		}

		Log.Info( $"[SteamGameCase] Refreshed pool with {randomGenre} games. Total: {_appIdPool.Count}" );
	}

	private async Task LoadSteamAssets( long appId, string title )
	{
		_isBusy = true;

		string cdn = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps";
	
		// Fetch the textures
		var coverTex = await Texture.LoadAsync( $"{cdn}/{appId}/library_600x900.jpg" );
		var heroTex = await Texture.LoadAsync( $"{cdn}/{appId}/library_hero.jpg" );

		// CHECK 1: Is it null? (Standard failure)
		// CHECK 2: Is it too small? (Failed downloads often return a 1x1 or 16x16 dummy)
		if ( coverTex == null || heroTex == null || coverTex.Width < 100 )
		{
			Log.Warning( $"[SteamGameCase] Asset fetch failed for {appId}. Retrying..." );
		
			// Wait 2 seconds to let the Steam CDN "cool down"
			await Task.Delay( 2000 );

			_isBusy = false;

			// Automatically try a different game from the pool
			_ = Shuffle(); 
			return;
		}

		// SUCCESS: Apply the textures to the ModelRenderer
		if ( this.IsValid && TargetRenderer?.SceneObject != null )
		{
			TargetRenderer.SceneObject.Attributes.Set( "CoverArt", coverTex );
			TargetRenderer.SceneObject.Attributes.Set( "BackArt", heroTex );
		
			// Only update the tooltip title once we know the assets are visible
			TooltipTitle = title;
		}

		// Wait 1 second before allowing the user to press E again
		await Task.Delay( 1000 );
		_isBusy = false;
	}
}
