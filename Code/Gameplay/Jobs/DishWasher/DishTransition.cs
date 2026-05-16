using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinvest;

/// <summary>
/// Attached to each dish in the pile.
/// ModelVisual = this dish's own Plate_Root instance (set by DishPile at spawn time).
/// SpriteVisual = the billboard sprite child (needs SphereCollider, IsTrigger=true, Radius~17).
/// </summary>
public sealed class DishTransition : Component
{
	[Property] public GameObject SpriteVisual { get; set; }

	/// <summary>This dish's own Plate_Root instance. Set by DishPile at spawn time.</summary>
	[Property] public GameObject ModelVisual { get; set; }

	/// <summary>
	/// The DishwasherDirtManager on this dish's Plate_Root.
	/// Used by DishwasherManager to update the HUD when this dish is focused.
	/// </summary>
	public DishwasherDirtManager DirtManager =>
		ModelVisual?.Components.GetInDescendants<DishwasherDirtManager>();

	protected override void OnStart()
	{
		SetMode( false );
	}

	public void SetMode( bool isWorking )
	{
		if ( SpriteVisual.IsValid() ) SpriteVisual.Enabled = !isWorking;
		if ( ModelVisual.IsValid() ) ModelVisual.Enabled = isWorking;
	}

	protected override void OnUpdate()
	{
		if ( SpriteVisual is null || !SpriteVisual.Enabled ) return;
		if ( !Input.Pressed( "attack1" ) ) return;

		// Find the camera framing this specific minigame instance instead of using the global player head
		var activeCam = Scene.GetAllComponents<CameraComponent>()
			.FirstOrDefault( c => c.Enabled && c.GameObject.WorldPosition.Distance( GameObject.WorldPosition ) < 1000f );

		// Fallback to Scene.Camera if no local workstation camera is found
		var cameraToUse = activeCam ?? Scene.Camera;

		Log.Info( $"[DishTrace] Ray shooting from Camera: '{cameraToUse.GameObject.Name}' at {cameraToUse.GameObject.WorldPosition}" );

		var ray = cameraToUse.ScreenPixelToRay( Mouse.Position );
		var tr = Scene.Trace
			.Ray( ray, 500f ) // 500f is plenty now that the ray starts at the actual table camera!
			.UsePhysicsWorld()
			.HitTriggers()
			.WithoutTags( "player" ) 
			.Run();

		if ( !tr.Hit ) return;

		var hit = tr.GameObject?.Components.GetInAncestorsOrSelf<DishTransition>();
		if ( hit != this ) return;

		Log.Info( $"[DishTransition] Success! Clicked {GameObject.Name}" );
		DishwasherManager.Instance?.RequestFocus( this );
	}
}
