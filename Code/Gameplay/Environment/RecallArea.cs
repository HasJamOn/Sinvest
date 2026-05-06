using Sandbox; 
using Sinvest;

namespace Sinvest;

/// <summary>
/// Defines a spatial volume used by the Recall system to determine if an object
/// is "within bounds." If a RecallRewind component exits this volume, the countdown begins.
/// </summary>
[Title( "Recall Area" )]
[Category( "Physics" )]
[Icon( "CheckBoxOutlineBlank" )]
public sealed class RecallArea : Component
{
	/// <summary>
	/// The dimensions of the volume in units.
	/// </summary>
	[Property] public Vector3 BoxSize { get; set; } = 100f;

	/// <summary>
	/// Calculates the Axis-Aligned Bounding Box (AABB) in world space based on the current position.
	/// </summary>
	/// <returns>A BBox centered at the GameObject's WorldPosition.</returns>
	public BBox GetWorldBounds()
	{
		// Calculate min and max corners by offsetting half-extents from the center
		return new BBox( WorldPosition - ( BoxSize * 0.5f ), WorldPosition + ( BoxSize * 0.5f ) );
	}

	/// <summary>
	/// Visualizes the detection volume within the Scene Editor.
	/// Draws a transparent cyan solid box and a solid cyan wireframe.
	/// </summary>
	protected override void DrawGizmos()
	{
		// Define local bounds relative to the object's transform
		BBox localBounds = new BBox( -BoxSize * 0.5f, BoxSize * 0.5f );
       
		// Draw filled volume for depth perception
		Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.1f );
		Gizmo.Draw.SolidBox( localBounds );
       
		// Draw wireframe for edge clarity
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Draw.LineBBox( localBounds );
	}
}
