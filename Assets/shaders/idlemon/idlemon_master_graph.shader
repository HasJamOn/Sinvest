
HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth();
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 1
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 0
	#endif
	
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define S_UV2 1
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
	float4 vColor : COLOR0 < Semantic( Color ); >;
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float3 vPositionOs : TEXCOORD14;
	float3 vNormalOs : TEXCOORD15;
	float4 vTangentUOs_flTangentVSign : TANGENT	< Semantic( TangentU_SignV ); >;
	float4 vColor : COLOR0;
	float4 vTintColor : COLOR1;
	#if ( PROGRAM == VFX_PROGRAM_PS )
		bool vFrontFacing : SV_IsFrontFace;
	#endif
};

VS
{
	#include "common/vertex.hlsl"
	
	float g_flSubtraction < Attribute( "Subtraction" ); Default1( 0 ); >;
	float g_flShaderTime < Attribute( "ShaderTime" ); Default1( 0 ); >;
	
	PixelInput MainVs( VertexInput v )
	{
		
		PixelInput i = ProcessVertex( v );
		i.vPositionOs = v.vPositionOs.xyz;
		i.vColor = v.vColor;
		
		ExtraShaderData_t extraShaderData = GetExtraPerInstanceShaderData( v.nInstanceTransformID );
		i.vTintColor = extraShaderData.vTint;
		
		VS_DecodeObjectSpaceNormalAndTangent( v, i.vNormalOs, i.vTangentUOs_flTangentVSign );
		
		float l_0 = g_flSubtraction;
		float l_1 = g_flShaderTime;
		float l_2 = l_0 * l_1;
		float l_3 = sin( l_2 );
		float3 l_4 = i.vPositionOs;
		float3 l_5 = float3( l_3, l_3, l_3 ) * l_4;
		float3 l_6 = l_5 * float3( 0.2, 0.2, 0.2 );
		i.vPositionWs.xyz += l_6;
		i.vPositionPs.xyzw = Position3WsToPs( i.vPositionWs.xyz );
		return FinalizeVertex( i );
		
	}
}

PS
{
	#include "common/pixel.hlsl"
	RenderState( CullMode, F_RENDER_BACKFACES ? NONE : DEFAULT );
		
	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;
	Texture2D g_tArtwork < Attribute( "Artwork" ); >;
	float g_flShaderTime < Attribute( "ShaderTime" ); Default1( 0 ); >;
	float g_flAddition < Attribute( "Addition" ); Default1( 0 ); >;
	float g_flMultiplier < Attribute( "Multiplier" ); Default1( 0 ); >;
	float g_flDivision < Attribute( "Division" ); Default1( 0 ); >;
	
	float4 MainPs( PixelInput i ) : SV_Target0
	{
		
		Material m = Material::Init( i );
		m.Albedo = float3( 1, 1, 1 );
		m.Normal = float3( 0, 0, 1 );
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		m.TintMask = 1;
		m.Opacity = 1;
		m.Emission = float3( 0, 0, 0 );
		m.Transmission = 0;
		
		float4 l_0 = Tex2DS( g_tArtwork, g_sSampler0, i.vTextureCoords.xy );
		float l_1 = g_flShaderTime;
		float3 l_2 = 
		        (fmod(l_1 * 0.066, 1.0) < 0.10 ? float3(0.29, 0.23, 0.16) : // Tier 1
		         fmod(l_1 * 0.066, 1.0) < 0.20 ? float3(0.42, 0.35, 0.23) : 
		         fmod(l_1 * 0.066, 1.0) < 0.30 ? float3(0.48, 0.48, 0.48) : // Tier 2
		         fmod(l_1 * 0.066, 1.0) < 0.40 ? float3(0.63, 0.63, 0.63) : 
		         fmod(l_1 * 0.066, 1.0) < 0.50 ? float3(0.55, 0.38, 0.22) : // Tier 3
		         fmod(l_1 * 0.066, 1.0) < 0.60 ? float3(0.72, 0.45, 0.20) : 
		         fmod(l_1 * 0.066, 1.0) < 0.68 ? float3(0.54, 0.00, 0.00) : // Tier 4
		         fmod(l_1 * 0.066, 1.0) < 0.72 ? float3(0.76, 0.07, 0.12) : 
		         fmod(l_1 * 0.066, 1.0) < 0.78 ? float3(0.35, 0.16, 0.51) : // Tier 5
		         fmod(l_1 * 0.066, 1.0) < 0.82 ? float3(0.48, 0.25, 0.63) : 
		         fmod(l_1 * 0.066, 1.0) < 0.86 ? float3(0.17, 0.35, 0.63) : // Tier 6
		         fmod(l_1 * 0.066, 1.0) < 0.90 ? float3(0.23, 0.48, 0.84) : 
		         fmod(l_1 * 0.066, 1.0) < 0.95 ? float3(0.75, 0.75, 0.75) : // Tier 7
		         fmod(l_1 * 0.066, 1.0) < 0.98 ? float3(0.83, 0.69, 0.22) : // Tier 8
		         fmod(l_1 * 0.066, 1.0) < 1.00 ? float3(0.00, 0.61, 0.47) : // Tier 9
		         float3(0.00, 1.00, 1.00));
		float l_3 = g_flShaderTime;
		float l_4 = l_3 * 2;
		float l_5 = sin( l_4 );
		float l_6 = saturate( ( l_5 - -1 ) / ( 1 - -1 ) ) * ( 1 - 0 ) + 0;
		float2 l_7 = TileAndOffsetUv( i.vTextureCoords.xy, float2( 1, 1 ), float2( l_1, l_1 ) );
		float l_8 = VoronoiNoise( l_7, 3.1415925, 10 );
		float l_9 = step( l_6, l_8 );
		float4 l_10 = saturate( lerp( l_0, l_0*float4( l_2, 0 ), l_9 ) );
		float3 l_11 = float3( 0.2126, 0.7152, 0.0722 );
		float l_12 = dot( l_0, float4( l_11, 0 ) );
		float l_13 = pow( l_12, 2 );
		float l_14 = g_flAddition;
		float l_15 = sqrt( l_14 );
		float l_16 = saturate( ( l_15 - 0 ) / ( 1000 - 0 ) ) * ( 150 - 1 ) + 1;
		float l_17 = l_13 * l_16;
		float l_18 = g_flMultiplier;
		float l_19 = g_flShaderTime;
		float l_20 = l_18 * l_19;
		float l_21 = sin( l_20 );
		float l_22 = lerp( 0.2, 1, l_21 );
		float l_23 = l_17 * l_22;
		float l_24 = g_flDivision;
		float l_25 = l_24 - 1;
		float l_26 = l_25 * 0.1;
		float l_27 = Simplex2D( i.vTextureCoords.xy );
		float l_28 = l_27 + 1.2;
		float l_29 = step( l_26, l_28 );
		
		m.Albedo = l_10.xyz;
		m.Emission = float3( l_23, l_23, l_23 );
		m.Opacity = l_29;
		m.Roughness = 1;
		m.Metalness = 0;
		m.AmbientOcclusion = 1;
		
		
		m.AmbientOcclusion = saturate( m.AmbientOcclusion );
		m.Roughness = saturate( m.Roughness );
		m.Metalness = saturate( m.Metalness );
		m.Opacity = saturate( m.Opacity );
		
		// Result node takes normal as tangent space, convert it to world space now
		m.Normal = TransformNormal( m.Normal, i.vNormalWs, i.vTangentUWs, i.vTangentVWs );
		
		// for some toolvis shit
		m.WorldTangentU = i.vTangentUWs;
		m.WorldTangentV = i.vTangentVWs;
		m.TextureCoords = i.vTextureCoords.xy;
				
		return ShadingModelStandard::Shade( m );
	}
}
