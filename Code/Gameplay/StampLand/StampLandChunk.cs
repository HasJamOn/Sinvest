using Sandbox;
using System.Collections.Generic;

public sealed class StampLandChunk : Component
{
	[Property] public Vector2Int ChunkCoords { get; set; }
	private List<StampLandCube> _cubes = new();
	private bool _isVisible = true;

	protected override void OnStart()
	{
		_cubes.AddRange( Components.GetAll<StampLandCube>( FindMode.EverythingInSelfAndChildren ) );
	}

	protected override void OnUpdate()
	{
		if ( Scene.Camera is null ) return;

		float dist = Vector3.DistanceBetween( WorldPosition, Scene.Camera.WorldPosition );
		bool shouldBeVisible = dist < 3000f;

		if ( shouldBeVisible != _isVisible )
		{
			_isVisible = shouldBeVisible;
			foreach ( var cube in _cubes )
			{
				if ( cube.Renderer.IsValid() ) cube.Renderer.Enabled = _isVisible;
				if ( cube.TextComponent.IsValid() ) cube.TextComponent.Enabled = _isVisible;
			}
		}
	}
}
