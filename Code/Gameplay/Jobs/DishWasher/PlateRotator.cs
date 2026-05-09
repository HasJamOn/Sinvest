using Sandbox;

namespace Sinvest;

public sealed class PlateRotator : Component
{
	[Property] public GameObject PlateRoot { get; set; }
	[Property] public float RotationSpeed { get; set; } = 5.0f;

	protected override void OnUpdate()
	{
		if ( !PlateRoot.IsValid() ) return;

		// INDUSTRY STANDARD: Toggle between "Pointer Mode" and "Capture Mode"
		if ( Input.Down( "attack2" ) )
		{
			// 1. Hide and lock the cursor. 
			// In s&box, MouseVisibility.Hidden enables raw delta input 
			// so the cursor doesn't hit the edge of your screen.
			Mouse.Visibility = MouseVisibility.Hidden;

			float mouseX = Input.MouseDelta.x;
			float mouseY = Input.MouseDelta.y;

			var rotationX = Rotation.FromAxis( Scene.Camera.WorldRotation.Up, -mouseX * RotationSpeed * Time.Delta );
			var rotationY = Rotation.FromAxis( Scene.Camera.WorldRotation.Right, mouseY * RotationSpeed * Time.Delta );

			PlateRoot.WorldRotation = rotationX * rotationY * PlateRoot.WorldRotation;
		}
		else
		{
			// 2. Restore the cursor to 'Visible' mode.
			// This allows the user to use the cursor to aim the 'DishwasherRaycast'.
			Mouse.Visibility = MouseVisibility.Visible;
		}
	}
}
