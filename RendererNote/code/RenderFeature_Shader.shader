Shader "Hidden/ShaderName"
{
    SubShader
    {
        Pass
        {
            Name "Unlit"
            ZTest Off
            
            //不使用_BlitTexture混合图像，使用透明混合计算
            /*ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha*/
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"  
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"  
            
            half4 GetSource(half2 uv) {  
                return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);  
            }
            half4 frag (Varyings i) : SV_Target
            {
                half4 col = GetSource(i.texcoord) * half4(1,0,0,1);
                return col;
            }
            ENDHLSL
        }
    }
}
