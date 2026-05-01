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
		if ( TextComponent.IsValid() )
			TextComponent.Text = Value.ToString();

		if ( Renderer.IsValid() )
			UpdateVisuals();
	}

	private void UpdateVisuals()
	{
		if ( Value == 0 )
		{
			Renderer.Tint = Color.Gray; 
			return;
		}

		// CORRECTED: Use Connection.Local.Id to identify the local client
		if ( OwnerId == Connection.Local.Id )
		{
			Renderer.Tint = Color.Green; 
		}
		else
		{
			Renderer.Tint = Color.Red; 
		}
	}

	public void ProcessTouch( Guid playerId )
	{
		// Ensure only the Host handles the logic to prevent "Ghost Claims"
		if ( !Networking.IsHost ) return;

		if ( Value == 0 )
		{
			// Log the attempt to see if the Guid is arriving correctly
			Log.Info( $"Claiming cube for player: {playerId}" );
        
			OwnerId = playerId;
			Value = 1;
		}
		else if ( OwnerId == playerId ) 
		{
			Value++;
		}
		else
		{
			Value--;
			if ( Value <= 0 )
			{
				Value = 0;
				OwnerId = Guid.Empty;
			}
		}
	}
}
