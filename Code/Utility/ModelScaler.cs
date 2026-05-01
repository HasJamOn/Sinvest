using Sandbox;

namespace Sinvest;

[EditorHandle]
public sealed class ModelScaler : Component, Component.ExecuteInEditor
{
	[Property] 
	[Description( "Optional: Target a specific renderer. If empty, looks on this GameObject." )]
	public ModelRenderer TargetRenderer { get; set; }

	[Property] public Vector3 ModelScale { get; set; } = Vector3.One;

	protected override void OnUpdate()
	{
		ApplyScale();
	}

	protected override void OnPreRender()
	{
		ApplyScale();
	}

	private void ApplyScale()
	{
		// Since Prop : ModelRenderer, Get<ModelRenderer> will find either.
		var renderer = TargetRenderer.IsValid() ? TargetRenderer : Components.Get<ModelRenderer>();

		if ( !renderer.IsValid() ) 
			return;

		var so = renderer.SceneObject;
		if ( !so.IsValid() ) 
			return;

		var tx = so.Transform;
		
		// Avoid constant transform updates if the scale hasn't changed
		if ( tx.Scale == ModelScale ) 
			return;

		tx.Scale = ModelScale;
		so.Transform = tx;
	}
}
