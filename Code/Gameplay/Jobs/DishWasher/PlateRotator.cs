using Sandbox;

namespace Sinvest;

public sealed class PlateRotator : Component
{
	[Property] public float RotationSpeed { get; set; } = 10.0f;

	private bool _wasRotating = false;

	protected override void OnUpdate()
	{
		bool isRotating = Input.Down( "attack2" );

		// Always manage mouse visibility — even if there's no active plate
		if ( !isRotating )
		{
			Mouse.Visibility = MouseVisibility.Visible;
			_wasRotating = false;
			return;
		}

		Mouse.Visibility = MouseVisibility.Hidden;

		// Get whichever Plate_Root is currently in the workspace
		var plateRoot = DishwasherManager.Instance?.ActivePlateRoot;
		if ( !plateRoot.IsValid() )
		{
			_wasRotating = true;
			return;
		}

		// Skip the first frame after RMB press — MouseDelta spikes on visibility switch
		if ( !_wasRotating )
		{
			_wasRotating = true;
			return;
		}

		float mouseX = Input.MouseDelta.x;
		float mouseY = Input.MouseDelta.y;

		var rotationX = Rotation.FromAxis( Scene.Camera.WorldRotation.Up, -mouseX * RotationSpeed * Time.Delta );
		var rotationY = Rotation.FromAxis( Scene.Camera.WorldRotation.Right, mouseY * RotationSpeed * Time.Delta );

		plateRoot.WorldRotation = rotationX * rotationY * plateRoot.WorldRotation;
	}
}
