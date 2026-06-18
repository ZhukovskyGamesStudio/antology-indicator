Shader "Custom/LitSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _AmbientBoost("Ambient Boost", Range(0,2)) = 1.0
        _AlphaClip("Alpha Clip (discard below)", Range(0,1)) = 0.004

        // Blend state (материал-управляемое). Дефолты = обычный альфа-блендинг,
        // чтобы _Color.a / альфа текстуры давали прозрачность (нужно для BlendItem).
        [HideInInspector] _SrcBlend("__src", Float) = 5.0   // SrcAlpha
        [HideInInspector] _DstBlend("__dst", Float) = 10.0  // OneMinusSrcAlpha
        [HideInInspector] _ZWrite("__zw", Float) = 0.0      // выкл для прозрачности
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            // Прозрачный альфа-блендинг. Альфа берётся из текстуры * _Color.a,
            // поэтому DOFade(_Color.a) в BlendItem плавно проявляет/гасит спрайт.
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // Lighting keywords (mirror URP forward path, incl. Forward+ clustered lights)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // URP 17 (Unity 6) переименовал Forward+ keyword: _FORWARD_PLUS -> _CLUSTER_LIGHT_LOOP.
            // Со старым именем блок additional lights ниже вырезался компилятором и спрайт был чёрным.
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _AmbientBoost;
                half   _AlphaClip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half4  color      : COLOR;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color * _Color;   // SpriteRenderer bakes its tint into vertex color
                OUT.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, half facing : VFACE) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                // Отбрасываем только практически прозрачные тексели (overdraw),
                // НЕ режем по высокому порогу — иначе фейд альфы «щёлкал» бы скачком.
                clip(tex.a - _AlphaClip);

                // Two-sided: make the visible face's normal point toward the camera side.
                float3 N = normalize(IN.normalWS) * sign(facing);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                if (dot(N, viewDir) < 0) N = -N;

                // Ambient / baked GI
                half3 lighting = SampleSH(N) * _AmbientBoost;

                // Main directional light
                Light mainLight = GetMainLight();
                lighting += mainLight.color * mainLight.distanceAttenuation * saturate(dot(N, mainLight.direction));

                // Additional lights (point / spot), Forward+ / clustered aware
                #if defined(_ADDITIONAL_LIGHTS) || USE_CLUSTER_LIGHT_LOOP
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = viewDir;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = GetAdditionalLight(lightIndex, IN.positionWS);
                    lighting += light.color * light.distanceAttenuation * saturate(dot(N, light.direction));
                LIGHT_LOOP_END
                #endif

                half3 rgb = tex.rgb * lighting;
                rgb = MixFog(rgb, IN.fogFactor);
                // Прозрачность: альфа = альфа текстуры * _Color.a (через IN.color).
                return half4(rgb, tex.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
