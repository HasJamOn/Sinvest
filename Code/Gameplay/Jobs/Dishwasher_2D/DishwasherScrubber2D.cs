using Sandbox;

namespace Sinvest;

public sealed class DishwasherScrubber2D : Component
{
	[Property] public float BrushRadius { get; set; } = 0.05f; 
	[Property] public GameObject Surface { get; set; } // Assign your Scrubbing_Surface here

	protected override void OnUpdate()
	{
		// If you want to be extra safe against other components:
		if ( Mouse.Visibility != MouseVisibility.Visible )
		{
			Mouse.Visibility = MouseVisibility.Visible;
		}
		
		if ( !Input.Down( "attack1" ) || !Surface.IsValid() ) return;

		// 1. Get Mouse Position projected into the world
		var mouseRay = Scene.Camera.ScreenPixelToRay( Mouse.Position );
        
		// 2. Map World Position to Local UV
		// ModelRenderer planes are centered, so localPos ranges from -50 to 50
		var localPos = Surface.WorldTransform.PointToLocal( mouseRay.Position );
        
		// 3. Convert to 0-1 UV (Assuming 100x100 unit plane)
		Vector2 uv = new Vector2( 
			(localPos.y / 100f) + 0.5f,  // Map Screen Y to Plane X
			(localPos.x / 100f) + 0.5f   // Map Screen X to Plane Y
		);
		
		if ( Surface.Components.TryGet<Dishwasher2DDynamicMask>( out var mask ) )
		{
			mask.CleanAtUV( uv, BrushRadius );
		}
	}
	
	protected override void OnStart()
	{
		// Force the mouse to be visible even if we aren't hovering over UI
		Mouse.Visibility = MouseVisibility.Visible;
	}
}
