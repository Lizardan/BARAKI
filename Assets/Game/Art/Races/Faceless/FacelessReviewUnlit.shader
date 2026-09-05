Shader "Game/Faceless/ReviewUnlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _TeamColor("Team Color", Color) = (0.2, 0.45, 0.95, 1)
        _UseTeam("Use Team Color", Float) = 0
        _UseBlend("Use Blend", Float) = 0
        _AlphaClip("Alpha Clip", Range(0, 1)) = 0
        _AtlasClip("Atlas Empty Clip", Range(0, 1)) = 0
        _ChainClip("Chain Black Clip", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        _ZWrite("ZWrite", Float) = 1
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
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

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
                half4 _TeamColor;
                half _UseTeam;
                half _UseBlend;
                half _AlphaClip;
                half _AtlasClip;
                half _ChainClip;
                half _Cull;
                half _SrcBlend;
                half _DstBlend;
                half _ZWrite;
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

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                if (_AlphaClip > 0.001h)
                {
                    clip(albedo.a - _AlphaClip);
                }

                if (_AtlasClip > 0.001h)
                {
                    half3 magosEmpty = half3(0.51h, 0.576h, 0.698h);
                    half magosDelta = distance(albedo.rgb, magosEmpty);
                    if (magosDelta < _AtlasClip)
                    {
                        albedo.rgb = half3(0.10h, 0.27h, 0.25h);
                    }
                }

                if (_ChainClip > 0.001h)
                {
                    half chainLuma = dot(albedo.rgb, half3(0.299h, 0.587h, 0.114h));
                    clip(chainLuma - _ChainClip);
                }

                half3 rgb = albedo.rgb;
                if (_UseTeam > 0.5h)
                {
                    rgb = lerp(rgb, _TeamColor.rgb, saturate(1.0h - albedo.a));
                }

                half alpha = _UseBlend > 0.5h ? albedo.a : 1.0h;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
