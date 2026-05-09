using Sandbox;

namespace Sinvest;

public sealed class PlateRotator : Component
{
	[Property] public GameObject PlateRoot { get; set; }
	[Property] public float RotationSpeed { get; set; } = 5.0f;

	protected override void OnUpdate()
	{
		if ( PlateRoot == null ) return;

		// Only rotate while Attack2 (Right Click) is held
		if ( Input.Down( "attack2" ) )
		{
			// Fetch mouse input deltas
			float mouseX = Input.MouseDelta.x;
			float mouseY = Input.MouseDelta.y;

			// Convert mouse movement into rotation increments
			// We use the Camera's Up and Right vectors to ensure rotation 
			// feels intuitive relative to what the player sees.
			var rotationX = Rotation.FromAxis( Scene.Camera.WorldRotation.Up, -mouseX * RotationSpeed * Time.Delta );
			var rotationY = Rotation.FromAxis( Scene.Camera.WorldRotation.Right, mouseY * RotationSpeed * Time.Delta );

			// Apply the rotation to the PlateRoot
			PlateRoot.WorldRotation = rotationX * rotationY * PlateRoot.WorldRotation;
		}
	}
}
