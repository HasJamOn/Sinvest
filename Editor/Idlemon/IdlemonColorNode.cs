using Sandbox;
using System;
using Editor;
using Editor.ShaderGraph;

[Title( "Idlemon Color Map" ), Category( "Idlemon" ), Icon( "palette" )]
public class IdlemonColorNode : ShaderNode
{
	[Input( typeof( float ) )]
	[Title( "Rarity (t)" )]
	public NodeInput InputT { get; set; }

	[Output( typeof( Vector3 ) )]
	[Title( "RGB" )]
	public NodeResult.Func Result => ( GraphCompiler compiler ) =>
	{
		// compiler.Result(InputT) returns the NodeResult struct
		var input = compiler.Result( InputT );

		// If the input isn't valid (nothing connected), Cast(1, 0.0f) 
		// will return the string "0.0".
		string t = input.Cast( 1, 0.0f );

		return new NodeResult( 3, $@"
        ({t} < 0.10 ? float3(0.29, 0.23, 0.16) :
         {t} < 0.20 ? float3(0.42, 0.35, 0.23) :
         {t} < 0.30 ? float3(0.48, 0.48, 0.48) :
         {t} < 0.40 ? float3(0.63, 0.63, 0.63) :
         {t} < 0.50 ? float3(0.55, 0.38, 0.22) :
         {t} < 0.60 ? float3(0.72, 0.45, 0.20) :
         {t} < 0.68 ? float3(0.54, 0.00, 0.00) :
         {t} < 0.72 ? float3(0.76, 0.07, 0.12) :
         {t} < 0.78 ? float3(0.35, 0.16, 0.51) :
         {t} < 0.82 ? float3(0.48, 0.25, 0.63) :
         {t} < 0.86 ? float3(0.17, 0.35, 0.63) :
         {t} < 0.90 ? float3(0.23, 0.48, 0.84) :
         {t} < 0.95 ? float3(0.75, 0.75, 0.75) :
         {t} < 0.98 ? float3(0.83, 0.69, 0.22) :
         {t} < 1.00 ? float3(0.00, 0.61, 0.47) :
         float3(0.00, 1.00, 1.00))" );
	};
}
