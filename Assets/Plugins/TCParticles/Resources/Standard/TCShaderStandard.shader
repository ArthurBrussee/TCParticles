Shader "TCParticles/Standard (lit)" {
    Properties {
        _MainTex("Albedo", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _MetallicGlossMap("Metallic", 2D) = "white" {}
        [Gamma] _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Glossiness("Smoothness", Range(0.0, 1.0)) = 0.5

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
        [HideInInspector] _FlipbookMode ("__flipbookmode", Float) = 0.0
        [HideInInspector] _LightingEnabled ("__lightingenabled", Float) = 1.0
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
        [HideInInspector] _DistortionStrengthScaled ("__distortionstrengthscaled", Float) = 0.0
    }

    SubShader {
        Tags { "RenderType"="Opaque" "IgnoreProjector"="True" "PreviewType"="Plane" "PerformanceChecks"="False" "TC_LIT"="True" }

        BlendOp [_BlendOp]
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull [_Cull]

        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Off

            CGPROGRAM
            #pragma target 4.6
			#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH
            #pragma multi_compile _ TC_CUSTOM_NORMAL_ORIENT

			#pragma multi_compile_instancing
			#pragma instancing_options procedural:TCDefaultProc
			
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON
            
			#pragma multi_compile_shadowcaster
            #pragma vertex vertParticleShadowCaster
            #pragma fragment fragParticleShadowCaster
            #include "TcStandardParticleShadow.cginc"
            ENDCG
        }

        CGPROGRAM
		#pragma target 4.6
		#pragma multi_compile TC_BILLBOARD TC_BILLBOARD_STRETCHED TC_MESH
        #pragma multi_compile _ TC_CUSTOM_NORMAL_ORIENT

		#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON
		#pragma shader_feature _METALLICGLOSSMAP
        #pragma shader_feature _NORMALMAP
        #pragma shader_feature _EMISSION
		
		#pragma multi_compile_instancing
		#pragma instancing_options procedural:TCDefaultProc
        #pragma surface surf Standard nolightmap nometa noforwardadd keepalpha noshadowmask nolppv
        #include "TcStandardParticles.cginc"
        ENDCG
    }

    CustomEditor "TcStandardParticlesShaderGUI"
}