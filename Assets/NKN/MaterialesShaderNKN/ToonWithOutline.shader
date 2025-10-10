Shader "Custom/ToonWithOutline"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness", Float) = 0.02
        _MainTex ("Base Texture", 2D) = "white" {}

        [Toggle(_BRIGHTNESS_ON)] _BrightnessEnabled ("Brightness", Float) = 0

        _ShadowLevel ("Shadow Intensity", Range(0,1)) = 0.3
        _BounceLevel ("Bounce Light Intensity", Range(0,1)) = 0.6
        _DirectLevel ("Direct Light Intensity", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        // -------- PASS 1: OUTLINE --------
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            float _OutlineThickness;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 norm = normalize(IN.normalOS);
                float3 offset = norm * _OutlineThickness;
                float4 pos = IN.positionOS + float4(offset, 0);
                OUT.positionHCS = TransformObjectToHClip(pos.xyz);
                return OUT;
            }

            float4 _OutlineColor;

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // -------- PASS 2: TOON BASE COLOR --------
        Pass
        {
            Name "BASE"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _BRIGHTNESS_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : NORMAL;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                return OUT;
            }

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;

            float _ShadowLevel;
            float _BounceLevel;
            float _DirectLevel;

            half4 frag(Varyings IN) : SV_Target
            {
                float3 lightDir = normalize(_MainLightPosition.xyz);
                float NdotL = dot(IN.normalWS, lightDir);
                float intensity = saturate(NdotL);

                float step = 0.0;

                #ifdef _BRIGHTNESS_ON
                    if (intensity > 0.75)
                        step = _DirectLevel;
                    else if (intensity > 0.25)
                        step = _BounceLevel;
                    else
                        step = _ShadowLevel;
                #else
                    step = intensity > 0.5 ? _DirectLevel : _ShadowLevel;
                #endif

                float2 uv = TRANSFORM_TEX(IN.uv, _MainTex);
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                return tex * _Color * step;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
