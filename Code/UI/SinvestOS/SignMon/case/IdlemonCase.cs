using Sandbox;
using System.Threading.Tasks;

public sealed class IdlemonCase : Component
{
    [Property] public ModelRenderer TargetRenderer { get; set; }
    
    public int SteamIdOverride { get; set; } = -1; 
    private int _lastAppliedId = -1;
    private bool _isUpdating = false;

    protected override void OnUpdate()
    {
       // Check for validity - using .IsValid() is the preferred s&box way
       if ( !TargetRenderer.IsValid() ) return;

       if ( _lastAppliedId != SteamIdOverride )
       {
          _lastAppliedId = SteamIdOverride;
          _ = LoadTexture( SteamIdOverride );
       }
    }

    public void UpdateAllStats( int steamId, Sinvest.IdleMonData data )
    {
       SteamIdOverride = steamId;
    }

    private async Task LoadTexture( int steamId )
    {
       if ( _isUpdating || steamId <= 0 ) return;
       _isUpdating = true;

       try 
       {
          string url = $"https://steamcdn-a.akamaihd.net/steam/apps/{steamId}/library_600x900.jpg";
          var tex = await Texture.LoadAsync( url );

          if ( tex == null || tex.IsError )
          {
             tex = await Texture.LoadAsync( $"https://cdn.akamai.steamstatic.com/steam/apps/{steamId}/header.jpg" );
          }

          if ( tex != null && TargetRenderer.IsValid() )
          {
             // 1. Set on the Component level (This is the most reliable for current s&box)
             TargetRenderer.Attributes.Set( "Artwork", tex );

             // 2. Set on the SceneObject level (Double-tap to ensure the GPU sees it)
             if ( TargetRenderer.SceneObject != null )
             {
                TargetRenderer.SceneObject.Attributes.Set( "Artwork", tex );
             }
             
             Log.Info( $"[Idlemon] Successfully applied SteamID {steamId} to Artwork attribute." );
          }
       }
       catch ( System.Exception e ) 
       { 
          Log.Error( $"[Idlemon] Texture Fail: {e.Message}" ); 
       }
       finally 
       { 
          _isUpdating = false; 
       }
    }
}
