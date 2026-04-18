using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.UI.Casino.Games.Cards;

public enum Suit { Hearts, Diamonds, Clubs, Spades }
public enum Rank { Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }

// --- CARD RESOURCE ---
[AssetType( Name = "Sinvest Card", Extension = "card", Category = "Sinvest" )]
public partial class CardResource : GameResource
{
	[Property, Group( "Metadata" )] 
	public string CardName { get; set; }

	[Property, Group( "Visuals" ), ResourceType( "png" )] 
	public Texture CardImage { get; set; }

	[Property, Group( "Details" )] 
	public Suit CardSuit { get; set; }

	[Property, Group( "Details" )] 
	public Rank CardRank { get; set; }

	[Property, Group( "Details" )] 
	public int CardID { get; set; }
}

// --- DECK COLLECTION ---
[AssetType( Name = "Deck Collection", Extension = "deck", Category = "Sinvest" )]
public partial class DeckCollection : GameResource
{
	[Property] 
	public List<CardResource> AllCards { get; set; } = new();

	/// <summary>
	/// Scans for all .card resources within the specific casino folder path.
	/// </summary>
	[Button( "Reload Cards from Folder" )]
	public void LoadCardsFromFolder()
	{
		// ResourceLibrary handles the heavy lifting of finding your .card files
		AllCards = ResourceLibrary.GetAll<CardResource>()
			.Where( x => x.ResourcePath.Contains( "ui/casino/cards" ) )
			.OrderBy( x => x.CardID ) 
			.ToList();

		Log.Info( $"DeckCollection: Successfully loaded {AllCards.Count} cards." );
	}
}
