Shader "Custom/DissolveEffect"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _Direction ("Direction", Float) = 1
        _EdgeWidth ("Edge Width", Range(0,0.2)) = 0.05
        _EdgeColor ("Edge Color", Color) = (0,0.8,1,1)
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
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                float height = input.positionOS.y * _Direction;
                float dissolve = height - _DissolveAmount;

                // Alpha clip style
                clip(dissolve);

                // Edge
                float edge = smoothstep(0, _EdgeWidth, dissolve);
                col.rgb = lerp(_EdgeColor.rgb, col.rgb, edge);
                col.a *= edge;

                return col;
            }
            ENDHLSL
        }
    }
}
