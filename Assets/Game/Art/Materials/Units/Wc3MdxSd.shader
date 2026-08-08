Shader "Game/Units/Wc3MdxSd"
{
    Properties
    {
        [MainTexture] _BaseMap("Diffuse", 2D) = "white" {}
        _VertexColor("Vertex Color", Color) = (1, 1, 1, 1)
        _GeosetColor("Geoset Color", Color) = (1, 1, 1, 1)
        _LayerAlpha("Layer Alpha", Float) = 1
        _Unshaded("Unshaded", Float) = 0
        _UvTranslate("UV Translate", Vector) = (0, 0, 0, 0)
        _UvRotate("UV Rotate (quat zw)", Vector) = (0, 1, 0, 0)
        _UvScale("UV Scale", Float) = 1
        _LightDirection("Light Direction", Vector) = (0, 1, 0, 0)
        [HideInInspector] _BoneMap("Bone Map", 2D) = "black" {}
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _AlphaDiscard("Alpha Discard", Float) = 0
        [HideInInspector] _AlphaThreshold("Alpha Threshold", Float) = 0
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

            // Per-material render state, driven by properties set in MdxMaterialBuilder
            // (mirrors filtermode.ts of the reference viewer).
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BoneMap);
            SAMPLER(sampler_BoneMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BoneMap_TexelSize;
                half4 _VertexColor;
                half4 _GeosetColor;
                half _LayerAlpha;
                half _Unshaded;
                float4 _UvTranslate;
                float4 _UvRotate;
                float _UvScale;
                float4 _LightDirection;
                half _AlphaDiscard;
                half _AlphaThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                // MdxMeshBuilder layout:
                //   uv2 = bone indices 0-3, uv3 = bone indices 4-7
                //   uv4 = weights 0-3,   uv5 = weights 4-7
                //   uv6.x = bone count (0 = identity), uv6.y = skin type (1 = Skin buffer)
                float4 bones0 : TEXCOORD2;
                float4 bones1 : TEXCOORD3;
                float4 weights0 : TEXCOORD4;
                float4 weights1 : TEXCOORD5;
                float4 info : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float4 uvTransRot : TEXCOORD3;
                float uvScale : TEXCOORD4;
                float3 lightDirWS : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Port of bonetexture.glsl fetchMatrix(). The bone map is an RGBA float
            // texture, width = boneCount * 4 texels, height = 1; each 4x4 matrix is
            // stored column-major across 4 consecutive texels (one texel = one column).
            void FetchMatrix(float column, float row, out float4 col0, out float4 col1, out float4 col2, out float4 col3)
            {
                float2 texel = _BoneMap_TexelSize.xy;
                float x = (column * 4.0 + 0.5) * texel.x;
                float y = (row + 0.5) * texel.y;

                col0 = SAMPLE_TEXTURE2D_LOD(_BoneMap, sampler_BoneMap, float2(x, y), 0);
                col1 = SAMPLE_TEXTURE2D_LOD(_BoneMap, sampler_BoneMap, float2(x + texel.x, y), 0);
                col2 = SAMPLE_TEXTURE2D_LOD(_BoneMap, sampler_BoneMap, float2(x + texel.x * 2.0, y), 0);
                col3 = SAMPLE_TEXTURE2D_LOD(_BoneMap, sampler_BoneMap, float2(x + texel.x * 3.0, y), 0);
            }

            void AddBone(inout float4 c0, inout float4 c1, inout float4 c2, inout float4 c3, float index)
            {
                float4 f0;
                float4 f1;
                float4 f2;
                float4 f3;
                FetchMatrix(index, 0, f0, f1, f2, f3);

                c0 += f0;
                c1 += f1;
                c2 += f2;
                c3 += f3;
            }

            // Column-major matrix * vector, written out explicitly so we never depend
            // on Unity's float4x4 constructor/row-major convention.
            float3 TransformAffine(float4 c0, float4 c1, float4 c2, float4 c3, float3 v)
            {
                return c0.xyz * v.x + c1.xyz * v.y + c2.xyz * v.z + c3.xyz;
            }

            float3 TransformRotation(float4 c0, float4 c1, float4 c2, float3 v)
            {
                return c0.xyz * v.x + c1.xyz * v.y + c2.xyz * v.z;
            }

            // Port of transforms.glsl getVertexGroupMatrix(). Uniform weight 1/boneCount.
            void GetVertexGroupMatrix(float boneCount, float4 b0, float4 b1,
                                      out float4 c0, out float4 c1, out float4 c2, out float4 c3)
            {
                c0 = 0;
                c1 = 0;
                c2 = 0;
                c3 = 0;

                if (b0.x > 0.5) AddBone(c0, c1, c2, c3, b0.x - 1.0);
                if (b0.y > 0.5) AddBone(c0, c1, c2, c3, b0.y - 1.0);
                if (b0.z > 0.5) AddBone(c0, c1, c2, c3, b0.z - 1.0);
                if (b0.w > 0.5) AddBone(c0, c1, c2, c3, b0.w - 1.0);

                if (boneCount > 4.5)
                {
                    if (b1.x > 0.5) AddBone(c0, c1, c2, c3, b1.x - 1.0);
                    if (b1.y > 0.5) AddBone(c0, c1, c2, c3, b1.y - 1.0);
                    if (b1.z > 0.5) AddBone(c0, c1, c2, c3, b1.z - 1.0);
                    if (b1.w > 0.5) AddBone(c0, c1, c2, c3, b1.w - 1.0);
                }

                if (boneCount > 0.5)
                {
                    c0 /= boneCount;
                    c1 /= boneCount;
                    c2 /= boneCount;
                    c3 /= boneCount;
                }
            }

            // Port of transforms.glsl transformSkin(). Weighted blend of up to 4 bones.
            void GetSkinMatrix(float4 b0, float4 w0,
                               out float4 c0, out float4 c1, out float4 c2, out float4 c3)
            {
                float4 f0;
                float4 f1;
                float4 f2;
                float4 f3;

                FetchMatrix(b0.x, 0, f0, f1, f2, f3);
                c0 = f0 * w0.x; c1 = f1 * w0.x; c2 = f2 * w0.x; c3 = f3 * w0.x;

                FetchMatrix(b0.y, 0, f0, f1, f2, f3);
                c0 += f0 * w0.y; c1 += f1 * w0.y; c2 += f2 * w0.y; c3 += f3 * w0.y;

                FetchMatrix(b0.z, 0, f0, f1, f2, f3);
                c0 += f0 * w0.z; c1 += f1 * w0.z; c2 += f2 * w0.z; c3 += f3 * w0.z;

                FetchMatrix(b0.w, 0, f0, f1, f2, f3);
                c0 += f0 * w0.w; c1 += f1 * w0.w; c2 += f2 * w0.w; c3 += f3 * w0.w;
            }

            // Port of quattransform.glsl. q = (sin, cos) of the 2D rotation.
            float2 QuatTransform(float2 q, float2 v)
            {
                float2 uv = float2(-q.x * v.y, q.x * v.x);
                float2 uuv = float2(-q.x * uv.y, q.x * uv.x);

                return v + 2.0 * (uv * q.y + uuv);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 position = input.positionOS.xyz;
                float3 normal = input.normalOS;
                float boneCount = input.info.x;
                float skinType = input.info.y;

                if (boneCount > 0.5)
                {
                    float4 c0;
                    float4 c1;
                    float4 c2;
                    float4 c3;

                    if (skinType > 0.5)
                    {
                        GetSkinMatrix(input.bones0, input.weights0, c0, c1, c2, c3);
                    }
                    else
                    {
                        GetVertexGroupMatrix(boneCount, input.bones0, input.bones1, c0, c1, c2, c3);
                    }

                    position = TransformAffine(c0, c1, c2, c3, position);
                    normal = TransformRotation(c0, c1, c2, normal);

                    if (skinType < 0.5)
                    {
                        normal = normalize(normal);
                    }
                }

                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(normal);
                // The reference applies .bgra to the geoset color (WC3 stores it BGR).
                output.color = _VertexColor * _GeosetColor.bgra * float4(1.0, 1.0, 1.0, _LayerAlpha);
                output.uvTransRot = float4(_UvTranslate.xy, _UvRotate.xy);
                output.uvScale = _UvScale;
                output.lightDirWS = normalize(_LightDirection.xyz);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;

                // Translation animation.
                uv += input.uvTransRot.xy;

                // Rotation animation (around the texel center).
                uv = QuatTransform(input.uvTransRot.zw, uv - 0.5) + 0.5;

                // Scale animation.
                uv = input.uvScale * (uv - 0.5) + 0.5;

                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 color = texel * input.color;

                // 1-bit alpha (Transparent) / "close to 0 alpha" (Modulate) discards.
                if (_AlphaDiscard > 0.5 && color.a < _AlphaThreshold)
                {
                    discard;
                }

                // WC3 lambert: clamp(dot + 0.7).
                if (_Unshaded < 0.5)
                {
                    float lambert = saturate(dot(normalize(input.normalWS), input.lightDirWS));
                    lambert = saturate(lambert + 0.7);
                    color.rgb *= lambert;
                }

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
