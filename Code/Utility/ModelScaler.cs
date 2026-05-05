using Sandbox;

namespace Sinvest;

/// <summary>
/// Directly manipulates the SceneObject transform of a ModelRenderer.
/// Useful for scaling models independently of the GameObject's world transform.
/// </summary>
[EditorHandle]
[Title( "Model Scaler" )]
[Category( "Sinvest/Utility" )]
public sealed class ModelScaler : Component, Component.ExecuteInEditor
{
	[Property]
	[Description( "Optional: Target a specific renderer. If null, searches this GameObject." )]
	public ModelRenderer TargetRenderer { get; set; }

	[Property] 
	public Vector3 ModelScale { get; set; } = Vector3.One;

	/// <summary>
	/// We use OnPreRender to ensure the scale is applied right before the frame is drawn,
	/// catching any external logic that might have reset the SceneObject transform.
	/// </summary>
	protected override void OnPreRender()
	{
		ApplyScale();
	}

	private void ApplyScale()
	{
		var renderer = TargetRenderer.IsValid() ? TargetRenderer : Components.Get<ModelRenderer>();

		if ( !renderer.IsValid() )
			return;

		var so = renderer.SceneObject;
		if ( !so.IsValid() )
			return;

		// The SceneObject transform is separate from the GameObject transform.
		// We modify the internal Scale property of the SceneObject.
		var tx = so.Transform;

		if ( tx.Scale == ModelScale )
			return;

		tx.Scale = ModelScale;
		so.Transform = tx;
	}
}
