using Sandbox;

[Title( "World Dialogue" )]
[Category( "Sinvest" )]
public sealed class WorldDialogue : Component
{
	[Property] public TextRenderer NameText { get; set; }
	[Property] public TextRenderer MessageText { get; set; }
	[Property] public ModelRenderer BackgroundPlane { get; set; }

	public void UpdateDialogue( string name, string text )
	{
		if ( NameText.IsValid() ) NameText.Text = name;
		if ( MessageText.IsValid() ) MessageText.Text = text;
	}
}
