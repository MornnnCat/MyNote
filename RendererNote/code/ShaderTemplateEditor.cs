using UnityEditor;

public class ShaderTemplateEditor : Editor
{
    //Create/Shader/Unlit URP Shader指向菜单中所处的位置
    [MenuItem("Assets/Create/Shader/Unlit URP Shader")]
    static void UnlitURPShader()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        //文件ID
        string templatePath = AssetDatabase.GUIDToAssetPath("d96cbac321078e84c96346e801737c54");
        //起名
        string newPath = string.Format("{0}/New Unlit URP Shader.shader", path);
        AssetDatabase.CopyAsset(templatePath, newPath);
        AssetDatabase.ImportAsset(newPath);
    }
    
    [MenuItem("Assets/Create/Shader/Unlit Deferred Shading Shader")]
    static void UnlitDeferredShader()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        //文件ID
        string templatePath = AssetDatabase.GUIDToAssetPath("74a46f89d897ea24eb0edf1941c4108b");
        //起名
        string newPath = string.Format("{0}/New Unlit Deferred Shading Shader.shader", path);
        AssetDatabase.CopyAsset(templatePath, newPath);
        AssetDatabase.ImportAsset(newPath);
    }
    
    [MenuItem("Assets/Create/Shader/Unlit URP Tessellation Shader")]
    static void UnlitTessellationShader()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        //文件ID
        string templatePath = AssetDatabase.GUIDToAssetPath("2d96daa3b622f794ca110513db2872dd");
        //起名
        string newPath = string.Format("{0}/New Unlit  URP Tessellation Shader.shader", path);
        AssetDatabase.CopyAsset(templatePath, newPath);
        AssetDatabase.ImportAsset(newPath);
    }
    
    [MenuItem("Assets/Create/Shader/Unlit URP PostProcessing Shader")]
    static void UnlitPostProcessingShader()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        //文件ID
        string templatePath = AssetDatabase.GUIDToAssetPath("448adfc6e2c61744da26837c7dfc4689");
        //起名
        string newPath = string.Format("{0}/New Unlit  URP PostProcessing Shader.shader", path);
        AssetDatabase.CopyAsset(templatePath, newPath);
        AssetDatabase.ImportAsset(newPath);
    }
}
