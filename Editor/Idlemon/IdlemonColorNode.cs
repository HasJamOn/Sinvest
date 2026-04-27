using Sandbox;
using System;
using Editor;
using Editor.ShaderGraph;

[Title( "Idlemon Generation Color Map" ), Category( "Idlemon" ), Icon( "loop" )]
public class IdlemonColorNode : ShaderNode
{
	[Input( typeof( float ) )]
	[Title( "Gen Number" )]
	public NodeInput GenerationInput { get; set; }

	[Output( typeof( Vector3 ) )]
	[Title( "RGB" )]
	public NodeResult.Func Result => ( GraphCompiler compiler ) =>
	{
		var input = compiler.Result( GenerationInput );
		string rawGen = input.Cast( 1, 0.0f );

		// Use fmod in the shader code to wrap the value.
		// Generation 1.5, 2.5, 3.5 will all result in 0.5 (Uncommon).
		// We divide by 10 to spread the loop over 10 generations, 
		// or just use fmod(rawGen, 1.0) if you want it to loop every 1 generation.
		// Use 0.066 because 1 / 15 steps is ~0.066
		// This makes every +1 Generation jump exactly one "if" statement down the list
		string wrappedGen = $"fmod({rawGen} * 0.066, 1.0)";

		return new NodeResult( 3, $@"
        ({wrappedGen} < 0.10 ? float3(0.29, 0.23, 0.16) : // Tier 1
         {wrappedGen} < 0.20 ? float3(0.42, 0.35, 0.23) : 
         {wrappedGen} < 0.30 ? float3(0.48, 0.48, 0.48) : // Tier 2
         {wrappedGen} < 0.40 ? float3(0.63, 0.63, 0.63) : 
         {wrappedGen} < 0.50 ? float3(0.55, 0.38, 0.22) : // Tier 3
         {wrappedGen} < 0.60 ? float3(0.72, 0.45, 0.20) : 
         {wrappedGen} < 0.68 ? float3(0.54, 0.00, 0.00) : // Tier 4
         {wrappedGen} < 0.72 ? float3(0.76, 0.07, 0.12) : 
         {wrappedGen} < 0.78 ? float3(0.35, 0.16, 0.51) : // Tier 5
         {wrappedGen} < 0.82 ? float3(0.48, 0.25, 0.63) : 
         {wrappedGen} < 0.86 ? float3(0.17, 0.35, 0.63) : // Tier 6
         {wrappedGen} < 0.90 ? float3(0.23, 0.48, 0.84) : 
         {wrappedGen} < 0.95 ? float3(0.75, 0.75, 0.75) : // Tier 7
         {wrappedGen} < 0.98 ? float3(0.83, 0.69, 0.22) : // Tier 8
         {wrappedGen} < 1.00 ? float3(0.00, 0.61, 0.47) : // Tier 9
         float3(0.00, 1.00, 1.00))" );                    // Reset/Divine
	};
}
