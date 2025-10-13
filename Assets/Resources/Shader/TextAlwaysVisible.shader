Shader "Custom/TextAlwaysVisible"
{
    Properties
    {
        _MainTex ("Font Atlas", 2D) = "white" {}
        [MainColor] _FaceColor ("Face Color", Color) = (1,1,1,1)
        _TextureWidth ("Texture Width", float) = 512
        _TextureHeight ("Texture Height", float) = 512
        _ScaleRatioA ("Scale RatioA", float) = 1
        
        // 背景設定
        _BackgroundColor ("Background Color", Color) = (0,0,0,0.5)
        _CornerRadius ("Corner Radius", Range(0, 0.5)) = 0.1
    }

    SubShader
    {
        Tags { "Queue"="Overlay+300" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
                float2 rectUV       : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _FaceColor;
                float _TextureWidth;
                float _TextureHeight;
                float _ScaleRatioA;
                half4 _BackgroundColor;
                float _CornerRadius;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                // 矩形UV（0-1の範囲）を計算
                OUT.rectUV = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 角丸矩形のSDF計算
                float2 rectPos = IN.rectUV * 2.0 - 1.0; // -1 to 1
                float2 d = abs(rectPos) - (1.0 - _CornerRadius * 2.0);
                float roundedRectDist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - _CornerRadius * 2.0;
                
                // 背景の描画（アンチエイリアス付き）
                float bgEdge = fwidth(roundedRectDist) * 0.5;
                float bgAlpha = smoothstep(bgEdge, -bgEdge, roundedRectDist);
                
                // TextMeshProのSDF距離を取得
                half distance = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                
                // ピクセル範囲を計算（高品質なアンチエイリアス用）
                float2 texelSize = float2(1.0 / _TextureWidth, 1.0 / _TextureHeight);
                float2 ddist = float2(ddx(IN.uv.x) * texelSize.x, ddy(IN.uv.y) * texelSize.y);
                float pixelScale = length(ddist) * _ScaleRatioA;
                
                // SDFベースのアンチエイリアス
                float scale = 4.0;
                float sd = (distance - 0.5) * scale + 0.5;
                float textAlpha = clamp((sd - 0.5) / pixelScale + 0.5, 0.0, 1.0);
                
                // テキストカラー
                half4 textColor = IN.color * _FaceColor;
                textColor.a *= textAlpha;
                
                // 背景カラー
                half4 backgroundColor = _BackgroundColor;
                backgroundColor.a *= bgAlpha;
                
                // 背景とテキストを合成（テキストが上）
                half4 finalColor = backgroundColor;
                finalColor.rgb = lerp(backgroundColor.rgb, textColor.rgb, textColor.a);
                finalColor.a = max(backgroundColor.a, textColor.a);
                
                // 完全に透明なピクセルを破棄
                if (finalColor.a < 0.01)
                    discard;

                return finalColor;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
