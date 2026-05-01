using Sandbox;
using System;

public sealed class StampLandCube : Component
{
	[Sync] public Guid OwnerId { get; set; }
	[Sync] public int Value { get; set; } = 0;

	[Property, Group("References")] public TextRenderer TextComponent { get; set; }
	[Property, Group("References")] public ModelRenderer Renderer { get; set; }

	protected override void OnUpdate()
	{
		if ( !Renderer.Enabled ) return;

		if ( TextComponent.IsValid() )
			TextComponent.Text = Value > 0 ? Value.ToString() : "";

		UpdateVisuals();
	}

	private void UpdateVisuals()
	{
		if ( Value == 0 )
		{
			// Using explicit float values for rgba consistency
			Renderer.Tint = new Color( 0.5f, 0.5f, 0.5f, 1.0f ); 
			return;
		}

		// Local vs Remote Player coloring
		Renderer.Tint = (OwnerId == Connection.Local.Id) 
			? new Color( 0.0f, 1.0f, 0.0f, 1.0f ) // Green
			: new Color( 1.0f, 0.0f, 0.0f, 1.0f ); // Red
	}

	public void ProcessTouch( Guid playerId )
	{
		if ( !Networking.IsHost ) return;

		if ( Value == 0 ) { OwnerId = playerId; Value = 1; }
		else if ( OwnerId == playerId ) { Value++; }
		else {
			Value--;
			if ( Value <= 0 ) { Value = 0; OwnerId = Guid.Empty; }
		}
	}
}
