using Sandbox;
using System;

namespace Sinvest;

public sealed class IdleMonVisuals : Component
{
	[Property] public ModelRenderer ModelVisual { get; set; }

	public void UpdateFromData( IdleMonData data )
	{
		if ( !ModelVisual.IsValid() ) return;

		// Hide the model if the slot is empty
		ModelVisual.Enabled = data.ID != Guid.Empty;

		// NOTE: This is exactly where we will pass the stats 
		// to the Shader material in the next steps!
	}
    
	protected override void OnUpdate()
	{
		// Give it that nice inventory rotation effect natively in the world
		if ( ModelVisual.IsValid() && ModelVisual.Enabled )
		{
			ModelVisual.Transform.Rotation *= Rotation.FromYaw( Time.Delta * 45f );
		}
	}
}
