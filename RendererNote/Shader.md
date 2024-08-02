---
typora-copy-images-to: img
---

# Shader

## 渲染管线

渲染分为3个阶段 应用阶段 几何阶段 光栅化阶段 

 

应用阶段：由CPU处理。把视野外的物体剔除掉,然后设置要渲染的状态（材质纹理、纹理、Shader等），然后把图元（点、线、三角面）传递到几何阶段。

几何阶段：由GPU处理。处理传来的顶点和三角面。这个阶段重要任务就是把模型坐标转换到屏幕坐标。这一阶段会输出屏幕空间的二维顶点坐标、顶点的深度值、颜色等相关属性。

光栅化阶段：由GPU处理。图元所包含的像素进行处理。然后哪些像素要被输出到屏幕上。

 

顶点数据->顶点着色器->曲面细分->几何处理->裁剪->屏幕映射->三角形设置->三角形遍历->片元着色器->逐片元操作->打印屏幕图像

顶点着色器：可完全编程。最主要的是把模型空间的位置转换到裁剪空间。同时处理顶点颜色。

曲面细分：可完全编程。这个阶段细分图元用的。比如实现LOD效果，加顶点实现更细节的动画，用低模加细分在运行的时候实现高模效果。

几何着色器。是完全可编程的。在顶点着色器阶段我们不能得知顶点和顶点的关系，但是在几何着色器可以。几何着色器主要是添加或者减少图元。

空间变换：模型空间->世界空间->观察空间->裁剪空间->屏幕空间

 

HDR和SDR的区别

概念不同，HDR是指高动态范围图像，SDR是指标准动态范围图像；||亮度范围表现不同，HDR比SDR有更大更亮的数据存储；||图像细节表现不同，HDR技术可以在使图像在明暗之间获取更多的细节表现。

### 着色器

#### 顶点片元着色器

代码模板:
```
 Shader "Hidden/ShaderTemplate"
 {
   //Hidden 表示此Shader 无法在面板中找到 比如最常见的粉红色ErrorShader 
Properties
   {
     _MainTex ("Texture", 2D) = "white" {}
     _Color ("Color Tint",Color) = (1,1,1,1)
     _Range ("Range",Range(0,10)) = 5
     _Float ("Float",Float) = 4
     _Int ("Int",Int) = 10
     _Vector("Vector",Vector) = (1,2,3,4)
     _Cube ("Cube",Cube) = "white"{}
     _3D ("3D",3D) = "black"{}
   }
```

//Unity 会扫描所有SubShader语义块，选择第一个可以再目标平台运行的SubShader,假设都不支持，那么会调用Fallback

```   SubShader
   {
     //subShader中指定的状态会应用到所有pass
     //SubShader Tags 列表如下 标签设置与Pass中不一样，但语法一致


Tags{"Queue" = "Transparent" "RenderType" = "Opaque"} //多Tags 写法
     //Queue 控制渲染顺序，指定渲染队列
     //Queue 选项如下
     //Background 队列索引号 1000 最先渲染
     //Geometry  队列索引号 2000 默认渲染队列，不透明物体使用这个队列
     //AlphaTest 队列索引号 2450 需要透明度测试的使用这个队列 
     //Transparent队列索引号 3000 任何使用了透明度混合（例如关闭了深度写入的Shader）
     //Overlay  队列索引号 4000 可实现叠加效果 任何在需要在最后渲染的物体都需要使用该队列

//RenderType 对着色器进行分类 可以用于着色器替换功能
     //RenderType 选项如下
     //Opaque 渲染不透明物体时使用
     //Transparnet 渲染透明物体使用

//DisableBatching 指明是否对该SubShader使用批处理 true | false

//ForceNoShadowCasting 该SubShader 是否会投射阴影 true | false

//IgnoreProjector 是否忽略阴影 true | false 

//CanUseSpriteAtlas 当该SubShader 是用于Sprites 时， 将该标签设为false

//PreviewType 指明材质面板如何预览该材质 默认情况下，材质将显示为一个球形 Plane | SkyBox

//[RenderSetUp]
     //设置剔除模式 Back 背面 Front正面 Off关闭
     Cull Off 

//设置深度测试时 使用的函数
     //Less Greater | LEqual | GEqual | Equal | NotEqual | Always
     ZTest Less Greater

//深度写入 On | Off 打开 | 关闭
     ZWrite On

//混合 开启并设置
     Blend SrcAlpha OneMinusSrcAlpha
     //Blend 混合选项
     //Blend Off 关闭混合
     //Blend SrcFactor DstFactor 开启混合并设置混合因子 最终颜色 = 源颜色（该片元产生的颜色）* SrcFactor + 目标颜色（已经存在与颜色缓冲区的颜色）* DstFactor
     //Blend SrcFactor DstFactor,ScrFactorA DstFactorA 与上面一致，只是Alpha通道使用不同的因子来混合
     //BlendOp BlendOperation 混合操作命令

//混合因子
     //One    因子为1
     //Zero   因子为0
     //SrcColor 因子为源颜色值 混合RGB时 以源颜色RGB分量为因子 混合A时 以源颜色A分量为因子
     //SrcAlpha 因子为源颜色的Alpha值
     //DstColor 因子为目标颜色值
     //DstAlpha 因子为目标颜色值的Alpha值
     //OneMinusSrcColor 因子为 1-源颜色值
     //OneMinusSrcAlpha 因子为 1-源颜色值Alpha分量
     //OneMinusDstColor 因子为 1-目标颜色值
     //OneMinusDstAlpha 因子为 1-目标颜色值Alpha分量
     

//混合操作命令
     //Add    混合后 源颜色 + 目标颜色
     //Sub    混合后 源颜色 - 目标颜色
     //RevSub  混合后 目标颜色 - 源颜色
     //Min    RGBA分量 在源颜色值与目标颜色值中取最小值 （会忽略混合因子）
     //Max    RGBA分量 在源颜色值与目标颜色值中取最大值 （会忽略混合因子）

//每个Pass定义了一个完成的渲染流程 SubShader中的Pass 会按顺序全部执行
     Pass
     {
       Name "TemplatePass" //定义Pass名称 可以使用UsePass 直接使用其他UnityShader中的Pass
       UsePass "ShaderTemplate/TemplatePass" //使用其他UnityShader中的Pass

Tags{"LightMode" = "ForwordBase"}
       //Pass中的Tags 选项如下
       //LightMode 选项列表
       //ForwardBase  前向渲染 该Pass会计算环境光，平行光，逐顶点/SH光源 和 LightMaps
       //Always    不管使用哪种渲染路径，该Pass总是会被渲染，但不计算任何光照
       //ForwardAdd  前向渲染 该Pass会计算额外的逐像素光照，每个Pass对应一个光源
       //Deferred   用于延迟渲染 该Pass会渲染G缓冲
       //ShadowCaster 把物体的深度信息渲染到阴影映射纹理Shadowmap 或 一张深度纹理中
       //PrepassBase  遗留的延迟渲染 该Pass会渲染法线和高光反射的指数部分
       //PrepassFinal 遗留的延迟渲染 该Pass通过合并纹理，光照和自发光来渲染得到最后的颜色
       

       //RequireOptions 用于指定当满足某些条件时才渲染该Pass 目前只支持 SoftVegetation

CGPROGRAM // Cg/HLSL 代码段
       \#pragma vertex vert //编译指令指明顶点着色器代码
       \#pragma fragment frag //指明片元着色器代码
       
       \#include "UnityCG.cginc" //引入unity 内置文件

//Shader中使用属性 我们需要在CG代码中定义一个与属性名称和类型都匹配的变量
       sampler2D _MainTex;
       float4 _MainTex_ST;
       fixed4 _Color;
       half _Range;
       float _Float;
       int _Int;
       float4 _Vector;
       samplerCube _Cube;
       sampler3D _3D;

//浮点类型总结
       //float   32位高精度浮点
       //half   16位中精度浮点 范围[-6w,+6w] 精确到十进制后3.3位
       //fixed   11位低精度浮点 范围[-2,+2] 精度1/256 适用颜色和单位向量使用

struct a2v //应用阶段数据来源 Render -> UnityShader application to vertex
       {
         float4 vertex : POSITION;
         float2 uv : TEXCOORD0;
       };

//语义大全
       //POSITION 用模型空间的顶点坐标填充
       //TANGENT  用模型空间的切线方向填充
       //NORMAL  用模型空间的法线方向填充
       //TEXCOORD0 用模型空间的第一套纹理坐标来填充（一个材质可以有多张贴图）
       //TEXCOORD1 用模型空间的第二套纹理坐标来填充
       //TEXCOORD2 用模型空间的第三套纹理坐标来填充
       //TEXCOORD3 用模型空间的第四套纹理坐标来填充
       //COLOR0  用与储存颜色信息（可自定义）

//SV_ 表示系统数值语义 System-Value
       //SV_POSITION 顶点着色器的输出裁剪空间中的顶点坐标 顶点着色器最重要的事情就是把顶点坐标从模型空间转换到裁剪空间
       //SV_Target  接受用户输出颜色，输入到默认的帧缓存中

//此结构定义顶点着色器的输出 vertex to frag
       struct v2f 
       {
         float2 uv : TEXCOORD0;
         float4 vertex : SV_POSITION;
       };

//顶点着色器代码
       v2f vert (a2v v)
       {
         v2f o;
         o.vertex = UnityObjectToClipPos(v.vertex); //顶点的MVP 变换 模型-观察-投影矩阵
         o.uv = v.uv;
         return o;
       }
       
       
       //片元着色器代码
       fixed4 frag (v2f i) : SV_Target
       {
         fixed4 col = tex2D(_MainTex, i.uv); //通过UV坐标对主纹理进行采样
         // just invert the colors
         col = 1 - col;
         return col;
       }
       ENDCG
     }
   }

Fallback "VertexLit" //最后一条后路 SubShader全不支持 运行指定的Shader 可以关闭Fallback Off
 }
```



$$
定义：
q=(q_v,q_w )=iq_x+jq_y+kq_z+q_w=q_v+q_w
$$
