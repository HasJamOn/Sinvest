namespace Sinvest;

public sealed class FloatingIncome
{
	/// <summary>
	/// The formatted text string displayed to the user.
	/// </summary>
	public string Text { get; set; }

	/// <summary>
	/// The current time in seconds this layout block has been active.
	/// </summary>
	public float Age { get; set; } = 0f;

	/// <summary>
	/// Cumulative pixel displacement used to float the text upward.
	/// </summary>
	public float TopOffset { get; set; } = 0f;

	/// <summary>
	/// When true, renders the gold 'PERFECT!' layout badge over the text block.
	/// </summary>
	public bool IsPerfect { get; set; } = false;

	/// <summary>
	/// Explicit flag indicating a high-contrast warning text block.
	/// Used to completely bypass chained selector bugs inside Panorama.
	/// </summary>
	public bool IsError { get; set; } = false;
}
