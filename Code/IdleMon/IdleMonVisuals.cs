using Sandbox;
using System;

namespace Sinvest;

public sealed class IdleMonVisuals : Component
{
	[Property] public ModelRenderer ModelVisual { get; set; }

	public void UpdateFromData( IdleMonData data )
	{
		if ( !ModelVisual.IsValid() ) return;

		ModelVisual.Enabled = data.ID != Guid.Empty;

		// Shader/material attribute pass-through goes here in future steps.
	}
}
