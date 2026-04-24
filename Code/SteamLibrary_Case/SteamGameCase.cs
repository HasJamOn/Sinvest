using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

// We add the [Icon] attribute so it looks nice in the editor
[Icon( "library_books" )]
public sealed class SteamGameCase : Component
{
    [Property, Group( "Settings" )] public long AppId { get; set; } = 4000;
    [Property, Group( "References" )] public ModelRenderer TargetRenderer { get; set; }

    // Properties for the Tooltip UI to read
    [Property, Group( "Tooltip" )] public string TooltipTitle { get; set; } = "Steam Game Case";
    [Property, Group( "Tooltip" )] public string TooltipIcon { get; set; } = "videogame_asset";
    [Property, Group( "Tooltip" )] public string TooltipDescription { get; set; } = "Shuffle Game";

    private List<long> _randomAppIds = new() { 4000, 240, 10, 70, 440, 570, 620, 250820, 218230 };

    protected override void OnStart()
    {
        _ = LoadSteamTexturesAsync( AppId );
    }

    protected override void OnUpdate()
    {
        // We don't need a trace here anymore! 
        // The Tooltip Razor component handles the tracing globally.
        // We just check if the player presses E.
        
        // Note: You might want a check here to ensure the player is 
        // actually looking at THIS object before shuffling.
    }

    public async Task Shuffle()
    {
        var newId = Random.Shared.FromList( _randomAppIds );
        await LoadSteamTexturesAsync( newId );
    }

    private async Task LoadSteamTexturesAsync( long id )
    {
        string coverUrl = $"https://steamcdn-a.akamaihd.net/steam/apps/{id}/library_600x900.jpg";
        string backgroundUrl = $"https://steamcdn-a.akamaihd.net/steam/apps/{id}/library_hero.jpg";

        var coverTex = await Texture.LoadAsync( coverUrl );
        var bgTex = await Texture.LoadAsync( backgroundUrl );

        if ( !this.IsValid || TargetRenderer?.SceneObject == null ) return;

        TargetRenderer.SceneObject.Attributes.Set( "CoverArt", coverTex );
        TargetRenderer.SceneObject.Attributes.Set( "BackArt", bgTex );
        
        // Update the tooltip title to show the current App ID
        TooltipTitle = $"Game Case (ID: {id})";
    }
}
