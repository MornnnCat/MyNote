---
typora-copy-images-to: img
---

## 常用功能



### 常用调试代码

[ExecuteInEditMode] ：编辑器模式运行

断点调试：加断点->运行场景->F5调试->F11运行下一句->窗口快速监视/即时窗口

Update方法必须使用单帧调试：启动调试->运行场景 暂停->加断点->F11

性能测试：

```c#
Profiler.BeginSample("performance test");
……
Profiler.EndSample();
```

### 视锥体AABB包围盒

注意：Graphics.DrawMeshInstancedProcedural中会接收一个Bounds值，其作用是使实例化物体坐标空间转换至相对Bounds的空间。（根本不是内置的AABB裁切，屁用都没有，被骗了！）

https://zhuanlan.zhihu.com/p/55915345



unity引擎提供一些很方便的方法用于计算AABB盒

```c#
Plane[] cameraFrustumPlanes = new Plane[6];// 存储AABB盒的六个面
GeometryUtility.CalculateFrustumPlanes(cam, cameraFrustumPlanes);// 计算AABB盒的六个面
//or
//Plane[] cameraFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);

//计算单元对象的包围盒
Bounds bound = new Bounds(centerPosWS, sizeWS);

bool isInPlane = GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, bound);// 判断bound是否在cameraFrustumPlanes内，返回一个布尔值

```



#### CustomGPUGrass_GrassCompute.cs

本代码中，首先基于当前贴图进行初始化，把像素为单位的数据映射到以三角面为单位，同时计算出这个三角面的包围盒，然后存储进这个字典中

```c#
Dictionary<Bounds, List<Matrix4x4>> _grassInfos
```

但现在我想增加一种初始化模式，如果应用于地形编辑的话，这种用图片做信息中介的形式转换需要的算力过于巨大，几乎无法实时计算出来，因此要去掉图片做为信息中介，信息存储使用跟直接的形式，比如json数据文件，而交互也不使用RT了，而是把射线碰撞坐标直接传过去。

1. 首先把初始化代码移植到交互脚本中，CustomGPUGrass_RenderFeature.cs，并修改存储介质，不再存储在图片中，而是json
2. 之后实现一个实时计算脚本，直接访问修改_grassInfos，也会给出存储为json数据是方法
3. 如果_grassInfos为空，则CustomGPUGrass_GrassCompute会读取json文件

### 视锥体OBB包围盒





### 数据序列化

只有一些简单类型才可以被直接序列化，Dictionary、Bounds、Matrix4x4等复杂类型无法直接序列化，因此我们需要为这个类型编写对应的映射类，把它们分解成简单类型。

例如：


```c#
// 我们需要序列化这个字典
Dictionary<Bounds, List<Matrix4x4>> d;

// 则对应的映射类应该是：
    [Serializable]
    public class SerializableBounds
    {
        public Vector3 center = new Vector3();
        public Vector3 size = new Vector3();

        public Bounds ToBounds()
        {
            return new Bounds(center, size);
        }
    }
    [Serializable]
    public class SerializableMatrix4X4
    {
        public float[] values = new float[16];

        public Matrix4x4 ToMatrix4X4()
        {
            Matrix4x4 matrix = new Matrix4x4();
            for (int i = 0; i < 16; i++)
                matrix[i] = values[i];
            return matrix;
        }
    }
    [Serializable]
    public class SerializableData
    {
        public SerializableBounds bounds = new SerializableBounds();
        public List<SerializableMatrix4X4> matrixLists = new List<SerializableMatrix4X4>();
    }
    [Serializable]
    public class SerializableDataList
    {
        public List<SerializableData> data = new List<SerializableData>();
    }
    
    // Dictionary<Bounds, List<Matrix4x4>>对应的类就是SerializableDataList
```

将这个数据转化成映射类的类型，就可以通过数据文件的API进行转化了。

同理，当我们试图读取数据文件为代码中的数据类型时，需要先**反序列化**，即把数据文件加载为映射类后，将映射类转换为我们需要的数据类型。

```c#
// 字典转换为映射类
	static SerializableDataList DictionaryToJson(Dictionary<Bounds, List<Matrix4x4>> dictionary)
        {
            SerializableDataList serializableDataList = new SerializableDataList();
            foreach (KeyValuePair<Bounds,List<Matrix4x4>> entry in dictionary)
            {
                SerializableData tmpData = new SerializableData();
                tmpData.bounds = new SerializableBounds();
                tmpData.bounds.center = entry.Key.center;
                tmpData.bounds.size = entry.Key.size;
                tmpData.matrixLists = new List<SerializableMatrix4X4>();
                foreach (Matrix4x4 matrix in entry.Value)
                {
                    SerializableMatrix4X4 tmpMatrix = new SerializableMatrix4X4();
                    for (int i = 0; i < 16; i++)
                    {
                        tmpMatrix.values[i] = matrix[i];
                    }
                    tmpData.matrixLists.Add(tmpMatrix);
                }
                serializableDataList.data.Add(tmpData);
            }
            return serializableDataList;
        }
// 存储为JSON文件
static void SaveData<T>(string path, T data)
    {
        string currentPath = DuplicationOfNameRename(Application.dataPath + path, ".json");
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(currentPath, json);
        AssetDatabase.Refresh();
    }


// 映射类转换为字典
	static Dictionary<Bounds, List<Matrix4x4>> ToDictionary(SerializableDataList serializableDataList)
        {
            Dictionary<Bounds, List<Matrix4x4>> dictionary = new Dictionary<Bounds, List<Matrix4x4>>();
            for (int i = 0; i < serializableDataList.data.Count; i++)
            {
                Bounds bounds = serializableDataList.data[i].bounds.ToBounds();
                List<Matrix4x4> matrices = new List<Matrix4x4>();
                foreach (var serializableMatrix in serializableDataList.data[i].matrixLists)
                    matrices.Add(serializableMatrix.ToMatrix4X4());
                dictionary[bounds] = matrices;
            }
            return dictionary;
        }

// 读取JSON文件
	static Dictionary<Bounds, List<Matrix4x4>> LoadData(string path)
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                SerializableDataList serializableDataList = JsonUtility.FromJson<SerializableDataList>(json);
                Dictionary<Bounds, List<Matrix4x4>> data = ToDictionary(serializableDataList);
                return data;
            }
            return default;
        }
```












## 设计模式



### 单例模式



### 工厂模式

