using Sandbox;
using System.Linq;

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

		// Find the active camera framing this specific minigame instance instead of global player eyes
		var activeCam = Scene.GetAllComponents<CameraComponent>()
			.FirstOrDefault( c => c.Enabled && c.GameObject.WorldPosition.Distance( GameObject.WorldPosition ) < 1000f );

		// Fallback safely to Scene.Camera if no workspace context is found
		var cameraToUse = activeCam ?? Scene.Camera;

		float mouseX = Input.MouseDelta.x;
		float mouseY = Input.MouseDelta.y;

		// Compute axes based explicitly on the camera actually framing the plate!
		var rotationX = Rotation.FromAxis( cameraToUse.WorldRotation.Up, -mouseX * RotationSpeed * Time.Delta );
		var rotationY = Rotation.FromAxis( cameraToUse.WorldRotation.Right, mouseY * RotationSpeed * Time.Delta );

		plateRoot.WorldRotation = rotationX * rotationY * plateRoot.WorldRotation;
	}
}
