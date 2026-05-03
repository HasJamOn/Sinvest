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
		if ( _uiCamera.IsValid() ) return;

		using ( RenderScene.Push() )
		{
			var camObj = new GameObject( true, "UI_Camera" );

			_uiCamera = camObj.Components.Create<CameraComponent>();
			_uiCamera.FieldOfView = 30;
			_uiCamera.BackgroundColor = Color.Transparent;
			_uiCamera.ZNear = 1f;
			_uiCamera.ZFar = 5000f;

			// ScenePanel renders via RenderScene.Camera, populated by IsMainCamera = true.
			_uiCamera.IsMainCamera = true;

			ResetCameraFallback();
		}
	}

	private void ResetCameraFallback()
	{
		var targetPoint = Vector3.Up * 20f;
		_uiCamera.LocalPosition = new Vector3( -120, 0, 40 );
		_uiCamera.LocalRotation = Rotation.LookAt( targetPoint - _uiCamera.LocalPosition );
		_uiCamera.FieldOfView = 30;
	}

	public void SetModel( string modelPath = null )
	{
		if ( !_uiCamera.IsValid() ) return;

		var targetPath = modelPath ?? string.Empty;
		// If we already have this model loaded, don't flicker/rebuild
		if ( _monInstance.IsValid() && _currentModelPath == targetPath ) return;
		
		_currentModelPath = targetPath;
		_monInstance?.Destroy();

		var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/idlemon/idlemon_template.prefab" );
		if ( prefab == null ) return;

		using ( RenderScene.Push() )
		{
			_monInstance = GameObject.Clone( prefab.ResourcePath, new CloneConfig { StartEnabled = true } );

			// Camera Override Logic
			var prefabCam = _monInstance.Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
			if ( prefabCam.IsValid() )
			{
				_uiCamera.WorldPosition = prefabCam.WorldPosition;
				_uiCamera.WorldRotation = prefabCam.WorldRotation;
				_uiCamera.FieldOfView = prefabCam.FieldOfView;
				prefabCam.Enabled = false;
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

		// Constant rotation for the UI view
		_monInstance.LocalRotation *= Rotation.FromYaw( RealTime.Delta * 50f );
	}

	public override void OnDeleted()
	{
		base.OnDeleted();
		_monInstance?.Destroy();
		// _uiCamera is destroyed by ScenePanel's internal world cleanup
	}
}
