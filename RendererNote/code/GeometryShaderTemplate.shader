Shader "XT/GeometryShaderExample" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            
            #include "UnityCG.cginc"
            
            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct v2g {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            struct g2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            float4 _Color;
            
            v2g vert (appdata v) {
                v2g o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            [maxvertexcount(3)]
            void geom (triangle v2g v[3], inout TriangleStream<g2f> triStream)
            {
                g2f o;
                o.pos = v[0].pos;
                o.uv = v[0].uv;
                triStream.Append(o);
                o.pos = v[1].pos;
                o.uv = v[1].uv;
                triStream.Append(o);
                o.pos = v[2].pos;
                o.uv = v[2].uv;
                triStream.Append(o);
            }
            
            float4 frag (g2f i) : SV_Target {
                return _Color;
            }
            ENDCG
        }
    }
}