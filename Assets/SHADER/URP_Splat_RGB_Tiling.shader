Shader "Custom/URP_Splat_RGB_Lit_MobileStable_Final"
{
    Properties
    {
        _SplatMap ("Splat Map (RGB)", 2D) = "white" {}

        // BASE
        _BaseTex ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseOpacity ("Base Opacity", Range(0,1)) = 1

        // SPLAT
        _TexR ("Texture R", 2D) = "white" {}
        _TexG ("Texture G", 2D) = "white" {}
        _TexB ("Texture B", 2D) = "white" {}

        _ColorR ("Color R", Color) = (1,1,1,1)
        _ColorG ("Color G", Color) = (1,1,1,1)
        _ColorB ("Color B", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 200

        // =================================================================
        // FORWARD LIT PASS
        // =================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float fogFactor    : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_SplatMap);       SAMPLER(sampler_SplatMap);
            TEXTURE2D(_BaseTex);        SAMPLER(sampler_BaseTex);
            TEXTURE2D(_TexR);           SAMPLER(sampler_TexR);
            TEXTURE2D(_TexG);           SAMPLER(sampler_TexG);
            TEXTURE2D(_TexB);           SAMPLER(sampler_TexB);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTex_ST;
                float4 _TexR_ST;
                float4 _TexG_ST;
                float4 _TexB_ST;
                float4 _BaseColor;
                float4 _ColorR;
                float4 _ColorG;
                float4 _ColorB;
                float _BaseOpacity;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.uv         = TRANSFORM_TEX(v.uv, _BaseTex);
                o.shadowCoord = TransformWorldToShadowCoord(o.positionWS);
                o.fogFactor   = ComputeFogFactor(o.positionCS.z);

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float3 splat = SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, i.uv).rgb;

                float2 uvBase = TRANSFORM_TEX(i.uv, _BaseTex);
                float2 uvR = TRANSFORM_TEX(i.uv, _TexR);
                float2 uvG = TRANSFORM_TEX(i.uv, _TexG);
                float2 uvB = TRANSFORM_TEX(i.uv, _TexB);

                float3 baseCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, uvBase).rgb * _BaseColor.rgb;
                baseCol *= _BaseOpacity;

                float3 colR = SAMPLE_TEXTURE2D(_TexR, sampler_TexR, uvR).rgb * _ColorR.rgb;
                float3 colG = SAMPLE_TEXTURE2D(_TexG, sampler_TexG, uvG).rgb * _ColorG.rgb;
                float3 colB = SAMPLE_TEXTURE2D(_TexB, sampler_TexB, uvB).rgb * _ColorB.rgb;

                float3 splatColor = colR * splat.r + colG * splat.g + colB * splat.b;
                float3 albedo = lerp(splatColor, baseCol, _BaseOpacity);

                float3 normalWS = normalize(i.normalWS);
                float3 lighting = 0;

                // MAIN LIGHT
                Light mainLight = GetMainLight(i.shadowCoord);
                float3 mainDiffuse = albedo * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation) * saturate(dot(normalWS, mainLight.direction));
                lighting += mainDiffuse;

                // ADDITIONAL LIGHTS 
                int addLightsCount = GetAdditionalLightsCount();
                for (int l = 0; l < addLightsCount; ++l)
                {
                    Light addLight = GetAdditionalLight(l, i.positionWS, half4(1,1,1,1));
                    float atten = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    lighting += albedo * addLight.color * atten * saturate(dot(normalWS, addLight.direction)); 
                }

                float3 ambient = SampleSH(normalWS) * albedo;
                float3 finalColor = lighting + ambient;
                finalColor = MixFog(finalColor, i.fogFactor);

                return half4(finalColor, 1);
            }
            ENDHLSL
        }

        // =================================================================
        // SHADOW CASTER PASS (ROBUSTO E CORRIGIDO)
        // =================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // NOTA: _MainLightPosition já é declarado automaticamente pelo URP 
            // nos arquivos incluídos acima. Não precisamos (e não podemos) redeclará-lo.

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float3 lightDirectionWS;
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    // Para luzes pontuais (Spot/Point), a direção é do vértice até a posição da luz
                    lightDirectionWS = normalize(_MainLightPosition.xyz - positionWS);
                #else
                    // Para luz direcional, o xyz já é a direção da luz
                    lightDirectionWS = _MainLightPosition.xyz;
                #endif

                // Aplica o Shadow Bias usando as funções nativas do URP (elimina Shadow Acne)
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                // Ajuste para Z-Reversed (padrão em plataformas modernas como DX11/DX12/Vulkan)
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // =================================================================
        // DEPTH ONLY PASS (Obrigatório para SSAO, Depth of Field, etc.)
        // =================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On 
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}