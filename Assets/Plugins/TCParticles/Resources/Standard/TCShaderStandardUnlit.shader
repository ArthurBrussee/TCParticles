Shader "TCParticles/Standard (unlit)" {
    Properties {
        _MainTex("Albedo", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DistortionStrength("Strength", Float) = 1.0
        _DistortionBlend("Blend", Range(0.0, 1.0)) = 0.5

        _SoftParticlesNearFadeDistance("Soft Particles Near Fade", Float) = 0.0
        _SoftParticlesFarFadeDistance("Soft Particles Far Fade", Float) = 1.0
        _CameraNearFadeDistance("Camera Near Fade", Float) = 1.0
        _CameraFarFadeDistance("Camera Far Fade", Float) = 2.0

        // Hidden properties
        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _ColorMode ("__colormode", Float) = 0.0
        [HideInInspector] _FlipbookMode ("__flipbookmode", Float) = 0.0
        [HideInInspector] _LightingEnabled ("__lightingenabled", Float) = 0.0
        [HideInInspector] _DistortionEnabled ("__distortionenabled", Float) = 0.0
        [HideInInspector] _EmissionEnabled ("__emissionenabled", Float) = 0.0
        [HideInInspector] _BlendOp ("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0
        [HideInInspector] _Cull ("__cull", Float) = 2.0
        [HideInInspector] _SoftParticlesEnabled ("__softparticlesenabled", Float) = 0.0
        [HideInInspector] _CameraFadingEnabled ("__camerafadingenabled", Float) = 0.0
        [HideInInspector] _SoftParticleFadeParams ("__softparticlefadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _CameraFadeParams ("__camerafadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _ColorAddSubDiff ("__coloraddsubdiff", Vector) = (0,0,0,0)
        [HideInInspector] _DistortionStrengthScaled ("__distortionstrengthscaled", Float) = 0.0
    }

    Category {
        SubShader {
            Tags { "RenderType"="Opaque" "IgnoreProjector"="True" "PreviewType"="Plane" "PerformanceChecks"="False" "TC_LIT"="False" }

            BlendOp [_BlendOp]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            ColorMask RGB

            Pass {
                Name "ShadowCaster"
                Tags { "LightMode" = "ShadowCaster" }

                BlendOp Add
                Blend One Zero
                ZWrite On
                Cull Off

                CGPROGRAM
				#pragma target 4.5
				#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH
                #pragma multi_compile _ TC_CUSTOM_NORMAL_ORIENT

				#pragma multi_compile_instancing
				#pragma instancing_options procedural:TCDefaultProc

                #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON
                #pragma shader_feature _ _COLOROVERLAY_ON _COLORCOLOR_ON _COLORADDSUBDIFF_ON
                
                #pragma multi_compile_shadowcaster

                #pragma vertex vertParticleShadowCaster
                #pragma fragment fragParticleShadowCaster

                #include "TcStandardParticleShadow.cginc"
                ENDCG
            }

            Pass
            {
                Tags { "LightMode"="ForwardBase" }

                CGPROGRAM
				#pragma target 4.5
				#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH
                #pragma multi_compile _ TC_CUSTOM_NORMAL_ORIENT

				#pragma multi_compile_instancing
				#pragma instancing_options procedural:TCDefaultProc
    
                #pragma multi_compile_fog

                #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON
                #pragma shader_feature _ _COLOROVERLAY_ON _COLORCOLOR_ON _COLORADDSUBDIFF_ON
                #pragma shader_feature _EMISSION

                #pragma vertex vertParticleUnlit
                #pragma fragment fragParticleUnlit

                #include "TcStandardParticles.cginc"
				float4      _MainTex_ST;

				VertexOutput vertParticleUnlit (appdata_particles v) {
					VertexOutput o;
					TC_DO_PARTICLE(v);

					o.vertex = UnityObjectToClipPos(v.vertex);
					o.texcoord = float4(TRANSFORM_TEX(v.texcoord, _MainTex), 0, 0);
					o.color = v.color;

					UNITY_TRANSFER_FOG(o, o.vertex);
					return o;
				}


				half4 fragParticleUnlit (VertexOutput IN) : SV_Target {
					half4 albedo = readTexture (_MainTex, IN) * _Color;
					albedo *= _Color;
					


					fragColorMode(IN);

					#if defined(_EMISSION)
						half3 emission = readTexture (_EmissionMap, IN).rgb;
					#else
						half3 emission = 0;
					#endif

					half4 result = albedo;

					#if defined(_ALPHAMODULATE_ON)
						result.rgb = lerp(half3(1.0, 1.0, 1.0), albedo.rgb, albedo.a);
					#endif

					result.rgb += emission * _EmissionColor;

					#if !defined(_ALPHABLEND_ON) && !defined(_ALPHAPREMULTIPLY_ON) && !defined(_ALPHAOVERLAY_ON)
						result.a = 1;
					#endif

					#if defined(_ALPHATEST_ON)
						clip (albedo.a - _Cutoff + 0.0001);
					#endif

					UNITY_APPLY_FOG_COLOR(IN.fogCoord, result, float4(0,0,0,0));					
					return result;
				}

                ENDCG
            }
        }
    }

    CustomEditor "TcStandardParticlesShaderGUI"
}
