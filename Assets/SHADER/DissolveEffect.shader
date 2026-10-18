Shader "Custom/DissolveEffect_Cracks"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _Direction ("Direction", Float) = 1
        _EdgeWidth ("Edge Width", Range(0,0.3)) = 0.08
        _EdgeColor ("Edge Color", Color) = (0, 0.9, 1, 1)
        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
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
            float4 _BaseColor;
            float _DissolveAmount;
            float _Direction;
            float _EdgeWidth;
            float4 _EdgeColor;
            float _NoiseScale;
            float _NoiseStrength;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Usa a textura original do personagem
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                float height = input.positionOS.y * _Direction;
                float n = noise(input.uv * _NoiseScale);
                float dissolveValue = height + (n - 0.5) * _NoiseStrength;
                float dissolve = dissolveValue - _DissolveAmount;

                // Corta o que ainda não apareceu
                clip(dissolve);

                // Borda de energia só na transição
                float edge = 1.0 - smoothstep(0.0, _EdgeWidth, dissolve);
                col.rgb = lerp(col.rgb, _EdgeColor.rgb, edge * 0.85);

                return col;
            }
            ENDHLSL
        }
    }
}