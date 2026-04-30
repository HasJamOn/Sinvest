using Sandbox;
using System;

namespace Sinvest;

public sealed class IdleMonVisuals : Component
{
	[Property] public ModelRenderer ModelVisual { get; set; }
    
	public void UpdateFromData( IdleMonData data, int steamId = 4000 )
	{
		if ( !ModelVisual.IsValid() ) return;

		bool hasData = data.ID != Guid.Empty;
		ModelVisual.Enabled = hasData;

		if ( !hasData ) return;

		var attr = ModelVisual.SceneObject?.Attributes;
		if ( attr == null ) return;

		// Push stats to shader once (OnUpdate handles the time)
		attr.Set( "Addition", (float)data.Addition );
		attr.Set( "Multiplier", (float)data.Multiplier );
		attr.Set( "Subtraction", (float)data.Subtraction );
		attr.Set( "Division", (float)data.Division );
		attr.Set( "Luck", (float)data.Luck );
		attr.Set( "TeamBonus", (float)data.TeamBonus );
		attr.Set( "Efficiency", (float)data.CostEfficiency );
		attr.Set( "Generation", (float)data.Generation );
    
		// Pass the ID to the Case logic
		var caseLogic = Components.Get<IdlemonCase>( FindMode.EverythingInSelfAndDescendants );
		if ( caseLogic.IsValid() )
		{
			caseLogic.UpdateAllStats( steamId, data );
		}
	}

	protected override void OnUpdate()
	{
		if ( ModelVisual.IsValid() && ModelVisual.SceneObject != null )
		{
			// The single time source for all shader animations
			ModelVisual.SceneObject.Attributes.Set( "ShaderTime", RealTime.Now );
		}
	}
}
