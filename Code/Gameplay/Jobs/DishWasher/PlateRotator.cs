using Sandbox;

namespace Sinvest;

public sealed class PlateRotator : Component
{
	[Property] public GameObject PlateRoot { get; set; }
	[Property] public float RotationSpeed { get; set; } = 5.0f;

	private bool _wasRotating = false;

	protected override void OnUpdate()
	{
		if ( !PlateRoot.IsValid() ) return;

		bool isRotating = Input.Down( "attack2" );

		if ( isRotating )
		{
			Mouse.Visibility = MouseVisibility.Hidden;

			// Skip input on the first frame of capture — MouseDelta spikes
			// on the frame visibility switches, causing a large jump that
			// leaves decal scene objects one frame behind (visible flicker).
			if ( !_wasRotating )
			{
				_wasRotating = true;
				return;
			}

			float mouseX = Input.MouseDelta.x;
			float mouseY = Input.MouseDelta.y;

			var rotationX = Rotation.FromAxis( Scene.Camera.WorldRotation.Up, -mouseX * RotationSpeed * Time.Delta );
			var rotationY = Rotation.FromAxis( Scene.Camera.WorldRotation.Right, mouseY * RotationSpeed * Time.Delta );

			PlateRoot.WorldRotation = rotationX * rotationY * PlateRoot.WorldRotation;
		}
		else
		{
			Mouse.Visibility = MouseVisibility.Visible;
			_wasRotating = false;
		}
	}
}
