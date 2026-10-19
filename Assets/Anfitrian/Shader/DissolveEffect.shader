Shader "Custom/DissolveEffect_Cracks"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _Direction ("Direction", Float) = 1

        // Ajuste conforme os limites locais do mesh.
        _MinHeight ("Min Height", Float) = -1
        _MaxHeight ("Max Height", Float) = 1

        _EdgeWidth ("Edge Width", Range(0.001,0.3)) = 0.08
        _EdgeColor ("Edge Color", Color) = (0,0.9,1,1)
        _EdgeIntensity ("Edge Intensity", Float) = 2

        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Dissolve"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _DissolveAmount;
                float _Direction;
                float _MinHeight;
                float _MaxHeight;
                float _EdgeWidth;
                float4 _EdgeColor;
                float _EdgeIntensity;
                float _NoiseScale;
                float _NoiseStrength;
            CBUFFER_END

            float hash(float2 p)
            {
                return frac(
                    sin(dot(p, float2(127.1, 311.7)))
                    * 43758.5453
                );
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(
                    lerp(a, b, f.x),
                    lerp(c, d, f.x),
                    f.y
                );
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    ) * _BaseColor;

                // Evita divisão por zero.
                float heightRange = max(
                    _MaxHeight - _MinHeight,
                    0.0001
                );

                // Normaliza a altura do mesh entre 0 e 1.
                float height = saturate(
                    (input.positionOS.y - _MinHeight)
                    / heightRange
                );

                // Direção positiva: baixo para cima.
                // Direção negativa: cima para baixo.
                height = (_Direction < 0)
                    ? 1.0 - height
                    : height;

                float n = noise(input.uv * _NoiseScale);
                float noiseOffset = (n - 0.5) * _NoiseStrength;

                float dissolveValue = height + noiseOffset;

                // Faz o efeito cobrir toda a faixa, inclusive
                // no início e no final da animação.
                float threshold = lerp(
                    -_NoiseStrength * 0.5,
                    1.0 + _NoiseStrength * 0.5 + 0.001,
                    saturate(_DissolveAmount)
                );

                float dissolve = dissolveValue - threshold;

                // Descarta os pixels já dissolvidos.
                clip(dissolve);

                // Borda luminosa no limite do dissolve.
                float edge = 1.0 - smoothstep(
                    0.0,
                    max(_EdgeWidth, 0.001),
                    dissolve
                );

                col.rgb = lerp(
                    col.rgb,
                    _EdgeColor.rgb,
                    edge
                );

                // Intensidade adicional para a borda.
                col.rgb +=
                    _EdgeColor.rgb * edge * _EdgeIntensity;

                return col;
            }
            ENDHLSL
        }
    }
}