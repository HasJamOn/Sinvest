using Sandbox;
using Sandbox.UI;

namespace Sinvest;

public class IdleMonDisplay : ScenePanel
{
    private GameObject _monInstance;
    private CameraComponent _uiCamera;
    private string _currentModelPath;

    protected override void OnAfterTreeRender( bool firstTime )
    {
	    base.OnAfterTreeRender( firstTime );
	    if ( _uiCamera != null ) return;

	    using ( RenderScene.Push() )
	    {
		    var camObj = new GameObject( true, "UI_Camera" );

		    _uiCamera = camObj.Components.Create<CameraComponent>();
		    _uiCamera.FieldOfView = 30;
		    _uiCamera.BackgroundColor = Color.Transparent;
		    _uiCamera.ZNear = 1f;
		    _uiCamera.ZFar = 5000f;

		    // MUST be true — ScenePanel renders via RenderScene.Camera,
		    // which is only populated when a camera in the scene has IsMainCamera = true.
		    // No conflict risk here since this is a private isolated scene.
		    _uiCamera.IsMainCamera = true;

		    var targetPoint = Vector3.Up * 20f;
		    camObj.LocalPosition = new Vector3( -120, 0, 40 );
		    camObj.LocalRotation = Rotation.LookAt( targetPoint - camObj.LocalPosition );
	    }
    }
    
    public void UpdateStats( IdleMonData data, int steamId = 4000 )
    {
	    if ( !_monInstance.IsValid() ) return;

	    var visuals = _monInstance.Components.Get<IdleMonVisuals>( FindMode.EverythingInSelfAndDescendants );
	    if ( visuals.IsValid() )
	    {
		    // Now visuals can handle the data and the specific steamId
		    visuals.UpdateFromData( data, steamId );
	    }
    }

    public void SetModel( string modelPath )
    {
	    if ( _uiCamera == null ) return;

	    var targetPath = modelPath ?? string.Empty;
	    if ( _monInstance.IsValid() && _currentModelPath == targetPath ) return;
	    _currentModelPath = targetPath;

	    _monInstance?.Destroy();
    
	    var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/idlemon/idlemon_template.prefab" );
	    if ( prefab == null ) return;

	    using ( RenderScene.Push() )
	    {
		    _monInstance = GameObject.Clone( prefab.ResourcePath, new CloneConfig { StartEnabled = true } );
        
		    // --- NEW CAMERA OVERRIDE LOGIC ---
		    // Look for a camera component anywhere inside the prefab
		    var prefabCam = _monInstance.Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
		    if ( prefabCam.IsValid() )
		    {
			    // Sync our ScenePanel camera to the prefab's camera settings
			    _uiCamera.WorldPosition = prefabCam.WorldPosition;
			    _uiCamera.WorldRotation = prefabCam.WorldRotation;
			    _uiCamera.FieldOfView = prefabCam.FieldOfView;
            
			    // Disable the camera component inside the prefab so it doesn't try to 
			    // render to the main screen, we only want its transform data.
			    prefabCam.Enabled = false; 
		    }
		    else
		    {
			    // Fallback: If no camera in prefab, reset to your default hardcoded view
			    var targetPoint = Vector3.Up * 20f;
			    _uiCamera.LocalPosition = new Vector3( -120, 0, 40 );
			    _uiCamera.LocalRotation = Rotation.LookAt( targetPoint - _uiCamera.LocalPosition );
			    _uiCamera.FieldOfView = 30;
		    }
	    }

	    if ( !_monInstance.IsValid() ) return;
	    _monInstance.LocalPosition = Vector3.Zero;
	    _monInstance.LocalRotation = Rotation.Identity;

	    if ( !string.IsNullOrEmpty( modelPath ) )
		    ApplyModelOverride( modelPath );
    }

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

    public override void Tick()
    {
	    base.Tick();
	    if ( !_monInstance.IsValid() ) return;

	    // Constant rotation for the UI view
	    _monInstance.LocalRotation *= Rotation.FromYaw( RealTime.Delta * 50f ); 
    }

    public override void OnDeleted()
    {
        base.OnDeleted();
        // Do NOT destroy _uiCamera manually — it lives in RenderScene which
        // ScenePanel already destroys in its own Delete() because _ownsScene = true.
        _monInstance?.Destroy();
    }
}
