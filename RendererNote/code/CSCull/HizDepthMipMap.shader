Shader "LF/HizDepthMipMap"
{
    Properties
    {
        [HideInInspector]_MainTex ("Texture", 2D) = "black" {}
    }
    SubShader
    {
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;

        ENDHLSL
        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }


            float CalculatorMipmapDepth(float2 uv)
            {
                float4 depth;
                float offset = _MainTex_TexelSize.x;
                depth.x = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, uv);
                depth.y = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, uv + float2(0, offset));
                depth.z = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, uv + float2(offset, 0));
                depth.w = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, uv + float2(offset, offset));
                return max(max(depth.x, depth.y), max(depth.z, depth.w));
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = CalculatorMipmapDepth(i.uv);
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }


            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
                float4 depth = 1-SAMPLE_TEXTURE2D(_CameraDepthTexture,sampler_CameraDepthTexture,i.uv);
                float lineDepth = LinearEyeDepth(depth,_ZBufferParams);
                return depth;
            }
            ENDHLSL
        }
        
    }
}
