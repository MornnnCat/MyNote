using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ViewFrustumCulling : MonoBehaviour
{
    [SerializeField] ComputeShader computeShader;
    ComputeBuffer computeBuffer, meshBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    public Mesh mesh;
    public Material mat;
    Bounds b = new Bounds(Vector3.zero, new Vector3(10000, 10000, 10000));
    [Range(10, 100000)]
    public int num = 64;
    [Range(0, 6)]
    public int DepthMM;
    void OnEnable()
    {
        HizDepthTexCreate();
        computeBuffer = new ComputeBuffer(num, sizeof(float) * 7, ComputeBufferType.Append);
        meshBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        computeBuffer.name = "MainCS";
        if (mesh != null)
        {
            args[0] = (uint)mesh.GetIndexCount(0);
            args[1] = (uint)0;
            args[2] = (uint)mesh.GetIndexStart(0);
            args[3] = (uint)mesh.GetBaseVertex(0);
        }
        else
        {
            args[0] = args[1] = args[2] = args[3] = 0;
        }
        meshBuffer.SetData(args);
    }


    void Update()
    {
        HizMipMapRender();
        computeShader.SetTexture(0, "_HizDepthTex", m_depthTexture);
        computeShader.SetVector("_DepthTexSize", new Vector2(m_depthTexture.width, m_depthTexture.height));
        computeShader.SetInt("mipmapLevel", DepthMM);
        ComputeShader();
        mat.SetBuffer("_CBuffer", computeBuffer);
        mat.SetFloat("_Step", 1);
        DrawMeshInstancedIndirect();
    }

    void ComputeShader()
    {
        int size = Mathf.CeilToInt(num / 4096);
        computeBuffer.SetCounterValue(0);
        computeShader.SetBuffer(0, "_CBuffer", computeBuffer);
        computeShader.SetFloat("_Num", num);
        computeShader.SetFloat("_Size", size);
        computeShader.SetMatrix("mat_VP", Camera.main.projectionMatrix * Camera.main.worldToCameraMatrix);
        GetPlane();
        computeShader.Dispatch(0, 8, 8, 1);
        //将computeBuffer中的计数赋值给meshBuffer 从第sizeof(uint)字节开始 args[1]
        ComputeBuffer.CopyCount(computeBuffer, meshBuffer, sizeof(uint));
    }

    void DrawMeshInstancedIndirect()
    {
        //缓冲区 bufferWithArgs 必须在给定的 argsOffset 偏移处具有五个整数： 每个实例的索引数、实例数、起始索引位置、基顶点位置、起始实例位置。
        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, b, meshBuffer);
    }

    private void OnDisable()
    {
        Clear();
    }
    private void OnApplicationQuit()
    {
        Clear();
    }

    void Clear()
    {
        computeBuffer?.Release();
        computeBuffer = null;
        meshBuffer?.Release();
        meshBuffer = null;
    }
    public static Matrix4x4 ScreenRay()
    {
        Camera camera = Camera.main;
        Matrix4x4 frustumCorners = Matrix4x4.identity;

        float fov = camera.fieldOfView; 
        float near = camera.nearClipPlane; 
        float aspect = camera.aspect; 

        float halfHeight = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        Vector3 toRight = camera.transform.right * halfHeight * aspect;
        Vector3 toTop = camera.transform.up * halfHeight;

        Vector3 topLeft = camera.transform.forward * near + toTop - toRight;
      
        topLeft /= near; 

        Vector3 topRight = camera.transform.forward * near + toRight + toTop;
     
        topRight /= near;

        Vector3 bottomLeft = camera.transform.forward * near - toTop - toRight;
       
        bottomLeft /= near;

        Vector3 bottomRight = camera.transform.forward * near + toRight - toTop;

        bottomRight /= near;

        frustumCorners.SetRow(0, bottomLeft);
        frustumCorners.SetRow(1, bottomRight);
        frustumCorners.SetRow(2, topRight);
        frustumCorners.SetRow(3, topLeft);
        return frustumCorners;
    }
    void GetPlane()
    {
        Vector4 f, b;
        Vector3 l, r, t, d;
        Camera camera = Camera.main;
        f = camera.transform.forward; f.w = camera.farClipPlane;
        b = -f; b.w = camera.nearClipPlane;

        var dir = ScreenRay();
        t = Vector3.Cross(new Vector3(dir.m30, dir.m31, dir.m32), camera.transform.right);
        d = Vector3.Cross(new Vector3(dir.m10, dir.m11, dir.m12), -camera.transform.right);
        l = Vector3.Cross(new Vector3(dir.m30, dir.m31, dir.m32), camera.transform.up);
        r = Vector3.Cross(new Vector3(dir.m10, dir.m11, dir.m12), -camera.transform.up);


        computeShader.SetVector("p_left", l);
        computeShader.SetVector("p_right", r);
        computeShader.SetVector("p_top", t);
        computeShader.SetVector("p_down", d);
        computeShader.SetVector("p_forwad", f);
        computeShader.SetVector("p_back", b);
        computeShader.SetVector("p_point", camera.transform.position);
    }

    #region "Hiz"
    RenderTexture m_depthTexture, m_lastFdepthTexture;
    Vector2Int m_depthTextureSize;
    Material m_depthTextureMaterial;
    // CommandBuffer commandBuffer;
    public Vector2Int depthTextureSize
    {
        get
        {
            if (m_depthTextureSize == Vector2Int.zero)
                m_depthTextureSize = new Vector2Int(Screen.width, Screen.height);
            return m_depthTextureSize;
        }
    }

    void HizDepthTexCreate()
    {
        m_depthTextureMaterial = new Material(Shader.Find("LF/HizDepthMipMap"));
        m_depthTexture = new RenderTexture(depthTextureSize.x, depthTextureSize.y, 0, RenderTextureFormat.RHalf);
        m_lastFdepthTexture = new RenderTexture(depthTextureSize.x, depthTextureSize.y, 0, RenderTextureFormat.RHalf);
        m_depthTexture.autoGenerateMips = false;//Mipmap自动生成
        m_depthTexture.useMipMap = true;
        m_depthTexture.filterMode = FilterMode.Point;
        m_depthTexture.Create();
    }

    void HizMipMapRender()
    {
        Vector2Int w = new Vector2Int(Screen.width, Screen.height);
        int mipmapLevel = 0;
        RenderTexture currentRenderTexture = null;//当前mipmapLevel对应的mipmap
        RenderTexture preRenderTexture = null;//上一层的mipmap，即mipmapLevel-1对应的mipmap

        //如果当前的mipmap的宽高大于8，则计算下一层的mipmap
        while (w.x > 8)
        {
            currentRenderTexture = RenderTexture.GetTemporary(w.x, w.y, 0, RenderTextureFormat.RHalf);
            currentRenderTexture.filterMode = FilterMode.Point;
            if (preRenderTexture == null)
            {
                //Mipmap[0]即copy原始的深度图
                Graphics.Blit(null, currentRenderTexture, m_depthTextureMaterial, 1);
            }
            else
            {
                //将Mipmap[i] Blit到Mipmap[i+1]上
                Graphics.Blit(preRenderTexture, currentRenderTexture, m_depthTextureMaterial, 0);
                RenderTexture.ReleaseTemporary(preRenderTexture);
            }
            Graphics.CopyTexture(currentRenderTexture, 0, 0, m_depthTexture, 0, mipmapLevel);
            preRenderTexture = currentRenderTexture;

            w /= 2;
            mipmapLevel++;
        }
        RenderTexture.ReleaseTemporary(preRenderTexture);

    }

    // private void OnGUI()
    // {
    //     GUI.DrawTexture(new Rect(0, 0, depthTextureSize.x / 4, depthTextureSize.y / 4), m_depthTexture, ScaleMode.ScaleAndCrop, false);
    // }

    #endregion
}
