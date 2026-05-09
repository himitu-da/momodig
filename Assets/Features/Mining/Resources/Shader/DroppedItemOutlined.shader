Shader "Custom/DroppedItemOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.92, 0.78, 1)
        _OutlineWidth("Outline Width (world)", Range(0.0, 0.1)) = 0.025
        _SurfaceOffset("Surface Offset (world)", Range(0.0, 0.02)) = 0.002
        _OutlineIntensity("Outline Intensity", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Name "SurfaceEdges"
            Cull Back
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineWidth;
                half _SurfaceOffset;
                half _OutlineIntensity;
            CBUFFER_END

            float GetObjectScale(float3 axisOS)
            {
                return max(length(mul((float3x3)GetObjectToWorldMatrix(), axisOS)), 0.0001);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz) + normalWS * _SurfaceOffset * _OutlineIntensity;
                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS = normalize(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                clip(_OutlineIntensity - 0.001);

                float3 absNormal = abs(normalize(IN.normalOS));
                float3 absPos = abs(IN.positionOS);

                float edgeDistOS;
                float widthOS;
                if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
                {
                    edgeDistOS = min(0.5 - absPos.y, 0.5 - absPos.z);
                    widthOS = _OutlineWidth / min(GetObjectScale(float3(0, 1, 0)), GetObjectScale(float3(0, 0, 1)));
                }
                else if (absNormal.y >= absNormal.x && absNormal.y >= absNormal.z)
                {
                    edgeDistOS = min(0.5 - absPos.x, 0.5 - absPos.z);
                    widthOS = _OutlineWidth / min(GetObjectScale(float3(1, 0, 0)), GetObjectScale(float3(0, 0, 1)));
                }
                else
                {
                    edgeDistOS = min(0.5 - absPos.x, 0.5 - absPos.y);
                    widthOS = _OutlineWidth / min(GetObjectScale(float3(1, 0, 0)), GetObjectScale(float3(0, 1, 0)));
                }

                float softness = max(widthOS * 0.25, 0.0005);
                float edgeMask = 1.0 - smoothstep(widthOS, widthOS + softness, edgeDistOS);
                clip(edgeMask - 0.01);

                half4 col = _OutlineColor;
                col.a *= _OutlineIntensity * edgeMask;
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Silhouette"
            Cull Front
            ZTest LEqual
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _OutlineWidth;
                half _SurfaceOffset;
                half _OutlineIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                float3 worldNormal = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 inflated = worldPos + worldNormal * _OutlineWidth * _OutlineIntensity;
                OUT.positionHCS = TransformWorldToHClip(inflated);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                clip(_OutlineIntensity - 0.001);

                half4 col = _OutlineColor;
                col.a *= _OutlineIntensity;
                return col;
            }
            ENDHLSL
        }
    }
}
