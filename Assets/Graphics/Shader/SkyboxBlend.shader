Shader "Custom/BlendedCubemapSkybox"
{
    Properties
    {
        _CubemapA ("Cubemap A", Cube) = "" {}
        _CubemapB ("Cubemap B", Cube) = "" {}
        _Blend ("Blend", Range(0, 1)) = 0

        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Background"
            "RenderType"="Background"
            "PreviewType"="Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURECUBE(_CubemapA);
            SAMPLER(sampler_CubemapA);

            TEXTURECUBE(_CubemapB);
            SAMPLER(sampler_CubemapB);

            CBUFFER_START(UnityPerMaterial)
                float _Blend;
                float _Exposure;
                float _Rotation;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS);

                output.positionCS = TransformWorldToHClip(positionWS);

                output.direction =
                    normalize(positionWS - GetCameraPositionWS());

                return output;
            }

            float3 RotateY(float3 dir, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);

                return float3(
                    dir.x * c - dir.z * s,
                    dir.y,
                    dir.x * s + dir.z * c
                );
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.direction);

                float angle = radians(_Rotation);
                dir = RotateY(dir, angle);

                half3 colorA =
                    SAMPLE_TEXTURECUBE(
                        _CubemapA,
                        sampler_CubemapA,
                        dir
                    ).rgb;

                half3 colorB =
                    SAMPLE_TEXTURECUBE(
                        _CubemapB,
                        sampler_CubemapB,
                        dir
                    ).rgb;

                half3 color = lerp(
                    colorA,
                    colorB,
                    _Blend
                );

                color *= _Exposure;

                return half4(color, 1);
            }

            ENDHLSL
        }
    }
}
