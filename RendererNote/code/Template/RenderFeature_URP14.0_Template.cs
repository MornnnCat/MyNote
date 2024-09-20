using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TemplateRenderFeature : ScriptableRendererFeature
{
    // 此处有一个奇怪的Bug，代码的插入点需要在脚本中进行初始化，否则挂载后除非重启项目，否则不会再次更改插入点
    public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRendering;
    private Shader _shader;
    private const string ShaderName = "Hidden/ShaderName";
    private Material _material;
    private RenderPass _renderPass;
    class RenderPass : ScriptableRenderPass
    {
        private Material _mat;
        //private RenderTextureDescriptor _descriptor;
        //private string TmpTextureName = "_TMPTexture";
        private RTHandle _sourceTexture, _destTexture;
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("Tamplate Render Pass");
        private static readonly int ShaderProperties = Shader.PropertyToID("_PropertiesName");
        //private float _shaderValue;
        
        internal bool Setup(ref Material material) {
            _mat = material;
            ConfigureInput(ScriptableRenderPassInput.Color);
            return _mat != null;
        } 

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            //创建临时纹理
            /*_descriptor = renderingData.cameraData.cameraTargetDescriptor;
            _descriptor.depthBufferBits = 0;
            _descriptor.height = 90;
            _descriptor.width = 160;
            RenderingUtils.ReAllocateIfNeeded(ref _sourceTexture, _descriptor, name: TmpTextureName);*/
            
            // 写入_BlitTexture供Shader使用
            _sourceTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
            // 配置目标和清除
            ConfigureTarget(_sourceTexture);
            ConfigureClear(ClearFlag.None, Color.white);
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            
            _destTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                //_mat.SetFloat(ShaderProperties, _shaderValue);
                Blitter.BlitCameraTexture(cmd, _sourceTexture, _destTexture, _mat, 0);
                
                //如果使用透明混合，不使用_BlitTexture，则可以不配置_sourceTexture
                //Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, _destTexture, _mat, 0);
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            //释放临时纹理的内存
            //_sourceTexture?.Release();
        }
    }

    

    public override void Create()
    {
        if (_renderPass == null)
        {
            _renderPass = new RenderPass();
            _renderPass.renderPassEvent = renderPassEvent;
        }
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (GetMaterial())
        {
            Debug.LogErrorFormat("{0}.AddRenderPasses(): Missing material. {1} render pass will not be added.", GetType().Name, name);
            return;
        }
        bool canAddPass = _renderPass.Setup(ref _material);
        if (canAddPass) renderer.EnqueuePass(_renderPass);
    }

    private bool GetMaterial()
    {
        if (_shader == null)
        {
            _shader = Shader.Find(ShaderName);
        }

        if (_material == null)
        {
            _material = CoreUtils.CreateEngineMaterial(_shader);
        }
        return _material == null;
    }
}


