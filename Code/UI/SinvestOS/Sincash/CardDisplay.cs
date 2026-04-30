using Sandbox; 
using Sinvest;

namespace Sandbox.UI.Casino.Games.Cards;

public sealed class CardDisplay : Component
{
	[Property] public CardResource Data { get; set; }
	[Property] public SpriteRenderer Renderer { get; set; }

	protected override void OnStart()
	{
		UpdateSprite();
	}

	protected override void OnValidate()
	{
		UpdateSprite();
	}

	public void UpdateSprite()
	{
		if ( Renderer == null ) return;
        
		if ( Data == null )
		{
			Renderer.Sprite = null; // Reset the sprite
			return;
		}

		if ( Data.CardImage != null )
		{
			// The SpriteRenderer expects a Sprite, not just a raw Texture
			// Sprite.FromTexture creates a sprite wrapper around your card image
			Renderer.Sprite = Sprite.FromTexture( Data.CardImage );
            
			Log.Info( $"Successfully set sprite from: {Data.CardImage.ResourcePath}" );
		}
		else
		{
			Log.Warning( "CardDisplay: Data found, but CardImage is null!" );
		}
	}
}
