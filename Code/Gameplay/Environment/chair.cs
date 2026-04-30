using Sandbox; 
using Sinvest;

public sealed class Chair : Component
{
	[Property] public string Name { get; set; } = "Chair";
	[Property] public string Description { get; set; } = "A chair.";
}
