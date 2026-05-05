using Sandbox; 
using Sinvest;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.UI.Casino.Games.Cards;

/// <summary>
/// Standard playing card suits used for logic and asset identification.
/// </summary>
public enum Suit { Hearts, Diamonds, Clubs, Spades }

/// <summary>
/// Standard playing card ranks. Values can be cast to int for numerical comparisons.
/// </summary>
public enum Rank { Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }

/// <summary>
/// A data-driven asset definition for a single playing card.
/// Created as a .card resource in the editor to link logic with textures.
/// </summary>
[AssetType( Name = "Sinvest Card", Extension = "card", Category = "Sinvest" )]
public partial class CardResource : GameResource
{
    [Property, Group( "Metadata" )] 
    public string CardName { get; set; }

    /// <summary> The visual representation of the card face. </summary>
    [Property, Group( "Visuals" ), ResourceType( "png" )] 
    public Texture CardImage { get; set; }

    [Property, Group( "Details" )] 
    public Suit CardSuit { get; set; }

    [Property, Group( "Details" )] 
    public Rank CardRank { get; set; }

    /// <summary> 
    /// Unique index for sorting or network synchronization (e.g., 1-52). 
    /// </summary>
    [Property, Group( "Details" )] 
    public int CardID { get; set; }
}

/// <summary>
/// A container for card assets, allowing for different "Skins" or rulesets.
/// </summary>
[AssetType( Name = "Deck Collection", Extension = "deck", Category = "Sinvest" )]
public partial class DeckCollection : GameResource
{
    /// <summary> The master list of cards belonging to this specific deck. </summary>
    [Property] 
    public List<CardResource> AllCards { get; set; } = new();

    /// <summary>
    /// Editor-only utility to bulk-populate the deck from the card resource folder.
    /// This prevents manual entry errors for a 52-card standard deck.
    /// </summary>
    [Button( "Reload Cards from Folder" )]
    public void LoadCardsFromFolder()
    {
       // Use ResourceLibrary to scan the project for all .card files.
       // Filtered by path to ensure we don't pick up cards from other mini-games.
       AllCards = ResourceLibrary.GetAll<CardResource>()
          .Where( x => x.ResourcePath.Contains( "ui/casino/cards" ) )
          .OrderBy( x => x.CardID ) 
          .ToList();

       Log.Info( $"DeckCollection: Successfully loaded {AllCards.Count} cards." );
    }
}
