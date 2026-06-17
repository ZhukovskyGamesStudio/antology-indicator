Shader "Custom/LitSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _AmbientBoost("Ambient Boost", Range(0,2)) = 1.0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "RenderType"="TransparentCutout"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            // Opaque alpha-clip: pixels above _Cutoff are fully solid (black stays black),
            // the rest are discarded. No blending → no background bleeding through.
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            // Lighting keywords (mirror URP forward path, incl. Forward+ clustered lights)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _AmbientBoost;
                half   _Cutoff;
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
                clip(tex.a - _Cutoff);

                // Two-sided: make the visible face's normal point toward the camera side.
                float3 N = normalize(IN.normalWS) * sign(facing);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                if (dot(N, viewDir) < 0) N = -N;

                // Ambient / baked GI
                half3 lighting = SampleSH(N) * _AmbientBoost;

                // Main directional light
                Light mainLight = GetMainLight();
                lighting += mainLight.color * mainLight.distanceAttenuation * saturate(dot(N, mainLight.direction));

                // Additional lights (point / spot), Forward+ aware
                #if defined(_ADDITIONAL_LIGHTS) || defined(_FORWARD_PLUS)
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
                // Opaque pass: kept pixels are fully solid. Force alpha = 1 so framebuffer-alpha
                // consumers (editor preview, WebGL canvas) don't render the sprite see-through.
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
