# 阶段 2～4：CPU／GPU、Shader 与 RenderGraph 联合迁移

日期：2026-10-27（Asia/Shanghai）。结论：**阶段 2～4 在 Windows Editor／DX12 联合验收完成；DX11 已资源编译，其他设备与完整 Player 验收仍待后续阶段。** 授权仅覆盖阶段 2、3、4 及本地提交，不包含阶段 5 或完整 Player 构建。目标 `E:\GithubRepositories\URP_Example`，只读源 `E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res`。

接手分支为 `codex/urp6-migration`，HEAD `8d23a576e5274bd6563036c1d82b82c2cf0aff24`，已核实为阶段 1 的 66 文件交接提交；接手目标工作区干净。源 master／HEAD `cd9f9be6296e21ec7c49b678c3d4cce8c7bc64cd` 及四项既有改动受到保护。

## 实现与契约

| 范围 | 改动及原因 |
| --- | --- |
| CPU Cell／位置 | 用真实三维位置 Bounds 分桶，处理 0／单点／单轴／非方形；预计算连续 Cell offset，取消逐 dispatch 从头累加。变形 Bounds 覆盖随机高度、风、弯曲、billboard 及远距离宽度 |
| 数据更新 | 保留世界坐标列表；增加 SetPositions／MarkPositionsDirty，支持同数量替换或原地编辑。位置生成器在数量或世界变换变化后刷新，保留种子 123 与数量对应密度，同时恢复 Unity Random 全局状态 |
| 相机与生命周期 | beginCameraRendering 使用最终相机矩阵，每相机独立 Append／args／MPB；RenderParams.camera 明确限定绘制。支持 Game／SceneView 的独立可见集，Overlay 复用已经绘出的 base 结果，Preview 不提交草地；层掩码关闭后不再消费缓存深度。禁用释放所有 GPU buffers 和程序 Mesh |
| Compute | 每个线程同时检查 JobLength、StartOffset 和 TotalCount；大任务按 65535 组拆分。用同一组归一化世界空间平面与保守草叶半径，距离为世界空间距离，透视／正交不再混用旧 clip-space z／w 假设 |
| Indirect | GraphicsBuffer.IndirectDrawIndexedArgs／RenderMeshIndirect；仅 GPU CopyCount 写 instanceCount，初始值为 0。6000.6 中公开成员是属性，实际结构体探测获取 instanceCount 字节偏移；真实 GPU 读回校验。Shader 使用 UnityIndirect.cginc、InitIndirectDrawArgs 和 GetIndirectInstanceID 后再取真实可见 ID |
| 光照／变形 | UniversalPipeline 标签、target 4.5、资源移出 UnityPerMaterial。GrassCommon.hlsl 统一颜色、深度与法线几何，保留随机宽高、billboard、距离增宽、三组风、弯曲及原半 Lambert／透光／高光／SampleSH(0)／雾公式。保留原每草叶 pivot 视向量与 BlendProbes 语义；没有新增草叶 ShadowCaster |
| Forward／Forward+ | 使用 _CLUSTER_LIGHT_LOOP；片元遍历 clustered 方向光和局部光，Forward 使用对应 per-object light loop。主光的普通／级联／屏幕坐标路径分开，附加光使用带 shadowMask 的真实阴影采样重载。光照移到片元，性能与旧顶点光照不同，未承诺历史帧率 |
| 弯曲生产 | RecordRenderGraph 创建 512×512 线性 R8_UNorm 白底，设备不支持 render／blend 时选线性 RGBA8。匹配实际位置区域的俯视 CullContextData.Cull／RendererList，保留根部上下 0.5m 交互带并覆盖实际根部 Y 范围，使用专用 GPU VP，不改相机矩阵。维持 Blend Zero SrcColor，通过 SetGlobalTextureAfterPass 发布；空 RendererList 仍发布白底 |
| 弯曲消费 | 生产早于 prepasses。PC 内置 prepass 未声明自定义全局纹理读取，因此草地使用补充的 RG 深度／法线绘制，UseGlobalTexture 与 ImportBuffer／UseBuffer 明确依赖；颜色由 URP Draw Opaque Objects 的 UseAllGlobalTextures 消费。补充 Pass 使用官方 RasterCommandBuffer 的 GraphicsBuffer Indirect 重载，不是旧 Configure／Execute 兼容路径 |
| Mobile 与 fallback | 不强制新增 Mobile 原本没有的深度／法线 prepass。每相机开始恢复不弯曲状态，Feature 禁用后不采样失效 RT；Overlay 不生产、提交或复用可变草地 buffers。PC／Mobile 现代 Renderer 的 Feature 排在 SSAO 前，原 SSAO 和各画质参数保留 |
| 编辑器验证入口 | 新增 Editor/GrassMigrationValidation*.cs；命令文件轮询复用现有会话，不启动第二编辑器。验证图内复制只在 UNITY_EDITOR 且显式订阅时记录；Player 无读回、持久 RT 或测试数据。Unity C# 编译失败时拒绝用旧程序集继续验证 |

没有修改 PackageCache，也没有 Unsafe Pass、旧 Configure／Execute、固定可见数或全量绘制替代 GPU 剔除。深度／法线仅补充活动 Renderer 请求的预通道；Mobile 保留原 Render Scale 0.8、MSAA 1，PC 保留 Render Scale 1、Forward+ 和 SSAO。

## 真实执行环境与方法

- Unity `6000.6.3f1 (45d8eee7de74)`；URP／Core／Shader Graph `17.6.0`。ProjectVersion、manifest、lock 和安装的包源码／package.json 均核对。
- Windows Editor／Direct3D 12／NVIDIA GeForce RTX 4060 Laptop GPU；复用目标 PID 46016。开始为 SampleScene，测试临时打开演示场景并进入 Play；源项目没有打开。
- 首次 Refresh 通过核实前台窗口后的 Ctrl+R，之后由已编译 Editor 脚本调用 AssetDatabase.Refresh。测试文件请求、结果和原始图像位于 `Logs/MigrationPhase234-20261027`。
- GPU 测试在 Unity 内实际 Dispatch，复制独立 Append counter，读回官方 args 结构及完整可见 ID；检查范围、唯一性、数量、合批集合，并与上传后位置的独立 CPU 参考计算逐项比较。参考数据只用于验证，不参与生产绘制计数。
- 图像对照通过真实 URP RenderRequest 完成，场景状态在 finally 中恢复。临时点光位置／阴影遮挡体、黑色 albedo 或半灰色混合平面均是明确的测试输入；不是运行时假数据或产品替代资源。

## 验证结果

| 检查 | 实际结果与证据 |
| --- | --- |
| Unity C# | 最终已由 Unity 编译并执行最新验证程序集；中间 ref/in、Marshal 字段假设和测试 clearDepth API 错误已修复，历史保留。不是外部 C# 编译代替 |
| 边界与 ID | 0／1／63／64／65／127／128／129／5003 通过；空数据没有零长 GPU 分配。ID 全部唯一且在范围内，Append counter=indirect instanceCount；同数量刷新、单点／X／Z／非方形／平移与三次禁用重启通过。见 GPU 证据 |
| 合批与参考集 | 5003 混合位置，透视 856、正交 1117；合批 10 dispatch 对不合批 85／95 dispatch 的可见 ID 集合完全相同。透视与正交每 45° 旋转均与 CPU 参考集相同 |
| 1000 万容量 | 实际位置 buffer 10M×12、Append 10M×4、args 1×20，GPU 剔除后 72048，260 Cell／17 dispatch，释放通过。生成／构建／剔除／同步读回总墙钟 3651ms；managed 从 54,878,208 到 295,755,776 字节，Unity reserved 1,855,533,056。它们是采样值与聚合耗时，不是 GPU 帧时、驱动峰值或 FPS |
| 演示 100 万 | 实际场景与资源包新相机均读回 76842 个可见 ID，counter 与 args 一致。主颜色 Indirect、角色与草地真实绘出 |
| 光照和动画 | PC Forward+、实际 Mobile Forward 均通过主光／点光开关、主光阴影、三组风分别归零、颜色纹理、随机草形参数、雾、非主方向光及 SH(0) 间接项对照。PC 的临时点光遮挡体产生明确草地阴影差异。Mobile 原附加光阴影关闭配置保留 |
| SH／探针边界 | 保留原 API 默认 BlendProbes。直接光全关时，黑色 albedo 对照证明 SH(0) 实际贡献；改 ambientProbe 对已烘焙探针的结果不强行要求变化。旧 LightingData、LightProbes／GI 外观有效性仍属于阶段 5 |
| 轨迹图 | 四个原 TrailRenderer 单独均有非白像素，其最新点世界坐标映射位置均读到轨迹；无轨迹全白。反向第二 Game 相机的 R8 像素与正向完全相同，buffers 对象不同 |
| 重叠与恢复 | 用真实轨迹 Shader 的半灰色平面隔离混合：单层 0.49804，双层 0.24706，平方 0.24804，差异在 R8 量化内。原四轨迹停止发射后最终真实经过 5.204 秒（此前一轮 5.203 秒亦通过），R8 恢复全白；恢复原 enabled／emitting 状态 |
| 相机操作 | 源 8 个保存位置逐项实际渲染和 GPU／参考集检查；空 Overlay 不建立可变草地 buffers 或草地图 Pass，同一栈的 Feature on/off 产生真实弯曲响应；草地层掩码为 0 时不消费先前缓存的可见集／深度 |
| Mobile 空 Overlay 对照 | 原比例 0.8 下存在边缘采样差异；禁用草地仍复现，原生场景 RGB 平均差 0.00329556；临时比例 1 下差异为 0。记录为原生缩放栈路径的采样差异，不用逐像素相等冒充草地契约，也不修改交付的 0.8 配置。同栈弯曲开关差异 0.05914989，隔离检查通过 |
| RenderGraph | 读取与 Viewer 相同的真实 DebugData；主相机顺序为弯曲生产、原生 prepass、草地补充深度／法线、SSAO、Opaque，明确纹理与三个 buffer 的读依赖。Mobile 无额外法线预通道。R8 与法线经显式图内复制读回，颜色／法线轮廓已目视检查 |
| Shader 资源构建 | BuildAssetBundles StrictMode／StandaloneWindows64，真实编译 DX12 与 DX11 的草地、轨迹、天空和 Compute；构建后保留所需变体。产物 82,750 字节，SHA-256 `3bb69e29212776f598b195da7bf2cf19077acf27d231fb9016ef8b1e33384b26`。仅资源构建，不是阶段 6 Player 构建 |
| 构建资源实际加载 | 新相机使用资源包加载的草地材质／Shader、Compute、轨迹材质及天空 Shader，实际 GPU 计数、Forward+ 光照／阴影、环境项、风、弯曲与法线对照通过；防止只验证 Editor 缓存中的同名 Shader |

颜色／法线共享所有变形；截图是实际执行产物，不宣称与 Unity 2019 原版或两个已升级 Lit 材质逐像素一致。Forward+／Forward 的片元光照成本、移动设备表现与完整帧性能仍待阶段 6 测量。

## 失败、纠正与未验证

- 保留实际中间失败：原 CS0115；新 API 的 in 参数／C# 9 不接受 ref；6000.6 args 属性不能 Marshal.OffsetOf；验证 Overlay clearDepth 为只读导致 CS0200。均修正，并在编译成功后重跑。编译失败期间旧程序集得出的加载测试失败不计作通过。
- 最早同回调 QualitySettings 索引切换不能证明实际管线切换；Windows 运行时质量列表只有 PC。后续按实际 currentRenderPipeline 校验，在 Play 内临时覆盖当前管线、等待真实帧后测试 Mobile；最终恢复 PC。初次误标 Mobile 的图片／结论作废。
- 克隆 TrailRenderer 的临时重叠探针未获得可用绘制结果，不能据其宣称乘法成功。最终将混合隔离为真实轨迹 Shader 的受控平面输入，原四条真实 TrailRenderer 的投影与 5 秒寿命另行独立验证。
- Frame Debugger 原生接口尝试没有捕获事件，未作为成功证据；改用真实 R8／法线 GPU 复制和 RenderGraph Viewer DebugData，满足图内容／顺序的等价实际检查。URP 17.6 主动禁止 RenderRequest 生成 Viewer 数据，因此在后续普通 Game 相机帧取图；没有改包放开限制。
- DX11 已资源编译，未切换设备实际运行；Android／Vulkan／Metal／XR／实时反射相机未验证。R8 不支持时的 RGBA8 分支未在该设备强制冒充测试成功。
- 完整 Player 构建／启动、长期泄漏／驱动内存峰值、Profiler CPU／GPU 帧时和设备性能仍属阶段 6。缺失 FPS／字体、旧 Lit 升级差异、天空闲置缺项、GI／烘焙有效性仍属阶段 5；没有删除它们来减少错误。

## 证据与复跑

完整本地证据目录：`E:\GithubRepositories\URP_Example\Logs\MigrationPhase234-20261027`。已提交的小体积证据：

- [Phase234-GPU.txt](Evidence/Phase234-GPU.txt)：边界、ID／counter／CPU 参考集与 10M 容量原始输出。
- [Phase234-Interaction.txt](Evidence/Phase234-Interaction.txt)：原四轨迹位置、无轨迹白底、画外／多相机、乘法、垂直交互带与最终 5.204 秒恢复。
- [Phase234-Cameras.txt](Evidence/Phase234-Cameras.txt)：8 个源相机位置、Overlay 隔离、原生 0.8／1 缩放对照与层掩码缓存检查。
- [Phase234-Rendering.txt](Evidence/Phase234-Rendering.txt)：实际 Mobile Forward、最终 Windows 构建资源加载后的 Forward+／光照／风／弯曲输出。
- [Phase234-Build.txt](Evidence/Phase234-Build.txt)、[Phase234-ShaderCompilation.txt](Evidence/Phase234-ShaderCompilation.txt)：真实资源构建结果、DX11／DX12 变体筛选和编译日志。
- [Phase234-RenderGraph.json](Evidence/Phase234-RenderGraph.json)：两套管线最终普通 Main Camera 帧的真实 Viewer DebugData 摘要、原文件 SHA-256、512×512 R8（format=5）描述和读写依赖。
- [Phase234-EditorFinal.txt](Evidence/Phase234-EditorFinal.txt)：退出 Play、恢复 SampleScene、最终 Shader 支持性与无错误检查。
- [Phase234-Checks.json](Evidence/Phase234-Checks.json)：版本、源保护、GUID／meta、材质数值、验证原文件哈希与最终范围检查。

大日志、资源包与截图保留在上述忽略目录，不默认加入 Git。

复跑时先核对目标编辑器占用，禁止并发打开同一项目。现有会话已编译脚本后，在上述目录写入 `request.json`，例如 `{"id":"gpu-rerun","command":"gpu"}`；编辑器 update 将执行并生成同 ID txt。实际构建用 `shaderbundle`（需退出 Play），加载验证用 `usebundle`（需演示 Play、PC 管线）。`quality` 的 quality=0／1 表示测试 Mobile／PC 资产覆盖，不是平台过滤后的 QualitySettings 运行索引；等待一真实帧再运行 render／appearance。interaction 的异步结果必须同时检查 overlap 和 recovery 文件；仅主命令 PASS 不等于恢复测试完成。

官方接口依据以已安装 17.6 包和内置 UnityIndirect.cginc 为准，关联网页见 Audit.md。额外核对 [RenderParams.lightProbeUsage](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/RenderParams-lightProbeUsage.html) 与 [原 DrawMeshInstancedIndirect 默认参数](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Graphics.DrawMeshInstancedIndirect.html)，因此显式保留 BlendProbes；没有以关闭探针替代未知的阶段 5 GI 验收。

## 最终保护、序列化与交接

源 88 文件逐项与阶段 1 前快照一致，master／HEAD／四项未提交改动一致。迁入的 33 个原 meta 原字节保留；目标 64 个 Assets GUID 无重复，新 GUID 与包无冲突，无缺失 meta。目标最初 70 个受保护文件中，68 个原字节不变，只有获批的 PC／Mobile Renderer 因 Feature 接线及官方序列化升级改变；Packages／ProjectSettings 均原字节保持。

Unity 允许并记录的序列化差异：两个旧管线资产由官方升级器升级，仍不是有效目标管线；自定义草地材质 serializedVersion 6→8，但全部保存的 Shader 值与接手 HEAD 逐项一致。现代 Renderer 的旧序列化字段由 17.6 迁至资源设置，PC SSAO 设置和 Forward+／Mobile Forward 模式保留，实际图中也有 SSAO。资源构建生成的 PC_RPAsset prefilter／默认字段没有作为画质修改提交，恢复了准确初始字节（包含原换行格式）；下次构建仍由官方预处理器计算。

Unity PID 46016 保持运行，已退出 Play 并恢复 `Assets/Scenes/SampleScene.unity`。没有保存演示场景的测试修改。最终本地提交以包含本报告与 Status.md 的提交为准；SHA 在交接答复给出。阶段 5 仍未获批，不自动恢复 FPS／字体、改 Lit 外观或重烘焙。

提交的编译节选仅规范化行尾空白；原始字节仍保留在本地 Editor-phase234.log。暂存清单精确核对为 40 个本轮文件，未含 Logs／Library 或未授权阶段资源；最终空白与文档链接检查通过。
