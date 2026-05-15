using Sandbox;

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

		var ray = Scene.Camera.ScreenPixelToRay( Mouse.Position );
		var tr = Scene.Trace
			.Ray( ray, 500f )
			.UsePhysicsWorld()
			.HitTriggers()
			.Run();

		if ( !tr.Hit ) return;

		var hit = tr.GameObject?.Components.GetInAncestorsOrSelf<DishTransition>();
		if ( hit != this ) return;

		Log.Info( $"[DishTransition] Clicked {GameObject.Name}" );
		DishwasherManager.Instance?.RequestFocus( this );
	}
}
