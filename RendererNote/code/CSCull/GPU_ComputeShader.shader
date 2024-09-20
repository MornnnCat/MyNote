Shader "LF/GPU_ComputeShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags       
        { 
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque" 
        }
        LOD 100
        // ZTest Greater
        // ZWrite off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        //GPUIns
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

        //GPUIns

        float4 _Color;
        float4 _MainTex_ST;


        ENDHLSL
        Pass
        {

            HLSLPROGRAM
            // GPUIns
            #pragma multi_compile_instancing
            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);


            struct CBuffer
            {
                float3 pos;
                float4 col;
            };
            StructuredBuffer<CBuffer> _CBuffer;
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint id:SV_InstanceID;
                UNITY_VERTEX_INPUT_INSTANCE_ID //GPUIns
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 col : TEXCOORD2;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID//GPUIns
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);//GPUIns
                UNITY_TRANSFER_INSTANCE_ID(v, o);//GPUIns
                float3 position =  _CBuffer[v.id].pos;
                o.col = _CBuffer[v.id].col;
                float4x4 mat = float4x4(
                float4(1,0,0,position.x),
                float4(0,1,0,position.y),
                float4(0,0,1,position.z),
                float4(0,0,0,1)
                );
                unity_ObjectToWorld = mat;
                // unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
                // unity_ObjectToWorld._m00_m11_m22 = _Step;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                float4 st = _MainTex_ST;//GPUIns
                o.worldPos = TransformObjectToWorld(v.vertex);
                o.uv = v.uv * st.xy + st.zw;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // sample the texture
                UNITY_SETUP_INSTANCE_ID(i);//GPUIns
                float4 col = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex, i.uv)*i.col;
                return col;//GPUIns
            }
            ENDHLSL
        }
    }
}
