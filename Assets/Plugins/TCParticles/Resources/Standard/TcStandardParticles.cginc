// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)
#ifndef TC_STANDARD_PARTICLES_INCLUDED
#define TC_STANDARD_PARTICLES_INCLUDED

#include "UnityPBSLighting.cginc"
#include "../TCFramework.cginc"

sampler2D _MainTex;

half4 _Color;
sampler2D _BumpMap;
half _BumpScale;
sampler2D _EmissionMap;
half3 _EmissionColor;
sampler2D _MetallicGlossMap;
half _Metallic;
half _Glossiness;
half _Cutoff;

#if defined (_COLORADDSUBDIFF_ON)
half4 _ColorAddSubDiff;
#endif

// Vertex shader input
struct appdata_particles
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 color : COLOR;
    float4 texcoord : TEXCOORD0;
    float4 texcoord1 : TEXCOORD1;

    #if defined(_NORMALMAP)
		float4 tangent : TANGENT;
    #endif

	UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Surface shader input
struct Input
{
    float4 color : COLOR;
    float2 uv_MainTex;
};

// Non-surface shader v2f structure
struct VertexOutput
{
    float4 vertex : SV_POSITION;
    float4 color : COLOR;

    UNITY_FOG_COORDS(0)

    float4 texcoord : TEXCOORD1;
};

//TODO: Flipbook blending not supported yet!
float4 readTexture(sampler2D tex, Input IN)
{
    float4 color = tex2D (tex, IN.uv_MainTex.xy);
    return color;
}

float4 readTexture(sampler2D tex, VertexOutput IN)
{
    float4 color = tex2D (tex, IN.texcoord.xy);
    return color;
}

#if defined(_COLORCOLOR_ON)
half3 RGBtoHSV(half3 arg1)
{
    half4 K = half4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    half4 P = lerp(half4(arg1.bg, K.wz), half4(arg1.gb, K.xy), step(arg1.b, arg1.g));
    half4 Q = lerp(half4(P.xyw, arg1.r), half4(arg1.r, P.yzx), step(P.x, arg1.r));
    half D = Q.x - min(Q.w, Q.y);
    half E = 1e-10;
    return half3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), Q.x);
}

half3 HSVtoRGB(half3 arg1)
{
    half4 K = half4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    half3 P = abs(frac(arg1.xxx + K.xyz) * 6.0 - K.www);
    return arg1.z * lerp(K.xxx, saturate(P - K.xxx), arg1.y);
}
#endif


// Color blending fragment function
#if defined(_COLOROVERLAY_ON)
#define fragColorMode(i) \
    albedo.rgb = lerp(1 - 2 * (1 - albedo.rgb) * (1 - i.color.rgb), 2 * albedo.rgb * i.color.rgb, step(albedo.rgb, 0.5)); \
    albedo.a *= i.color.a;
#elif defined(_COLORCOLOR_ON)
#define fragColorMode(i) \
    half3 aHSL = RGBtoHSV(albedo.rgb); \
    half3 bHSL = RGBtoHSV(i.color.rgb); \
    half3 rHSL = float3(bHSL.x, bHSL.y, aHSL.z); \
    albedo = float4(HSVtoRGB(rHSL), albedo.a * i.color.a);
#elif defined(_COLORADDSUBDIFF_ON)
#define fragColorMode(i) \
    albedo.rgb = albedo.rgb + i.color.rgb * _ColorAddSubDiff.x; \
    albedo.rgb = lerp(albedo.rgb, abs(albedo.rgb), _ColorAddSubDiff.y); \
    albedo.a *= i.color.a;
#else
#define fragColorMode(i) \
    albedo *= i.color;
#endif

// Pre-multiplied alpha helper
#if defined(_ALPHAPREMULTIPLY_ON)
#define ALBEDO_MUL albedo
#else
#define ALBEDO_MUL albedo.a
#endif

void surf (Input IN, inout SurfaceOutputStandard o)
{
    half4 albedo = readTexture (_MainTex, IN);
	albedo *= _Color;
	   
	fragColorMode(IN);

    #if defined(_METALLICGLOSSMAP)
		float2 metallicGloss = readTexture (_MetallicGlossMap, IN).ra * float2(1.0, _Glossiness);
    #else
		float2 metallicGloss = float2(_Metallic, _Glossiness);
    #endif

    #if defined(_NORMALMAP)
		float3 normal = normalize (UnpackScaleNormal (readTexture (_BumpMap, IN), _BumpScale));
    #else
		float3 normal = float3(0,0,1);
    #endif

    #if defined(_EMISSION)
		half3 emission = readTexture (_EmissionMap, IN).rgb;
    #else
		half3 emission = 0;
    #endif

    o.Albedo = albedo.rgb;

    #if defined(_NORMALMAP)
		o.Normal = normal;
    #endif
    o.Emission = emission * _EmissionColor;
    o.Metallic = metallicGloss.r;
    o.Smoothness = metallicGloss.g;
    					
    // TODO: Remove when shipping TC Particles!!
    // o.Smoothness = IN.color.a;

    #if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON) || defined(_ALPHAOVERLAY_ON)
		o.Alpha = albedo.a;
    #else
		o.Alpha = 1;
    #endif

    #if defined(_ALPHAMODULATE_ON)
		o.Albedo = lerp(half3(1.0, 1.0, 1.0), albedo.rgb, albedo.a);
    #endif

    #if defined(_ALPHATEST_ON)
		clip (albedo.a - _Cutoff + 0.0001);
    #endif
}

#endif // UNITY_STANDARD_PARTICLES_INCLUDED
