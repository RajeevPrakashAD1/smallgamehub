Shader "TrailTrap/BackgroundGrid"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.024, 0.039, 0.11, 1)
        _GridColor ("Grid", Color) = (0.106, 0.18, 0.333, 1)
        _CellSize  ("Cell Size (world units)", Float)                = 1
        _LineWidth ("Line Width (cell fraction)", Range(0.005, 0.2)) = 0.03
        _Pulse     ("Pulse (0-1)", Range(0, 1))                      = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionHCS : SV_POSITION; float2 worldXY : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _GridColor;
            float _CellSize, _LineWidth, _Pulse;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(IN.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(ws);
                o.worldXY = ws.xy;              // interpolated: every pixel learns its world pos
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 cell = abs(frac(IN.worldXY / _CellSize) - 0.5);
                float d = 0.5 - max(cell.x, cell.y);   // distance to nearest line (cell units)

                float aa = fwidth(d);                  // how much d changes across ONE pixel
                float lineAmt = 1 - smoothstep(_LineWidth, _LineWidth + aa, d);

                return lerp(_BaseColor, _GridColor, lineAmt * (0.65 + 0.35 * _Pulse));
            }
            ENDHLSL
        }
    }
}
