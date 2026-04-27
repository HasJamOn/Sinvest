using Sandbox;
using System;
using System.Threading.Tasks;
using Sinvest;

public sealed class IdlemonCase : Component
{
    [Property, Group( "References" )] public ModelRenderer TargetRenderer { get; set; }
    
    // Internal State
    public int SteamIdOverride { get; set; } = 4000; 
    private IdleMonData _currentData = IdleMonData.Empty;

    private int _lastAppliedTextureId = -1;
    private bool _isUpdatingTexture = false;

    protected override void OnUpdate()
    {
       if ( !TargetRenderer.IsValid || TargetRenderer.SceneObject == null ) return;

       var attributes = TargetRenderer.SceneObject.Attributes;

       // 1. ANIMS: The time-god node
       attributes.Set( "ShaderTime", RealTime.Now );

       // 2. THE FULL SOUL: Every stat from the GDD
       attributes.Set( "Addition", (float)_currentData.Addition );
       attributes.Set( "Multiplier", (float)_currentData.Multiplier );
       attributes.Set( "Subtraction", (float)_currentData.Subtraction );
       attributes.Set( "Division", (float)_currentData.Division );
       attributes.Set( "TeamBonus", (float)_currentData.TeamBonus );
       attributes.Set( "Luck", (float)_currentData.Luck / 100.0f ); 
       attributes.Set( "Efficiency", (float)_currentData.CostEfficiency / 100.0f );
       attributes.Set( "Generation", (float)_currentData.Generation );
       attributes.Set( "ModelPath", _currentData.ModelPath );

       // 3. TEXTURE: Logic gate fixed
       if ( _lastAppliedTextureId != SteamIdOverride )
       {
          // We set this immediately so the next frame doesn't try to LoadTexture again
          _lastAppliedTextureId = SteamIdOverride;
          _ = LoadTexture( SteamIdOverride );
       }
    }

    public void UpdateAllStats( int steamId, IdleMonData data )
    {
       SteamIdOverride = steamId;
       _currentData = data;
    }

    private async Task LoadTexture( int steamId )
    {
       if ( _isUpdatingTexture ) return;
       _isUpdatingTexture = true;

       try 
       {
          string url = $"https://steamcdn-a.akamaihd.net/steam/apps/{steamId}/library_600x900.jpg";
          var tex = await Texture.LoadAsync( url );

          // Fallback to GMod (4000) if needed
          if ( tex == null || tex.Width <= 1 )
          {
             tex = await Texture.LoadAsync( "https://steamcdn-a.akamaihd.net/steam/apps/4000/library_600x900.jpg" );
          }

          if ( tex != null && TargetRenderer.IsValid && TargetRenderer.SceneObject != null )
          {
             TargetRenderer.SceneObject.Attributes.Set( "Artwork", tex );
          }
       }
       catch ( Exception e ) { Log.Error( $"[Idlemon] Texture Error: {e.Message}" ); }
       finally { _isUpdatingTexture = false; }
    }
}
