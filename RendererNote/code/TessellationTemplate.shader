Shader "XT/TessellationTemplate"
{
    Properties
    {
        [NoScaleOffset]_BaseMap ("Base Map", 2D) = "white" {}  
        
        [Header(Tess)][Space]
     
        [KeywordEnum(integer, fractional_even, fractional_odd)]_Partitioning ("Partitioning Mode", Float) = 0
        [KeywordEnum(triangle_cw, triangle_ccw)]_Outputtopology ("Outputtopology Mode", Float) = 0
        _EdgeFactor ("EdgeFactor", Range(1,8)) = 4 
        _InsideFactor ("InsideFactor", Range(1,8)) = 4 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
         
        Pass
        { 
            HLSLPROGRAM
            #pragma target 4.6 
            #pragma vertex FlatTessVert
            #pragma fragment FlatTessFrag 
            #pragma hull FlatTessControlPoint
            #pragma domain FlatTessDomain
             
            #pragma multi_compile _PARTITIONING_INTEGER _PARTITIONING_FRACTIONAL_EVEN _PARTITIONING_FRACTIONAL_ODD 
            #pragma multi_compile _OUTPUTTOPOLOGY_TRIANGLE_CW _OUTPUTTOPOLOGY_TRIANGLE_CCW 
   
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl" 

            CBUFFER_START(UnityPerMaterial) 
            float _EdgeFactor; 
            float _InsideFactor; 
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap); 

            struct Attributes
            {
                float3 positionOS   : POSITION; 
                float2 texcoord     : TEXCOORD0;
    
            };

            struct VertexOut{
                float3 positionOS : INTERNALTESSPOS; 
                float2 texcoord : TEXCOORD0;
            };
             
            struct PatchTess {  
                float edgeFactor[3] : SV_TESSFACTOR;
                float insideFactor  : SV_INSIDETESSFACTOR;
            };

            struct HullOut{
                float3 positionOS : INTERNALTESSPOS; 
                float2 texcoord : TEXCOORD0;
            };

            struct DomainOut
            {
                float4  positionCS      : SV_POSITION;
                float2  texcoord        : TEXCOORD0; 
            };


            VertexOut FlatTessVert(Attributes input){ 
                VertexOut o;
                o.positionOS = input.positionOS; 
                o.texcoord   = input.texcoord;
                return o;
            }
   
   
            PatchTess PatchConstant (InputPatch<VertexOut,3> patch, uint patchID : SV_PrimitiveID){ 
                PatchTess o;
                o.edgeFactor[0] = _EdgeFactor;
                o.edgeFactor[1] = _EdgeFactor; 
                o.edgeFactor[2] = _EdgeFactor;
                o.insideFactor  = _InsideFactor;
                return o;
            }
 
            [domain("tri")]   
            #if _PARTITIONING_INTEGER
            [partitioning("integer")] 
            #elif _PARTITIONING_FRACTIONAL_EVEN
            [partitioning("fractional_even")] 
            #elif _PARTITIONING_FRACTIONAL_ODD
            [partitioning("fractional_odd")]    
            #endif 
 
            #if _OUTPUTTOPOLOGY_TRIANGLE_CW
            [outputtopology("triangle_cw")] 
            #elif _OUTPUTTOPOLOGY_TRIANGLE_CCW
            [outputtopology("triangle_ccw")] 
            #endif

            [patchconstantfunc("PatchConstant")] 
            [outputcontrolpoints(3)]                 
            [maxtessfactor(64.0f)]                 
            HullOut FlatTessControlPoint (InputPatch<VertexOut,3> patch,uint id : SV_OutputControlPointID){  
                HullOut o;
                o.positionOS = patch[id].positionOS;
                o.texcoord = patch[id].texcoord; 
                return o;
            }
 
  
            [domain("tri")]      
            DomainOut FlatTessDomain (PatchTess tessFactors, const OutputPatch<HullOut,3> patch, float3 bary : SV_DOMAINLOCATION)
            {  
                float3 positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z; 
	            float2 texcoord   = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
                   
                DomainOut output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.texcoord = texcoord;
                return output; 
            }
 

            half4 FlatTessFrag(DomainOut input) : SV_Target{   
                half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord).rgb;
                return half4(color, 1.0); 
            }
              
            ENDHLSL
        }
    }
}

