using Sandbox;
using System.Threading.Tasks;

public sealed class SteamGameCase : Component
{
    [Property, Group( "Steam Settings" ), Description( "The Steam AppID of the game (e.g., 4000 for Garry's Mod)" )]
    public long AppId { get; set; } = 4000;

    [Property, Group( "References" ), Description( "The ModelRenderer that will receive the textures." )]
    public ModelRenderer TargetRenderer { get; set; }

    protected override void OnStart()
    {
        // Safety check to ensure the user assigned the renderer in the inspector
        if ( TargetRenderer == null )
        {
            Log.Error( $"[SteamGameCase] TargetRenderer is not assigned on {GameObject.Name}!" );
            return;
        }

        // Fire and forget the async loading task
        _ = LoadSteamTexturesAsync();
    }

    private async Task LoadSteamTexturesAsync()
    {
        // 1. Construct the Steam CDN URLs
        // library_600x900.jpg is the standard vertical box art format
        string coverUrl = $"https://steamcdn-a.akamaihd.net/steam/apps/{AppId}/library_600x900.jpg";
        string backgroundUrl = $"https://steamcdn-a.akamaihd.net/steam/apps/{AppId}/library_hero.jpg";

        Log.Info( $"[SteamGameCase] Fetching textures for AppID: {AppId}..." );

        // 2. Fetch textures from the web
        // Texture.LoadAsync handles the HTTP request and engine-side texture creation
        var coverTex = await Texture.LoadAsync( coverUrl );
        var bgTex = await Texture.LoadAsync( backgroundUrl );

        // 3. Final Safety Checks
        // Check if the component or its SceneObject was destroyed during the await
        if ( !this.IsValid || TargetRenderer == null || TargetRenderer.SceneObject == null )
            return;

        if ( coverTex == null )
        {
            Log.Warning( $"[SteamGameCase] Failed to load cover art for AppID: {AppId}. Check if AppID is valid." );
            return;
        }

        // 4. Apply textures to the SceneObject Attributes
        // "CoverArt" and "BackArt" must match the 'Name' field in your Shader Graph Texture 2D nodes
        TargetRenderer.SceneObject.Attributes.Set( "CoverArt", coverTex );
        
        if ( bgTex != null )
        {
            TargetRenderer.SceneObject.Attributes.Set( "BackArt", bgTex );
        }

        Log.Info( $"[SteamGameCase] Successfully applied textures for {AppId}" );
    }
}
