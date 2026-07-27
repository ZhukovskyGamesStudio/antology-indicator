Shader "Atmosphere/LampHaze"
{
    // Мягкий аддитивный ореол вокруг лампы — «свет в пыльном воздухе».
    //
    // Честной волюметрики в проекте нет и не надо: стиль рисованный, и настоящий
    // объём читался бы как грязь. Здесь просто билборд с радиальным затуханием,
    // который: (1) всегда смотрит в кадр, (2) мягко гаснет у стен через depth-fade,
    // чтобы не было видно круглого края квада, (3) гаснет вблизи камеры, чтобы
    // при заходе внутрь лампы экран не белел.
    Properties
    {
        _Color      ("Tint", Color) = (1, 0.82, 0.55, 1)
        _Intensity  ("Intensity", Float) = 1.0
        _Falloff    ("Falloff", Range(0.5, 8)) = 2.5
        _Core       ("Core size", Range(0, 0.9)) = 0.0
        _SoftFade   ("Depth soft fade (m)", Float) = 0.75
        _NearFade   ("Near camera fade (m)", Float) = 0.6
    }

    SubShader
    {
        Tags
        {
            "Queue"        = "Transparent+100"
            "RenderType"   = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType"  = "Plane"
        }

        Pass
        {
            Name "Haze"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One       // аддитивно: ореол только добавляет свет, ничего не затемняет
            ZWrite Off
            ZTest LEqual        // за стеной ореола не видно
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
                float _Falloff;
                float _Core;
                float _SoftFade;
                float _NearFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  eyeDepth   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                // Билборд: центр объекта уводим во view space, а вершину раскладываем
                // по осям камеры. Поворот трансформа при этом не важен — лампу можно
                // крутить как угодно, ореол всё равно смотрит в кадр.
                float3 centerVS = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));

                // Размер берём из масштаба трансформа, чтобы радиус правился в инспекторе.
                float sx = length(float3(UNITY_MATRIX_M[0][0], UNITY_MATRIX_M[1][0], UNITY_MATRIX_M[2][0]));
                float sy = length(float3(UNITY_MATRIX_M[0][1], UNITY_MATRIX_M[1][1], UNITY_MATRIX_M[2][1]));

                float3 posVS = centerVS + float3(IN.positionOS.x * sx, IN.positionOS.y * sy, 0);

                OUT.positionCS = TransformWViewToHClip(posVS);
                OUT.uv         = IN.uv;
                OUT.eyeDepth   = -posVS.z;
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Радиальное затухание, без текстуры: _Core держит ровное ядро,
                // _Falloff решает, насколько мягко ореол растворяется к краю.
                float d = length(IN.uv * 2.0 - 1.0);
                float a = saturate((1.0 - d) / max(1.0 - _Core, 1e-4));
                a = pow(a, _Falloff);

                // Мягкое подрезание о геометрию: без этого виден круглый край квада,
                // воткнувшегося в потолок или стену.
                float sceneEye = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(IN.positionCS)), _ZBufferParams);
                a *= saturate((sceneEye - IN.eyeDepth) / max(_SoftFade, 1e-4));

                // И гасим, когда камера почти уткнулась в ореол.
                a *= saturate(IN.eyeDepth / max(_NearFade, 1e-4));

                // Аддитивный блендинг: туман должен ослаблять добавку, а не подмешивать
                // свой цвет, поэтому не MixFog, а простое умножение на плотность.
                a *= ComputeFogIntensity(IN.fogFactor);

                return half4(_Color.rgb * (_Color.a * _Intensity * a), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
