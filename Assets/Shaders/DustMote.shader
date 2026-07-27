Shader "Atmosphere/DustMote"
{
    // Пылинка в воздухе.
    //
    // Ключевое отличие от обычного Lit-партикла: здесь НЕТ NdotL. Билборд всегда
    // повёрнут к камере, поэтому у честного Lambert'а пылинка между игроком и
    // лампой оказывается чёрной — то есть ровно там, где настоящая пыль светится
    // ярче всего. Мелкая частица рассеивает свет во все стороны, так что берём
    // только затухание с расстоянием, без косинуса.
    Properties
    {
        _Color        ("Tint", Color) = (1, 0.95, 0.85, 1)
        _LightBoost   ("Light boost", Range(0, 8)) = 1.6
        _AmbientBoost ("Ambient boost", Range(0, 4)) = 0.35
        _Softness     ("Edge softness", Range(0.5, 8)) = 2.0
        _MaxAlpha     ("Max alpha", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"        = "Transparent"
            "RenderType"   = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType"  = "Plane"
        }

        Pass
        {
            Name "DustForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One   // premultiplied-ish additive: пыль только подсвечивается
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // URP 17 (Unity 6): Forward+ keyword называется _CLUSTER_LIGHT_LOOP.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _LightBoost;
                half  _AmbientBoost;
                half  _Softness;
                half  _MaxAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                half4  color      : COLOR;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv         = IN.uv;
                OUT.color      = IN.color * _Color;   // ParticleSystem кладёт сюда фейд по времени жизни
                OUT.fogFactor  = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Круглая мягкая точка вместо текстуры — пылинка занимает пару пикселей,
                // текстуру там всё равно никто не разглядит.
                float d = length(IN.uv * 2.0 - 1.0);
                half  a = pow(saturate(1.0 - d), _Softness);

                // Свет: только затухание с расстоянием и тени, без косинуса нормали.
                half3 lighting = SampleSH(half3(0, 1, 0)) * _AmbientBoost;

                Light mainLight = GetMainLight();
                lighting += mainLight.color * mainLight.distanceAttenuation;

                #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = half3(0, 1, 0);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                    lighting += light.color * light.distanceAttenuation;
                LIGHT_LOOP_END
                #endif

                half3 rgb = IN.color.rgb * lighting * _LightBoost;
                half  alpha = saturate(a * IN.color.a * _MaxAlpha);

                // Яркость света решает, видно пылинку или нет: в тёмном углу она гаснет
                // сама собой, в конусе лампы — вспыхивает.
                alpha *= saturate(Luminance(rgb));
                alpha *= ComputeFogIntensity(IN.fogFactor);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
