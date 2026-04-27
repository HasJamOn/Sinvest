using Sandbox;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sinvest;

public sealed class IdlemonDebugger_finishline : Component
{
    [Property, Group( "Target" )] public IdlemonCase TargetCase { get; set; }
    [Property, Group( "Target" )] public IdleMonVisuals TargetVisuals { get; set; }

    [Header( "Manual Overrides" )]
    [Property] public int SteamId { get; set; } = 4000;
    [Property] public double Addition { get; set; } = 0;
    [Property] public double Multiplier { get; set; } = 1.0;
    // ... (rest of your properties remain the same)

    [Button( "Force Full Refresh", "refresh" )]
    public void ForceRefresh()
    {
        var dummyData = CreateDummy();
        if ( TargetVisuals.IsValid() )
            TargetVisuals.UpdateFromData( dummyData, SteamId );
    }

   [Button( "RUN DEEP DIAGNOSTICS", "search" )]
    public async void RunDiagnostics()
    {
        Log.Info( "--- STARTING IDLEMON MYSTERY SOLVER ---" );

        if ( !TargetVisuals.IsValid() ) 
        {
            Log.Error( "❌ DIAGNOSTIC: TargetVisuals is NOT linked!" );
            return;
        }

        var renderer = TargetVisuals.ModelVisual;
        if ( !renderer.IsValid() )
        {
            Log.Error( "❌ DIAGNOSTIC: ModelRenderer is missing!" );
        }
        else
        {
            // NEW FIX: Access the material via the Renderer's own property
            // This bypasses the SceneObject.GetMaterial error
            var mat = renderer.MaterialOverride;
            string matName = mat.IsValid() ? mat.Name : "No Material Found";

            Log.Info( "✅ DIAGNOSTIC: Renderer Found." );
            Log.Info( $"👉 Model: {renderer.Model?.Name ?? "None"}" );
            Log.Info( $"👉 Material: {matName}" );

            if ( matName.Contains( "white" ) || matName.Contains( "error" ) )
            {
                Log.Warning( "⚠️ WARNING: Using a default material! Custom attributes like 'Artwork' won't work on this." );
            }
        }

        Log.Info( $"Checking Steam CDN for AppID: {SteamId}..." );
        await TestSteamDownload( SteamId );

        Log.Info( "--- DIAGNOSTICS COMPLETE ---" );
    }

    private async Task TestSteamDownload( int id )
    {
        string url = $"https://steamcdn-a.akamaihd.net/steam/apps/{id}/library_600x900.jpg";
    
        try
        {
           var tex = await Texture.LoadAsync( url );
           if ( tex != null && !tex.IsError )
           {
              Log.Info( $"✅ SUCCESS: Downloaded {id} artwork ({tex.Width}x{tex.Height})." );
            
              var renderer = TargetVisuals?.ModelVisual;
              if ( renderer.IsValid() )
              {
                 // KEY FIX: Use the Component's Attributes, not just the SceneObject's
                 renderer.Attributes.Set( "Artwork", tex );
                 Log.Info( "👉 ACTION: Pushed texture to attribute 'Artwork' on the Renderer." );
              }
           }
           else
           {
              Log.Error( $"❌ FAIL: Texture.LoadAsync failed for ID {id}. URL: {url}" );
           }
        }
        catch ( Exception e )
        {
           Log.Error( $"❌ CRITICAL: {e.Message}" );
        }
    }

    private IdleMonData CreateDummy() => new IdleMonData 
    { 
        ID = Guid.NewGuid(), Name = "DEBUG_NODE", SteamId = SteamId, 
        Multiplier = Multiplier, Addition = Addition, ModelPath = "models/dev/box.vmdl" 
    };

    protected override void OnUpdate()
    {
       Gizmo.Draw.Text( $"🔍 DEBUGGING ID: {SteamId}", new Transform( WorldPosition + Vector3.Up * 25f ) );
    }
}
