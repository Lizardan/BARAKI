Shader "Game/Units/TeamColorDiffuse"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Team Color", Color) = (1, 1, 1, 1)
        _Cutoff("Yellow Threshold", Range(0, 1)) = 0.22
        _YellowPower("Yellow Softness", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Cutoff;
                half _YellowPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half3 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // Gold / yellow cloak pigment in WC3-style atlases (e.g. Arthas cloth).
                half yellowness = saturate((albedo.r + albedo.g) * 0.5h - albedo.b - _Cutoff);
                yellowness = pow(yellowness, 1.0h / _YellowPower);

                // WC3 TeamColor underlay: transparent texels reveal team color.
                half underlay = saturate(1.0h - albedo.a);

                half mask = saturate(yellowness * 3.0h + underlay);
                half lum = max(dot(albedo.rgb, half3(0.299h, 0.587h, 0.114h)), 0.12h);
                half3 team = _BaseColor.rgb * lum;
                return lerp(albedo.rgb, team, mask);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
