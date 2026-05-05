using Sandbox;
using Sandbox.UI;

namespace Sinvest;

/// <summary>
/// A specialized ScenePanel that renders 3D IdleMon models directly into the UI.
/// Manages its own internal RenderScene and camera positioning.
/// </summary>
public class IdleMonDisplay : ScenePanel
{
    private GameObject _monInstance;
    private CameraComponent _uiCamera;
    private string _currentModelPath;

    protected override void OnAfterTreeRender( bool firstTime )
    {
       base.OnAfterTreeRender( firstTime );
       if ( _uiCamera.IsValid() ) return;

       // RenderScene.Push ensures that any GameObjects created are isolated 
       // from the main game world and exist only within this UI component.
       using ( RenderScene.Push() )
       {
          var camObj = new GameObject( true, "UI_Camera" );

          _uiCamera = camObj.Components.Create<CameraComponent>();
          _uiCamera.FieldOfView = 30;
          _uiCamera.BackgroundColor = Color.Transparent;
          _uiCamera.ZNear = 1f;
          _uiCamera.ZFar = 5000f;

          // ScenePanel automatically renders the camera marked as 'Main' within its isolated scene.
          _uiCamera.IsMainCamera = true;

          ResetCameraFallback();
       }
    }

    /// <summary>
    /// Positions the camera to a standard 3/4 view in case the prefab lacks a dedicated UI camera.
    /// </summary>
    private void ResetCameraFallback()
    {
       var targetPoint = Vector3.Up * 20f;
       _uiCamera.LocalPosition = new Vector3( -120, 0, 40 );
       _uiCamera.LocalRotation = Rotation.LookAt( targetPoint - _uiCamera.LocalPosition );
       _uiCamera.FieldOfView = 30;
    }

    /// <summary>
    /// Loads the 3D model. Uses a template prefab to support complex visual effects (shaders/particles).
    /// </summary>
    /// <param name="modelPath">Optional path to a .vmdl override.</param>
    public void SetModel( string modelPath = null )
    {
       if ( !_uiCamera.IsValid() ) return;

       var targetPath = modelPath ?? string.Empty;
       
       // Optimization: Avoid rebuilding the scene if the model hasn't changed.
       if ( _monInstance.IsValid() && _currentModelPath == targetPath ) return;
       
       _currentModelPath = targetPath;
       _monInstance?.Destroy();

       var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/idlemon/idlemon_template.prefab" );
       if ( prefab == null ) return;

       using ( RenderScene.Push() )
       {
          _monInstance = GameObject.Clone( prefab.ResourcePath, new CloneConfig { StartEnabled = true } );

          // PREFAB CAMERA OVERRIDE:
          // If the model prefab has a CameraComponent, we steal its transform to ensure 
          // different sized models are framed perfectly.
          var prefabCam = _monInstance.Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
          if ( prefabCam.IsValid() )
          {
             _uiCamera.WorldPosition = prefabCam.WorldPosition;
             _uiCamera.WorldRotation = prefabCam.WorldRotation;
             _uiCamera.FieldOfView = prefabCam.FieldOfView;
             prefabCam.Enabled = false; // Disable the copy to prevent rendering conflicts
          }
          else
          {
             ResetCameraFallback();
          }
       }

       if ( !_monInstance.IsValid() ) return;

       _monInstance.LocalPosition = Vector3.Zero;
       _monInstance.LocalRotation = Rotation.Identity;

       if ( !string.IsNullOrEmpty( modelPath ) )
       {
          ApplyModelOverride( modelPath );
       }
    }

    /// <summary>
    /// Swaps the underlying mesh of the ModelRenderer or IdleMonVisuals component.
    /// </summary>
    private void ApplyModelOverride( string path )
    {
       if ( !_monInstance.IsValid() ) return;
       var model = Model.Load( path );
       if ( model == null ) return;

       var visuals = _monInstance.Components.Get<IdleMonVisuals>( FindMode.EverythingInSelfAndDescendants );
       if ( visuals.IsValid() && visuals.ModelVisual.IsValid() )
       {
          visuals.ModelVisual.Model = model;
          return;
       }

       var renderer = _monInstance.Components.Get<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
       if ( renderer.IsValid() ) renderer.Model = model;
    }

    /// <summary>
    /// Passes ASMD stats and SteamID to the visual component to drive shader parameters.
    /// </summary>
    public void UpdateStats( IdleMonData data, int steamId = 4000 )
    {
       if ( !_monInstance.IsValid() ) return;

       var visuals = _monInstance.Components.Get<IdleMonVisuals>( FindMode.EverythingInSelfAndDescendants );
       visuals?.UpdateFromData( data, steamId );
    }

    public override void Tick()
    {
       base.Tick();
       if ( !_monInstance.IsValid() ) return;

       // Purely cosmetic: Rotates the model so the player can see it from all angles in the UI.
       _monInstance.LocalRotation *= Rotation.FromYaw( RealTime.Delta * 50f );
    }

    public override void OnDeleted()
    {
       base.OnDeleted();
       _monInstance?.Destroy();
    }
}
