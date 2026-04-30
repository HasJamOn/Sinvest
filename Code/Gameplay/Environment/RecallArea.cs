using Sandbox; 
using Sinvest;

namespace Sinvest;

[Title( "Recall Area" )]
[Category( "Physics" )]
[Icon( "CheckBoxOutlineBlank" )]
public sealed class RecallArea : Component
{
	[Property] public Vector3 BoxSize { get; set; } = 100f;

	public BBox GetWorldBounds()
	{
		// Returns a box centered on this GameObject's world position
		return new BBox( WorldPosition - ( BoxSize * 0.5f ), WorldPosition + ( BoxSize * 0.5f ) );
	}

	protected override void DrawGizmos()
	{
		// Draw the area box in the editor
		BBox localBounds = new BBox( -BoxSize * 0.5f, BoxSize * 0.5f );
		Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.1f );
		Gizmo.Draw.SolidBox( localBounds );
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Draw.LineBBox( localBounds );
	}
}
