// Subtle CRT scanlines + flicker for a station-monitor Quad.
// Apply to a transparent Quad placed in front of a screen mesh.
Shader "Custom/ScreenFlicker"
{
    Properties
    {
        _BaseColor      ("Base Color",      Color) = (0.6, 0.85, 1.0, 0.35)
        _ScanlineColor  ("Scanline Color",  Color) = (0.0, 0.0, 0.0, 0.45)
        _ScanlineCount  ("Scanline Count",  Float) = 220.0
        _ScanlineSpeed  ("Scanline Speed",  Float) = 0.6
        _FlickerSpeed   ("Flicker Speed",   Float) = 18.0
        _FlickerAmount  ("Flicker Amount",  Range(0,1)) = 0.18
        _NoiseAmount    ("Noise Amount",    Range(0,1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ScanlineColor;
                float  _ScanlineCount;
                float  _ScanlineSpeed;
                float  _FlickerSpeed;
                float  _FlickerAmount;
                float  _NoiseAmount;
            CBUFFER_END

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // Scanlines drift slowly down the screen
                float scan = sin((IN.uv.y + t * _ScanlineSpeed * 0.05) * _ScanlineCount * 3.14159);
                scan = saturate(scan * 0.5 + 0.5);

                // Mix base color with scanline color based on the scan value
                half4 col = lerp(_BaseColor, _ScanlineColor, scan * _ScanlineColor.a);

                // Per-frame flicker
                float flicker = 1.0 - _FlickerAmount * (0.5 + 0.5 * sin(t * _FlickerSpeed));

                // High-frequency noise
                float n = hash(IN.uv * 1024.0 + t);
                col.rgb *= flicker;
                col.rgb = lerp(col.rgb, col.rgb * n, _NoiseAmount);

                col.a = _BaseColor.a;
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
