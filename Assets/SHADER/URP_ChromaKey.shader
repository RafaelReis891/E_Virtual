Shader "Custom/ChromaKey"
{
    Properties
    {
        _MainTex ("Video", 2D) = "white" {}
        _KeyColor ("Key Color", Color) = (0,1,0,1)
        _Threshold ("Threshold", Range(0,1)) = 0.4
        _Smooth ("Smooth", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _KeyColor;
            float _Threshold;
            float _Smooth;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float diff = distance(col.rgb, _KeyColor.rgb);
                float alpha = smoothstep(_Threshold, _Threshold + _Smooth, diff);
                col.a = alpha;
                return col;
            }
            ENDHLSL
        }
    }
}