using Sandbox;
using Editor;
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sandbox.UI.Casino.Games.Cards;

namespace Sandbox;

public static class SinvestEditorMenu
{
    [Menu( "Editor", "Sinvest/Generate Card Resources" )]
    public static void GenerateCards()
    {
        // 1. PHYSICAL PATHS
        string projectDir = @"C:\Users\Admin\Documents\s&box projects\sinvest";
        string pngDir = Path.Combine( projectDir, "Assets", "UI", "Casino", "Cards", "art", "Cards_large" );
        string saveDir = Path.Combine( projectDir, "Assets", "UI", "Casino", "Cards", "data" );

        if ( !Directory.Exists( pngDir ) )
        {
            EditorUtility.DisplayDialog( "Error", "Source folder not found:\n" + pngDir );
            return;
        }

        if ( !Directory.Exists( saveDir ) )
            Directory.CreateDirectory( saveDir );

        // 2. PROCESS FILES
        var files = Directory.GetFiles( pngDir, "card_*.png" );
        int count = 0;

        foreach ( var filePath in files )
        {
            try
            {
                string fileName = Path.GetFileName( filePath );
                string nameNoExt = fileName.Replace( ".png", "" );
                string[] parts = nameNoExt.Split( '_' );
                
                if ( parts.Length < 3 ) continue;

                string suitStr = CultureInfo.CurrentCulture.TextInfo.ToTitleCase( parts[1] );
                string rankStr = parts[2];

                if ( !Enum.TryParse<Suit>( suitStr, out Suit suit ) ) continue;
                Rank rank = ParseRankString( rankStr );
                
                string savePath = Path.Combine( saveDir, rank.ToString().ToLower() + "_" + suit.ToString().ToLower() + ".card" );

                // 3. CREATE DATA OBJECT
				// Adding "__type" so s&box recognizes this as your specific GameResource
				var cardData = new
				{
					__type = "Sandbox.UI.Casino.Games.Cards.CardResource", // Matches your new namespace
					CardName = rank.ToString() + " of " + suit.ToString(),
					CardImage = "UI/Casino/Cards/art/Cards_large/" + fileName,
					CardSuit = (int)suit,
					CardRank = (int)rank,
					CardID = ((int)suit * 13) + (int)rank
				};

				// 4. SERIALIZE AND WRITE
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize( cardData, options );

                File.WriteAllText( savePath, jsonString );
                count++;
            }
            catch ( Exception ex )
            {
                Log.Warning( "Sinvest: Error on " + filePath + ": " + ex.Message );
            }
        }

        EditorUtility.DisplayDialog( "Success", "Wrote " + count + " clean .card files to data folder!" );
        AssetSystem.RegisterFile( "Assets/UI/Casino/Cards/data" );
    }

    private static Rank ParseRankString( string r )
    {
        return r.ToUpper() switch
        {
            "A" => Rank.Ace, "J" => Rank.Jack, "Q" => Rank.Queen, "K" => Rank.King,
            "02" => Rank.Two, "03" => Rank.Three, "04" => Rank.Four, "05" => Rank.Five,
            "06" => Rank.Six, "07" => Rank.Seven, "08" => Rank.Eight, "09" => Rank.Nine, "10" => Rank.Ten,
            _ => Rank.Two
        };
    }
}
