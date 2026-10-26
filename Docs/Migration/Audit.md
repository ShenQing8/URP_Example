# Unity 草地项目迁移审计

审计日期：2026-10-27（Asia/Shanghai）。本文件保存已完成的只读静态审计，不是迁移完成证明。当前执行进度以 [Status.md](Status.md) 为准，实施与验收以 [Plan.md](Plan.md) 为准。

## 1. 范围与证据边界

| 项目 | 路径 | 实际版本 |
| --- | --- | --- |
| 源项目 | `E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res` | Unity 2019.4.15f1，URP/Core/Shader Graph 7.3.1 |
| 目标项目 | `E:\GithubRepositories\URP_Example` | Unity 6000.6.3f1，URP/Core/Shader Graph 17.6.0 |

版本证据来自 ProjectSettings/ProjectVersion.txt、Packages/manifest.json、packages-lock.json 和目标 Library/PackageCache 内官方 package.json。不能按“Unity 6”泛称选择旧版教程接口。重新接手时核对版本；包缓存哈希目录名只表示审计时快照。

审计覆盖源 Assets、Packages、ProjectSettings、README、LICENSE，并读取两边包缓存以解析包资源 GUID 和目标 API。已阅读全部自定义代码、Shader、材质、Prefab、场景、动画与导入设置，检查文本 GUID 和同文件 PPtr 引用；对二进制 LightingData 读取文件头及外部依赖表。

审计未启动 Unity，未执行导入、C#／Shader／Compute 编译、Play Mode、GPU 抓帧、构建或设备测试。静态可解析引用不证明 Unity 导入兼容；运行表现、性能和烘焙数据有效性均待验证。阶段 0 仅固化这些既有结论。

## 2. 项目结构与依赖

源 Assets 有 26 个资源文件和 33 个 `.meta`（含目录 meta），无缺失 meta、重复 GUID 或与目标 Assets 的 GUID 冲突。结构如下：

| 根目录以下范围 | 内容 |
| --- | --- |
| `Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core` | Renderer、位置生成、草地 Shader／材质／颜色 JPG、Compute，以及 GrassBending 子目录 |
| `Core/GrassBending` | 弯曲 RendererFeature、轨迹 Shader／材质、TrailRenderer Prefab |
| `InstancedIndirectGrass/TestScene` | scene.unity、两个旧管线资产、三个演示脚本、两个 Lit 材质、AnimatorController／AnimationClip |
| `TestScene/scene` | LightingData、颜色及方向光照贴图、反射 Cubemap |
| `Assets/URPMobileGrassInstancedIndirectDemo/SkyBox` | 自定义天空 Shader／材质 |

以上路径以源项目为基准，迁入目标时保留对应资源身份。资源统计为：6 C#、3 Shader、1 Compute、5 材质、3 asset、1 场景、1 Prefab、1 controller、1 anim、1 JPG、1 PNG、2 EXR。没有自定义 asmdef、Shader Graph、自定义 HLSL include、模型文件或 DLL。草叶 Mesh 由代码生成，其余主要使用 Unity 内置几何体。

源 manifest 显式声明 Burst 1.2.3、Mathematics 1.2.1、URP 7.3.1、UGUI 1.0.0 等。代码没有使用 Burst Job 或 Mathematics 运算，Unity.Collections／Unity.Jobs 仅有未使用的 using；CPU 剔除仍在主线程。无需搬运旧依赖版本，也不能把不存在的 Job 化宣称为保留功能。

目标已有 Input System 1.20.0 和现代 URP 模板设置。现有示例使用 IMGUI，没有 legacy Input 或 FindObjectOfType 调用，不把泛用 API 清单当成本项目实际修改项。若重建缺失统计组件，届时再审查新增输入逻辑。

## 3. 功能基线与数据链

```text
生成世界位置 -> Cell 分桶／排序／上传 -> CPU Cell 视锥剔除
-> 合并连续可见 Cell 的 dispatch -> GPU Compute 实例剔除
-> Append 可见实例 ID -> CopyCount 写入间接实例数 -> Indirect 草地绘制

四个循环动画角色 -> TrailRenderer -> 俯视 R8 弯曲图 -> 草地顶点采样／弯曲
```

| 文件 | 已有职责及必须保留的行为 |
| --- | --- |
| `Core/InstancedIndirectGrassPosDefine.cs` | ExecuteAlways；固定随机种子 123 生成位置，随数量保持密度；数量与绘制距离滑条；把真实位置列表交给 Renderer |
| `Core/InstancedIndirectGrassRenderer.cs` | ExecuteAlways；约 10×10 米 Cell；CPU 视锥粗剔除；连续 Cell 合批；Compute 调度；Append counter；间接参数和一次主颜色绘制；Cell／dispatch 统计及合批开关 |
| `Core/CullingCompute.compute` | CSMain，64×1×1；读取位置、裁剪空间及距离测试，向 Append buffer 写入可见 ID |
| `Core/InstancedIndirectGrass.shader` | 程序化三角草叶；随机宽高；相机朝向与远距离增宽；地面到草尖颜色；三组风；弯曲；随机法线、半 Lambert／透光、高光、环境 SH、主光／附加光、接收阴影、雾 |
| `Core/GrassBending/GrassBendingRTPrePass.cs` | AfterRenderingPrePasses；512×512 R8 白底；俯视绘制 LightMode=GrassBending 的轨迹；发布全局弯曲图 |
| `Core/GrassBending/GrassBendingRTPrePassTrailShader.shader` | 轨迹纹理乘顶点颜色，Blend Zero SrcColor，Cull Off，ZTest Always；只供弯曲图绘制 |
| `TestScene/MoveCamera.cs` | 在 8 个预设相机位置间平滑切换 |
| `TestScene/RotateLight.cs` | 主方向光旋转；主光／点光开关及转速滑条 |
| `TestScene/ApplicationFPS60.cs` | 设置 Application.targetFrameRate=60；不等于实际达到 60 FPS |
| `SkyBox/SkyBox.shader` | 天空颜色渐变，与雾颜色衔接 |

场景默认 100 万实例、绘制距离 125；GUI 数量范围 100 万至 1000 万，距离 25～200。位置脚本 Inspector 的 Range 上限为 4000 万，不等于已验证容量。原资源含四个角色和四个轨迹 Prefab 实例；动画约 7.57 秒循环。TrailRenderer 寿命 5 秒，颜色由黑到白使弯曲逐渐恢复；并非持久 RT 或 Compute 恢复模拟。

草地 Shader 的环境项为原作 SampleSH(0)，迁移应保留既有视觉公式并比较，不擅自改成另一套材质效果。源 Shader 仅有 UniversalForward Pass，没有实际 ShadowCaster、DepthOnly、DepthNormals 或 MotionVectors Pass；末尾注释不是实现。接收阴影是原功能，草叶投射阴影是可另行讨论的扩展。

源 README 的移动端高实例数和帧率是历史作者描述，不是目标 Unity／设备验收结果。保留源 MIT 署名与许可，不能把历史截图当作迁移后证据。

## 4. 资源引用与完整性

| 链路 | 静态结果 |
| --- | --- |
| scene -> Renderer／PosDefine -> 草地材质／Compute | 均可解析 |
| 草地材质 -> 自定义 Shader／颜色 JPG | 均可解析；颜色图 sRGB、无 mip；保留导入与纹理变换参数用于比较 |
| scene 的四个 Animator -> Player.controller -> PlayerMove.anim | 均可解析 |
| 四个轨迹实例 -> GrassBendingTrailRenderer.prefab -> 材质 -> 自定义轨迹 Shader | 均可解析 |
| 旧 URP Renderer -> GrassBendingRTPrePass 子资产 | 可解析 |
| 场景天空 -> SkyBox 材质 -> SkyBox Shader | 可解析 |
| Unity 内置 Mesh／内置资源、URP Lit／AssetVersion 脚本 | 区别于 Assets；在包或引擎内解析，不误判缺失 |

LightingData.asset 为 v21 二进制序列化文件，文件头记录 2019.4.3f1，包含 LightProbes 数据。四个外部 GUID 全部可解析：

| GUID | 资源 |
| --- | --- |
| b4bb10ac330c5c04680371d58f56a3a1 | scene.unity |
| fdb09265ee228f841837865b94f91ee4 | Lightmap-0_comp_light.exr |
| 2b3a04f0d3d535c41b3e191a9cf544a7 | Lightmap-0_comp_dir.png |
| 53ce169df7acf6c44bcb3a3369318d3b | ReflectionProbe-0.exr |

场景启用 baked lightmaps、禁用 realtime lightmaps。是否可沿用旧烘焙数据、是否需要新建 LightingSettings 和重新烘焙，必须由目标 Unity 实测决定。

确认缺失或悬空的引用：

| GUID／位置 | 性质与影响 |
| --- | --- |
| e4ff4051e75dc9443879b116bd65c930；scene.unity:1642 | Advanced FPS Counter 脚本缺失，配置仍在场景。恢复原实现或基于真实运行数据做等价统计，不能静默删除面板 |
| fb80074802966ad438ec681fae1be1c2；scene.unity:1727 | 统计面板字体缺失；完全原样恢复需原资源，替代字体的差异应记录 |
| 606abc37fcc31474699772fde405e2b4；SkyBox.mat:23 | 闲置 _MainTex 引用；现有 SkyBox Shader 未声明或读取此属性，不是天空效果依赖 |
| 463065d4f17d1d94d848aa127b94dd43、e7689051185d12f4298e1ebb2693a29f、e8537455c6c08bd4e8bf0be3707da685 | PresetManager 的三个默认 Preset 缺失；不把旧设置覆盖到目标 |
| a287be6c49135cd4f9b2b8666c39d999；TimelineSettings | 源未安装 Timeline 对应包；角色实际由 Animator 驱动，不依赖此设置 |

场景出现 20 次的材质 GUID `31321ba15b8f8eb4c954353edc038b1d` 是 URP 包内 Runtime/Materials/Lit.mat，两边均存在。此前聊天中将其列作疑似缺失，包扫描后已更正，不列为缺项。

## 5. 目标设置与视觉差异

| 项目 | 源 | 目标审计快照 |
| --- | --- | --- |
| Color Space | Linear | Linear |
| Renderer | 旧 Forward，含弯曲 Feature | PC m_RenderingMode=2 为 Forward+，含 SSAO；Mobile=0 为 Forward |
| HDR／Render Scale | 关闭／0.8 | 开启；PC=1、Mobile=0.8 |
| 阴影距离 | 200 | PC、Mobile 均为 50 |
| Depth／Opaque Texture | 关闭 | PC 开启，Mobile 关闭 |
| 默认场景 | 草地 scene | 模板 SampleScene |

目标 GraphicsSettings 的默认管线为空，但 QualitySettings 的 PC／Mobile 覆盖分别指向现代 RPAsset。迁移时检查有效链路与 fallback，不因默认字段为空就误判项目没有 URP。保留 PC Forward+、Mobile Forward 和目标模板功能，显式记录需要调整的画质参数；不通过关闭 SSAO、附加光或阴影掩盖适配缺失。

2026-10-27 阶段 2～4 核对修正：目标原始 Mobile Render Scale 为 0.8（阶段 1 交接 HEAD 的 `Assets/Settings/Mobile_RPAsset.asset`），PC 为 1；两者 HDR 均开启、MSAA 均为 1。此前表格将目标 Render Scale 统一写成 1 不准确。当前迁移保持这两个原始比例；实际叠加相机缩放对照见 Phase234.md。

## 6. API 迁移与官方依据

依据优先级：目标实际安装的 17.6 官方包源码与示例、6000.6 官方手册／API、历史版本资料。已核查的网页链接列在本节；重新实现时核对其版本边界。

| 旧实现 | 目标事实与替代 |
| --- | --- |
| ScriptableRenderPass.Configure／Execute、ConfigureTarget／ConfigureClear | 17.6 已无这些入口，原 override 直接迁入会阻断编译；重写 RecordRenderGraph、AddRasterRenderPass |
| GetTemporaryRT、ReleaseTemporaryRT、FrameCleanup | FrameCleanup 在 Deprecated.cs 仍保留转发，但不作为图内纹理生命周期；使用 TextureHandle／TextureDesc，由图管理 |
| context.DrawRenderers | 使用 RendererListParams、CreateRendererList、UseRendererList、DrawRendererList |
| cmd.SetGlobalTexture | 使用 SetGlobalTextureAfterPass，并确认消费者声明 UseGlobalTexture／UseAllGlobalTextures |
| 改写相机绘制矩阵 | RasterCommandBuffer.SetViewProjectionMatrices 仍存在；RenderingUtils 有 Raster 重载。处理 GPU 投影、逆矩阵、Y 翻转、状态恢复及必要全局状态声明；也可用专用 Shader VP 参数 |
| Graphics.DrawMeshInstancedIndirect | 6000.6 标记 obsolete，未删除；推荐 Graphics.RenderMeshIndirect，配 GraphicsBuffer、IndirectDrawIndexedArgs.size 和 RenderParams |
| uint[5] 参数／直接 SV_InstanceID 索引 | 不机械照搬布局；使用官方参数结构和 UnityIndirect.cginc、InitIndirectDrawArgs、GetIndirectInstanceID，保持真实可见 ID 链 |
| ComputeBuffer／Append／CopyCount | 仍受支持；GraphicsBuffer.CopyCount 支持混合 buffer 重载。准确核对 instanceCount 写入偏移，不因更换类型丢掉 GPU counter |
| 顶点内 GetAdditionalLightsCount＋普通 for | 17.6 clustered 路径计数函数返回 0；实现 Forward／Forward+ 对应光循环、InputData 和非主方向光处理 |
| _FORWARD_PLUS 教程写法 | 本目标使用 _CLUSTER_LIGHT_LOOP／USE_CLUSTER_LIGHT_LOOP；旧词自 6.1 deprecated |
| #if _MAIN_LIGHT_SHADOWS | 新单独 cascade 变体也必须处理；官方关键词组合含普通、级联和屏幕空间变体，后者需匹配坐标路径 |
| 两参数 GetAdditionalLight(i, positionWS) | 17.6 此重载不计算附加光阴影；使用匹配 shadowMask 的重载与实际阴影配置 |

本地关键官方源码，以 `E:\GithubRepositories\URP_Example\Library\PackageCache` 为根：

- `com.unity.render-pipelines.universal@327d93d1cbc8/Runtime/Passes/ScriptableRenderPass.cs`：RecordRenderGraph／OnCameraCleanup；旧 Configure／Execute 不存在。
- 同包 `Runtime/Settings/RenderGraphSettings.cs`：从 6000.4 起设置不再使用，enableRenderCompatibilityMode 恒 false。旧升级文档和默认日志残留的“打开兼容模式”提示不适用于本目标。
- 同包 `Samples~/URPRenderGraphSamples/Culling/CullingRenderPassRendererFeature.cs`：在 RecordRenderGraph 用 frameData.Get<CullContextData>().Cull，创建 RendererList。
- 同包 `Runtime/Passes/DrawObjectsPass.cs:258`：UseAllGlobalTextures(true)。实际草地消费者仍须验证，无需改包源码。
- 同包 `ShaderLibrary/RealtimeLights.hlsl`、`Shaders/Lit.shader`：附加光、阴影重载及关键词；`Runtime/UniversalRenderPipeline.cs` 定义管线 Shader 标签 UniversalPipeline。
- Core `com.unity.render-pipelines.core@02d75770e4b3/Runtime/CommandBuffers/RasterCommandBuffer.cs` 与 `Runtime/Utilities/CullContextData.cs`；后者命名空间为 UnityEngine.Rendering.Universal。

已查阅官方网页：

- [6000.6 RenderGraph](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/render-graph.html)
- [图内绘制对象](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/render-graph-draw-objects-in-a-pass.html)
- [创建纹理](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/render-graph-create-a-texture.html)
- [发布全局纹理及消费依赖](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/render-graph-create-global-texture.html)
- [旧 Indirect API 的废弃状态](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Graphics.DrawMeshInstancedIndirect.html)
- [RenderMeshIndirect 与 Shader ID 示例](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html)
- [IndirectDrawIndexedArgs](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/GraphicsBuffer.IndirectDrawIndexedArgs.html)
- [GraphicsBuffer.CopyCount](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/GraphicsBuffer.CopyCount.html)
- [Forward／Forward+ 附加光](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/use-built-in-shader-methods-additional-lights-fplus.html)
- [自定义 Shader 阴影](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/use-built-in-shader-methods-shadows.html)
- [自定义 cullingMatrix](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Rendering.ScriptableCullingParameters-cullingMatrix.html)
- [URP 自定义 Shader 转换结构参考](https://docs.unity3d.com/6000.6/Documentation/Manual/urp/urp-shaders/birp-urp-custom-shader-upgrade-guide.html)

## 7. 正确性与运行风险

| 编号 | 静态发现 | 实施与验证要求 |
| --- | --- | --- |
| R01 | Compute 按 ceil(jobLength/64) 派发，但没有本次区间／总数检查 | 超额线程可读下一 Cell、重复 append 或越界；同时校验 dispatch 长度和总容量 |
| R02 | Cell Bounds Y=0，Z 尺寸错误复用 X；整批 Bounds Y 固定 0 | 覆盖实际高度、风、弯曲、远距离增宽和非方形／移动区域，检查视锥边缘消失 |
| R03 | 空列表、单点或单轴分布可能产生零长 buffer／零 Cell 数及非法索引 | 定义空数据与退化区域行为；真实执行 0／1／63／64／65 等边界用例 |
| R04 | 单例、Camera.main、材质与 Compute 缺少保护；运行 Mesh 未配套销毁 | 核对初始化／禁用顺序与 ExecuteAlways；反复启停检查释放 |
| R05 | 两个位置缓存主要以数量判断；同数量换位置不重传 | 明确数据脏标记或更新契约，移动／缩放与数据刷新一致 |
| R06 | 每次 dispatch 从头累加之前 Cell 的数量 | 可预计算 Cell offset，保持合批结果不变；通过 Profiler 实测 CPU 影响 |
| R07 | 可见集使用 Main Camera；绘制未指定相机；MoveCamera 同在 LateUpdate | 明确最终相机矩阵、提交时序及 SceneView／反射／叠加行为，防止错误复用可见集 |
| R08 | 原草地光照在顶点，PC 为 Forward+ | 保留视觉公式；可保留 Forward 低成本路径并增加片元 clustered 路径，验证观感和性能 |
| R09 | 两个草地相关 Shader 管线标签为 UniversalRenderPipeline；目标值 UniversalPipeline | 按目标官方标签修正并在 Unity 验证支持性；不能仅检查文本编译 |
| R10 | StructuredBuffer 声明在 UnityPerMaterial CBUFFER；无显式能力 target | 将资源声明与常量布局分离，采用目标 URP HLSL／采样形式；逐后端验证，不把静态风险当作已观察的编译错误 |
| R11 | 原主光条件和附加光重载不匹配目标阴影语义 | 验证级联、软阴影、距离衰减和实际采样；不以禁用阴影通过 |
| R12 | 弯曲绘制仅替换矩阵，仍用主相机 cullResults | 俯视图可能缺失画外轨迹；使用匹配范围的独立 culling 参数与 RendererList |
| R13 | 俯视正交与恢复相机矩阵沿用旧写法 | 处理 GPU 投影、Y 翻转、反向 Z、正确恢复；检查后续天空／地面没有状态污染 |
| R14 | 全局纹理旧临时生命周期，多相机未隔离 | RenderGraph 显式依赖；确认无轨迹白底、Feature 禁用和多相机时不读失效纹理 |
| R15 | PC SSAO 开启，但原草地无深度／法线 Pass | 如补充 Pass，复用完整变形；弯曲图在首个消费者前产生，不能继续照搬 AfterRenderingPrePasses |
| R16 | 高数量数据存在 CPU 原始列表、分桶列表、排序数组及 GPU 位置／ID 多份存储 | 1000 万实例的 GPU 两大 buffer 约 160 MB，CPU 另有数百 MB；实测峰值和释放，不能承诺历史设备帧率 |
| R17 | 剔除用 CPU 逻辑投影，clip-space 测试有原作放宽阈值 | 明确透视／正交和投影契约；不能改为 GPU 投影却保留不匹配的 z 测试 |
| R18 | 旧烘焙数据和模板画质不同，FPS／字体资源缺失 | 恢复或等价实现并记录差异；不清空烘焙、移除统计或宣称原样恢复 |

## 8. 基线保护

目标初始提交为 `42c1fde2ef37cde1348cd587c8215494d99e9356`。阶段 0 开始时目标 main 工作区干净。

源仓库已有四项未提交改动：Core/InstancedIndirectGrass.mat、TestScene/GrassGround.mat、Packages/packages-lock.json、ProjectSettings/QualitySettings.asset（前两项位于演示目录下）。保留当前文件作为审计基线，不 reset、不“修复”源文件、不以源 HEAD 替代其工作区。

阶段 0 的授权只涵盖四份协作文档和目标本地迁移分支／提交。源与目标资源、包、ProjectSettings 不应因固化报告而改变；实际检查结果由 Status.md 记录。
