# 阶段 6：集成、构建与运行验收

日期：2026-10-27（Asia/Shanghai）。**阶段 6 在本机 Windows x64／Mono Player、DX12／DX11 的已执行范围完成。** 其他设备、更长耐久及未归因的引擎退出分配摘要保持限制；当前授权／交接见 [Status.md](Status.md)。本轮仅本地提交，不推送。

## 接手、授权与保护

- 目标实际根 `E:\GithubRepositories\URP_Example`；origin 为 `https://github.com/ShenQing8/URP_Example.git`；main 跟踪 origin/main。接手 HEAD／阶段 5 提交 `707212f087763d0c55704166fbe1c870605b1f58`，远程 main／默认 HEAD 仍为 `8b79517a4128c21ade3b42f4fb5af4cf3ed267c3`。没有切换历史迁移分支或推送。
- 接手唯一未提交改动为 Status 的阶段 5 复核记录，本轮保留全文。用户已批准阶段 6 范围内实现、修复、验证及本地提交，要求提交 Summary 用中文。
- 源目录 `E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res` 保持只读；源代码、资源及 Git 仓库不参与本轮写入。
- Unity `6000.6.3f1 (45d8eee7de74)`，URP／Core／Shader Graph `17.6.0`，Input System `1.20.0`；从 ProjectVersion、manifest、lock、安装包重新核对。
- 复用目标 Editor PID 32380，不启动同项目第二编辑器；源 Unity 2019 项目保持关闭、只读。源 88 文件与阶段 1 清单一致，源 Git 四项既有改动保留。
- 本机完整证据在 `Logs/MigrationPhase6-20261027`，构建在 `Builds/Phase6`；两者均由 Git 忽略。

## 验证方法与范围

1. 先构建普通 Windows x64 Release Player，以真实 DX12 设备启动，检查默认首场景、草地、字体、UI 和退出。
2. 再构建 Development Player，通过仅该构建包含、且必须显式传入 `-grassValidation <绝对输出目录>` 才启用的辅助采样。普通启动销毁辅助对象，Release 不含辅助类型或测试场景对象；不改演示产品界面。
3. 功能测试读回真实 GPU Append counter、Indirect 参数、完整可见 ID，并与独立 CPU 参考集比较；测试数据只进入临时测试 Renderer，不替代生产计数。性能窗口不执行同步 GPU 读回。
4. Development 构建通过官方 `IncludeAdditionalRPAssets` 接口临时纳入 Windows 质量列表排除的 Mobile 管线，用于正确保留 Forward 变体；构建后恢复。此诊断配置与普通 Release 的 PC 交付配置区分记录。
5. 使用 ProfilerRecorder、FrameTimingManager 和短时二进制 Profiler 捕获记录 CPU／GPU 时间；另采集 Unity allocated／reserved／graphics estimate 以及操作系统进程／GPU 内存。Main Thread 可能包含等待，GPU estimate 不等于专用显存实际占用；记录采样峰值的频率及边界。
6. 反复禁用检查旧 GPU buffer `IsValid=False` 和旧 Mesh 被销毁，再启用读回实际可见数；场景往返检查释放；持续运行记录内存及 RT／程序 Mesh 数量，不能据有限时长证明永不泄漏。

保留此前 Mobile 0.8 空 Overlay 的采样边界、局部 BlendProbes 和两个 Lit 官方升级；不重烘焙旧 GI，不以 Windows Mobile 配置代表 Android 设备。

## 已取得的构建与功能证据

| 范围 | 实际结果 |
| --- | --- |
| Windows x64 Release | 首次 BuildReport Succeeded，106,456,473 bytes，6:08.893；最终构建 106,458,857 bytes，27.147 秒；均 0 error／0 warning，草地首场景和 SampleScene 均包含 |
| Development 诊断构建 | 最终显式门控构建 173,541,658 bytes，22.680 秒，0 error／0 warning；额外管线包含和帧计时仅用于此构建 |
| 普通 Development | 173,499,659 bytes，40.248 秒，0 error／0 warning；没有诊断定义、组件或复制钩子 |
| 普通 Player 画面／鼠标 | DX12、960×600；草地、角色、天空、字体、统计与完整 UI 实际绘出。真实鼠标使相机移动、主光／点光状态改变、合批 dispatch 数变化、距离变为 200；截图和 OS 输入记录留证 |
| GPU 数据链 | DX12 Player，PC／Mobile 各 8 视角完整可见 ID 与独立 CPU 参考集一致；Append counter=Indirect instanceCount，ID 唯一且在范围内。默认视角为 76,842；合批 24 dispatch／不合批 258，集合一致 |
| 数量／距离 | 真实 100 万→300 万→1000 万→100 万；相应可见数 76,842／79,840／86,209／76,842。距离 25／200／125 分别 8,935／168,587／76,842。数量增加保持原密度并扩大场域，不能说 1000 万全部可见 |
| GPU 尾线程 | 临时 Renderer 的 0／1／63／64／65／127／128／129／5003 实际通过；空数据无 GPU 分配，非空 ID 集与 CPU 参考一致；未写生产假计数 |
| 弯曲与深度 | 图内显式读取真实 512×512 R8_UNorm；PC／Mobile 的四条原始轨迹逐条均有非白像素；PC 补充深度／法线消费者存在，Mobile 无额外消费者 |
| 恢复 | 原轨迹停发后真实 5.351 秒，图内复制恢复全白；恢复 emitting 后继续运行 |
| Player 资源集成 | 首场景正确；活动对象组件无 null；默认 Volume 19 个活动组件有效；字体 LegacyRuntime 可用；18 个 Renderer 使用 1 组光照贴图、8 个 SH 探针系数有限、默认反射 ReflectionProbe-0 实际加载；三个必需 Shader supported=True；四个 Animator 实际移动 |

功能最终报告为 `DX12Final/06-functional.txt`，`UnityErrorsDuringCommand=0`、`COMMAND PASS`。这些是本轮 Player 执行结果；旧 Editor 验收继续仅覆盖其原记录范围。

## 中间失败与分类

- 初次启动发生在最终产物拷贝结束前，Player 立即退出，仅留下两行 Mono 路径。保留 `premature-launch-not-counted.log`，未计入有效启动／运行验收；随后在 BuildReport 成功后重启最终产物。
- 新辅助最初引用 URP 内部 GlobalSettings 类型和 Add／Remove，触发 CS0122；改为公开 `EditorGraphicsSettings.PopulateRenderPipelineGraphicsSettings` 和 `IncludeAdditionalRPAssets`。新增条件中的短路 out 变量触发 CS0165，修正后由 Unity 重新编译、重建，没有用旧程序集结果冒充新脚本通过。改用显式 `GRASS_PLAYER_VALIDATION` 构建定义，避免已弃用 DEVELOPMENT_BUILD 的 UAC0009。
- 首轮 Player 功能检查在 CPU `Shader.GetGlobalTexture`／`GetGlobalFloat` 上报“Bending texture absent”。RenderGraph 纹理和 CommandBuffer 全局状态不应据此作 CPU 持久资源断言；改为复用已验证的图内声明读取／复制后重跑。生产 Pass 的执行逻辑未改，仅把既有可选观察钩子扩展到显式诊断构建，Release 不含该钩子。
- 普通 Player 首次初始化输出四条景深／Panini Shader stripped 信息。两次启动稳定复现；URP 17.6 的三个 Pass 构造器明确以 `LogType.Log` 调用 LoadShader，官方 ShaderBuildPreprocessor 按 `IsActive` 剥离。最终 Player 实测 DOF=Off／IsActive=False、Panini distance=0／IsActive=False。因此这是未用效果的官方信息，不是编译错误；保留日志、默认 Volume 和剥离设置，没有屏蔽 Console、改包或删除效果。
- DX12 从 100 万提高容量时记录 upload buffer 扩容信息（16 MiB→至少 36 MB→至少 120 MB）；GPU 数据检查和后续运行通过。该信息原样保留，不作 Shader／Compute 编译错误隐去。
- 最初 OS 反引号输入未得到隐藏后的画面，不能据文件命名宣称快捷键已通过；该项须以后续状态读回／截图为准。

## Player Profiler 方法和已有测量

Windows x64／Mono／RTX 4060 Laptop／DX12，1280×720，targetFrameRate=60、VSync=0，默认相机位、实际角色／风／光照动画继续运行。PC 为 Forward+／scale 1／保留 SSAO，Mobile 为 Forward／scale 0.8。计时前预热 5 秒；每组最初约 10 秒同时保存二进制 Profiler。以下帧时统计剔除每组前 15 秒并按 FrameTiming 的时间戳去重，避免把记录开关的初始扰动作为稳定样本。所有样本来自 Player，非 Editor 统计面板。

Cell 一列单独来自最初约 10 秒的二进制 Profiler 捕获，与后续稳定帧时并非同一采样窗口；DX11 对照也使用相同方法。

| 条件 | 稳定样本 | CPU 总帧周期中位数 ms | CPU 主线程工作中位数 ms | CPU 渲染线程中位数 ms | GPU 中位数 ms | CPU Cell 剔除中位数 ms |
| --- | --- | --- | --- | --- | --- | --- |
| PC／100 万 | 2699 | 16.6670 | 1.9520 | 0.6894 | 0.74957 | 0.1344 |
| Mobile／100 万 | 2700 | 16.6670 | 1.7406 | 0.53725 | 0.25702 | 0.1371 |
| PC／1000 万 | 1800 | 16.6669 | 2.9164 | 0.6842 | 0.75776 | 1.1247 |
| DX11 PC／100 万 | 1800 | 16.66705 | 2.04555 | 0.56215 | 0.63898 | 0.1456 |

总帧周期包含等待，不等于 CPU 运算成本。主线程与渲染线程可能重叠，不能相加后当作总时长；目标 60 FPS 及本机样本不构成其他设备性能承诺。1000 万位置维持原密度、扩大场域，默认视角实际可见约 8.6 万，因此 GPU 帧时接近 100 万场景并不表示绘出了全部 1000 万。

ProfilerRecorder 的 Main Thread 和 GPU counter 有效；直接以名称创建的 Render Thread／Grass CPU Cell Culling recorder 无效，CSV 的对应列为 0，**不把 0 当作成本为零**。渲染线程采用真实 FrameTimingManager 数据，Cell 剔除则使用 Unity 导入二进制 Player Profiler 后的 RawFrameDataView 样本。PC／Mobile／1000 万的捕获分别含 601／602／602 帧，Cell 实际样本 600／601／601；含等待的 PlayerLoop 和独立 RenderLoop 也保留在分析输出。完整 CSV、P95／max、逐帧内存和短时原始捕获在本地证据目录；摘要将随小证据提交。

官方方法参考：[Frame timing ProfilerRecorder](https://docs.unity.com/en-us/engine/6000.5/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/record-timing-data)；实际属性及单位以本机 6000.6 API XML 和运行输出核对。RawFrameDataView 导入的是已保存的 Player 捕获，不是测量 Editor 本身的 CPU 用时。

## OS 输入与集成保护补充

最终真实输入记录为 `DX12Final/10-ui-before.txt`／`10-ui-hidden.txt`／`10-ui-operated.txt`／`10-ui-restored.txt`。使用物理扫描码 0x29 后，StatsVisible 从 True→False→True；鼠标实际将 view 改为 1、主光关／点光开、转速 0、数量 300 万、距离 200、合批 False，读回 170,730 个真实可见实例；随后恢复 100 万／125／合批 True，7 次相机按钮回到 view=0。滑条点击恢复转速读为 44，之后场景往返从未改动的场景恢复原 45；未保存运行时状态。

已复核普通 Release 的 Assembly-CSharp 不含 GrassPlayerValidation 或 ValidationRecord，诊断 Development 含两者。场景处理器另以本次构建的显式 IsValidationBuild 门控，防止普通 Development Build 注入诊断组件；此项使用普通 Development 构建单独验收。

## 资源释放、持续运行与内存边界

DX12 Player：20 次组件禁用／重新启用，保存旧 positions／visible／args 引用并断言 `IsValid=False`，旧 Mesh 销毁、当前字段归零；每次重启实际读回 76,842 可见实例。另做 5 次草地→SampleScene→草地往返，卸载时单例为空、旧 buffer 失效、旧 Mesh 不存在、程序草地 Mesh 数量为 0，重新加载后恢复非零绘制。

之后连续运行 **600.038 秒**。每 30 秒记录内存，并执行稀疏 GPU 一致性检查；这段运行用于稳定性，未并入无读回的性能窗口。程序 Mesh 始终为 1；图池预热时 RT=35，随后降为 14 并保持。Unity reserved=906,493,952 bytes（864.5 MiB），allocated 最终 220,172,034 bytes（约 210 MiB）；前段缓存变化约 2 MiB，后段保持平台，没有观察到随时间持续增长。退出前再次图内复制得到 2737 个非白像素，PC 深度／法线消费者存在，命令新增 Unity Error／Exception／Assert=0。

RenderGraph 的临时纹理由图池管理，卸载演示不要求引擎销毁所有池内 RT；因此不把 RT 对象数量暂时不为零当作草地持有泄漏，也不宣称所有引擎 RT 都立即归零。主动创建的验收复制 RT／RTHandle 在 finally 释放，并在性能／持续运行时没有订阅复制钩子。

外部监控名义每 2 秒采一次该 Player PID 的 WorkingSet／PrivateBytes 和 WDDM GPU Process Memory。DX12 整段运行的已采样 private peak 约 2010.7 MiB；Windows 记录的 PeakWorkingSet 约 1343.1 MiB；已采样专用 GPU 内存约 369.8 MiB、共享约 356.4 MiB。采样不能保证捕捉每个驱动瞬态；PeakWorkingSet 是 OS 的该进程高水位，不能将虚拟地址预留当作真实 RAM 用量。Unity graphics estimate 另行记录，不替代 WDDM 或实际 VRAM 数据。

退出原始日志含 Unity `MemoryLeaks`／Immediate 摘要：577,298 bytes，其中 Manager 534,039 bytes，另有 Thread／Default／NativeArray 等引擎标签。该摘要缺乏可归因到草地的调用栈，保留原文和比较数据；上述 buffer／Mesh 失效断言成立，但**不据此宣称整个引擎进程零残留或所有潜在泄漏已排除**。有限的 10 分钟测试不能代替小时／全天、不同驱动和设备的耐久验证。

## DX11 实际运行

同一 RTX 4060 Laptop 上实际 `-force-d3d11`，设备日志为 Direct3D 11.0／feature level 11.1。最终显式诊断构建门控实际生效；PC／Mobile 各 8 视角、GPU 参考集、数量／距离／合批、0～5003 尾线程、四角色／单条轨迹、深度法线和 R8 图通过，恢复实际 5.337 秒，新增 Unity 错误=0。此项已从“仅资源编译”升级为真实 Player 执行；10 分钟持续运行的结论仍限 DX12。

DX11 另完成 45.007 秒 Profiler 采样、602 帧二进制捕获、5 次禁用重启、5 次场景往返和 20.040 秒短期运行，旧 buffer／Mesh 失效与恢复通过，最终 Mesh=1／RT=14、真实弯曲图非白 2780 像素；新增 Unity 错误=0。

## 最终普通产物、保护与交接

最终 Release（DX12／DX11）和普通 Development（DX12）均串行启动、读取首场景日志、真实捕获画面后关闭，exitCode=0，必需草地／天空／角色／字体／统计可见，无缺脚本或 Shader 错误。三次故意传入 `-grassValidation`，均没有生成 ready 文件；程序集也不含该辅助类型和图复制钩子，验证普通构建不会受验收机制影响。

普通 Development 退出也有 Immediate 摘要 571,048 bytes；Manager=534,039、NativeArray=3544 与诊断构建相同，说明两者存在共同基线，但不能由总量差异推出每一笔分配的所有权。完整原始摘要保留在 Checks；不修改包、删除默认资源或假报零残留。

现代 PC／Mobile RPAsset、Renderer、GlobalSettings、Graphics／ProjectSettings、包、两个 Lit、场景和烘焙资源均恢复／保持接手字节。构建生成的 prefilter、runtime list、默认序列化字段、Input System preload、测试包临时 Resources 已核对并归档生成副本；不把它们当作画质改动提交。源 88 文件／master／HEAD／四项改动、33 原 meta 均保持；目标 70 GUID 无重复／缺 meta，与 9678 份包 meta 无冲突。

新增四个脚本及 meta：`GrassPlayerBuildValidation`（显式构建与恢复临时设置）、`GrassPlayerValidationSceneProcessor`（仅本次显式诊断构建注入）、`GrassPlayerValidation`（命令行启用的功能／计时／释放检查）、`GrassProfilerAnalysis`（离线读取真实 Player Profiler）。既有核心代码仅扩展可选观察钩子的编译条件；未改变 C#／Compute／颜色／深度／法线共享数据契约，也未改运行画质或旧 GI。

结束时全部 Player 已退出；目标 Editor PID 32380 为 Edit／`Assets/Scenes/SampleScene.unity`／dirty=False／PC／compilationFailed=False，没有源 Editor 或第二项目会话。

## 证据与复跑

本地完整目录 `E:\GithubRepositories\URP_Example\Logs\MigrationPhase6-20261027`。`DX12Final`／`DX11` 保存功能、图像、CSV、短时 Profiler、进程监控及长期／短期记录；`Release-d3d12`／`Release-d3d11`／`PlainDevelopment-d3d12` 保存最终普通启动日志和画面。`Editor-phase6.log` 保留真实中间失败和最终编译／构建日志。构建与原始证据不入 Git。

随仓库提交的小证据：

- [Phase6-Build.txt](Evidence/Phase6-Build.txt)：真实构建报告及被拒绝的中间构建请求。
- [Phase6-Player.txt](Evidence/Phase6-Player.txt)：两 API 的完整功能、OS 操作、普通产物启动及最终 Editor 状态。
- [Phase6-Profiler.txt](Evidence/Phase6-Profiler.txt)、[Phase6-Performance.json](Evidence/Phase6-Performance.json)：四组计时、离线真实捕获分析、P95／max 与采样内存峰值。
- [Phase6-Stability.txt](Evidence/Phase6-Stability.txt)：释放、场景往返、DX12 600 秒／DX11 短期运行原始输出。
- [Phase6-Checks.json](Evidence/Phase6-Checks.json)：受保护文件、原 meta、GUID／包冲突、产物摘要、普通启动、退出 Immediate 分配及原始证据 SHA-256。

复跑构建时先按 AGENTS 核对编辑器占用、干净 Edit 场景及版本，创建输出目录。向 `Logs/MigrationPhase6-20261027/build-request.json` 写入 `{"id":"new-build","development":false}` 构建普通 Release；`development:true` 为显式诊断，增加 `plainDevelopment:true` 则为普通 Development。等待同 ID txt 的 BUILD PASS，不能在产物复制结束前启动。构建 API 的预处理器仍可能生成设置字段，按本报告比对并保留授权外的用户改动。

普通运行入口为 `Builds/Phase6/Release/GrassDemo.exe`，需要同目录完整数据和运行库。诊断 Player 在该目录的 Development 子目录启动，传入 `-force-d3d12` 或 `-force-d3d11`、`-screen-fullscreen 0 -screen-width 1280 -screen-height 720`、`-grassValidation <绝对输出目录>` 和 `-logFile <日志路径>`；向该输出目录 request.json 写入带唯一 id 的 `functional`、`inspect`、`profile-pc`／`profile-mobile`／`profile-high`（seconds）、`stability`（seconds／cycles）或 `quit` 请求，依次等待结果，避免覆盖未消费请求。离线分析向 profiler-request.json 提供 id／capture 原始 .raw 路径，`capture:"state"` 只读取 Editor 状态。

Windows 已执行范围迁移验收完成。Android／Vulkan／Metal／XR／其他硬件、RGBA8 fallback、小时／全天耐久及全部驱动瞬态未验证；没有重新验证 Player 空 Overlay 的像素等价性，保留历史 Mobile 0.8 边界、局部 BlendProbes、字体等价替代和 Lit 官方升级。下一对话可按 Status 做交接复核；新增设备范围另定。阶段提交用 `git log -1 -- Docs/Migration/Phase6.md` 定位，实际 SHA 和提交后工作区由交接答复提供，未推送。

文档校验脚本最初因 Windows 默认 GBK 读取中文失败，改为显式 UTF-8 后重跑：64 处本地链接可解析、代码围栏配对，阶段 5 复核记录全文保留。最终提交前另检查精确文件范围及 Git 空白；不提交 Logs、Builds 或缓存。
