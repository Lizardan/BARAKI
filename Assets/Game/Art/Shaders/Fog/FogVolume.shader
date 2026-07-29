Shader "Game/FogVolume"
{
    Properties
    {
        _DensityTex ("Density", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.02, 0.02, 0.03, 1)
        _ShadowColor ("Shadow Color", Color) = (0.01, 0.01, 0.015, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.82
        _DensityScale ("Density Scale", Range(0, 20)) = 1
        _FogAreaSize ("Fog Area Size", Float) = 280
        _Softness ("Softness", Range(0.5, 4)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        ZTest Always
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FogFullscreen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_DensityTex);
            SAMPLER(sampler_DensityTex);
            float4 _DensityTex_TexelSize;
            float4 _FogColor;
            float4 _ShadowColor;
            float _Opacity;
            float _DensityScale;
            float _FogAreaSize;
            float _Softness;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                return output;
            }

            float3 ReconstructWorld(float2 uv, float rawDepth)
            {
                #if UNITY_REVERSED_Z
                float depth = rawDepth;
                #else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            float3 GroundHitFromCameraRay(float2 uv)
            {
                float3 nearPos = ComputeWorldSpacePosition(uv, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP);
                #if UNITY_REVERSED_Z
                float farDepth = 0.0;
                #else
                float farDepth = 1.0;
                #endif
                float3 farPos = ComputeWorldSpacePosition(uv, farDepth, UNITY_MATRIX_I_VP);
                float3 dir = farPos - nearPos;
                if (abs(dir.y) < 1e-5)
                {
                    return float3(nearPos.x, 0.0, nearPos.z);
                }

                float t = max(-nearPos.y / dir.y, 0.0);
                return nearPos + dir * t;
            }

            // 5-tap blur softens texel grid of the density map.
            float SampleDensitySoft(float2 fogUv)
            {
                float2 texel = _DensityTex_TexelSize.xy * _Softness;
                float d = 0.0;
                d += SAMPLE_TEXTURE2D(_DensityTex, sampler_DensityTex, fogUv).r * 0.4;
                d += SAMPLE_TEXTURE2D(_DensityTex, sampler_DensityTex, fogUv + float2(texel.x, 0)).r * 0.15;
                d += SAMPLE_TEXTURE2D(_DensityTex, sampler_DensityTex, fogUv - float2(texel.x, 0)).r * 0.15;
                d += SAMPLE_TEXTURE2D(_DensityTex, sampler_DensityTex, fogUv + float2(0, texel.y)).r * 0.15;
                d += SAMPLE_TEXTURE2D(_DensityTex, sampler_DensityTex, fogUv - float2(0, texel.y)).r * 0.15;
                return d;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionCS.xy / _ScaledScreenParams.xy;

                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                bool isSky = rawDepth < 1e-5;
                #else
                bool isSky = rawDepth >= 0.99999;
                #endif

                float3 worldPos = isSky
                    ? GroundHitFromCameraRay(uv)
                    : ReconstructWorld(uv, rawDepth);

                float area = max(_FogAreaSize, 1.0);
                float2 fogUv = worldPos.xz / area + 0.5;
                float outside = any(fogUv < 0.0) || any(fogUv > 1.0) ? 1.0 : 0.0;
                fogUv = saturate(fogUv);

                float d = SampleDensitySoft(fogUv);
                d = max(d, outside);
                d = saturate(d * max(_DensityScale, 0.001));
                // Soft alpha curve — mid densities stay translucent instead of snapping opaque.
                float a = saturate(smoothstep(0.05, 0.95, d) * _Opacity);
                float3 col = lerp(_ShadowColor.rgb, _FogColor.rgb, 0.35);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
