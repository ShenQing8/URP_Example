# 阶段 5：演示完整性与缺项恢复

日期：2026-10-27（Asia/Shanghai）。**阶段 5 完成，结论限 Windows Editor／DX12；整体迁移尚未完成最终验收。** 本轮未构建或运行 Player，未进入阶段 6，未推送。

## 接手与环境

- 已读取 AGENTS、Status、Plan、Audit、Phase1、Phase234；核对实际根 `E:\GithubRepositories\URP_Example`，origin 为 `https://github.com/ShenQing8/URP_Example.git`。
- 实际接手 HEAD 为 `8b79517a4128c21ade3b42f4fb5af4cf3ed267c3`，main 跟踪 origin/main，工作区干净；git ls-remote 确认远程默认 main／HEAD 同为 8b79517。用户提示中的发布基线 d52ece0 已有后续文档提交；发布说明与 `.gitattributes` 已包含在 8b79517，原样保留。核心迁移仍为 `b22b027790e627a80a02e48a110feed605842448`，未切回历史迁移分支。
- Unity `6000.6.3f1 (45d8eee7de74)`，URP／Core／Shader Graph `17.6.0`；重新核对 ProjectVersion、manifest、lock 和已安装 package.json。
- Windows Editor／Direct3D12／NVIDIA GeForce RTX 4060 Laptop GPU；复用已有目标会话 PID 32380，没有启动第二编辑器或打开源项目。结束时恢复 PC 管线、1280×720 Game 视图和 SampleScene，退出 Play，编辑器继续运行。
- 源 `E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res` 全程只读；本轮另采集源 88 文件和目标 148 文件快照，不以旧聊天结论替代新检查。

## 实现与替代差异

| 文件／范围 | 改动及原因 |
| --- | --- |
| `TestScene/DemoPerformanceStats.cs` 及 meta | 缺失原 Advanced FPS Counter 和字体，源中也没有可恢复文件。新增独立实现，使用新脚本 GUID，不冒用原插件身份；显示真实 FPS／毫秒、均值／极值、内存与设备信息 |
| `TestScene/scene.unity` | 在原统计对象上接入新组件，替换悬空脚本和字体引用；保留原对象 fileID。字体为实际解析到的 Unity 内置 LegacyRuntime，GUID `0000000000000000e000000000000000`、fileID 10102，无外部字体路径依赖 |
| `MoveCamera.cs` | 提取按钮使用的 NextView，保留 8 个位置和平滑插值；处理空列表／引用保护 |
| 四处原 IMGUI 入口 | 使用 DemoGUI 在小 Game 视图中缩放原控件；保存和恢复 GUI 矩阵、颜色及绘制深度，避免 Renderer 的黑色文字状态污染统计面板。涉及 MoveCamera、RotateLight、PosDefine、Renderer；GPU／Shader／弯曲契约未修改 |
| `SkyBox/SkyBox.mat` | 仅将 Shader 未声明／不读取的 `_MainTex` 悬空 PPtr 设为 0；天空渐变参数与 Shader 保持，未删除天空效果 |
| `ProjectSettings/EditorBuildSettings.asset` | 将草地场景加入首位；保留 SampleScene 和现代 Input System 配置。只配置入口，本轮未执行 Player 构建 |
| `Editor/GrassDemoValidation.cs` 及 meta | 显式文件请求执行本阶段真实操作、图像与 GI 对照；只在 Editor 编译，不进入 Player |

统计每 0.5 秒用实际帧数／真实经过时间采样，毫秒为对应帧周期；最近 50 个区间以总帧数／总时间计算均值。跳过前三个区间后记录区间 FPS 极值；不是逐帧极值。Unity reserved／allocated 来自 Profiler API；graphics 为 Unity 提供的估计值，API 不提供有效值时显示 unavailable，不用设备显存容量伪装使用量。设备容量、屏幕和图形 API 单独显示。

保留 14 号字体、绿／黄／红 FPS 阈值 50／20、黑色半透明背景及反引号统计显隐。保留 `Application.targetFrameRate=60`，面板同时显示真实 FPS 和 VSync；目标值不是测量值。原插件的字体、精确排版和内部统计实现不可得；这里是明确的等价替代，不声称逐像素复原。原 keepAlive 行为改为随演示场景生命周期；不把统计对象带到其他场景。原配置关闭 render 计时，因此没有添加虚假的 GPU 帧时间；设备／Profiler 验收仍属阶段 6。

## 真实操作与回归

| 检查 | 实际结果 |
| --- | --- |
| Unity 编译／Shader | 最新 C# 由现有 Unity 编译并执行；草地／轨迹／天空 Shader 均 supported=True、ShaderUtil 无错误；两个 Lit 也检查无 Shader 错误。保留历史验证辅助的既有 UAC0005 警告，不宣称零警告 |
| 实际鼠标操作 | EditorWindow.SendEvent 将鼠标事件送入真实 GameView／运行时 IMGUI；相机按钮、主光／点光和合批开关、数量／距离／转速滑条全部响应。数量滑条实际生成 300 万位置并读回 GPU counter，距离到 200，转速到 0 |
| 小尺寸／快捷键 | 640×360 和 1280×720 控件可达、面板完整；Input System 注入真实键状态，反引号隐藏／恢复统计通过。最终测试无新增 Unity Error／Exception／Assert，临时尺寸已清理 |
| 相机 | NextView 循环 8 次回到原索引，实际相机持续插值；另对 8 个保存位置逐项渲染并比较 GPU 可见 ID／CPU 参考集 |
| 数量／距离／合批 | 100 万→300 万→1000 万→100 万，25／200 距离实际生效；Append counter=indirect instanceCount，合批开关数量一致。相机参考集回归另验证 ID 集合，未用固定计数 |
| 统计随负载变化 | 数量重建过程中观测 FPS 从约 59.91 到 3.50／1.78，均值与极值随采样改变；这是 Editor 含生成／同步检查停顿的样本，不是稳定帧率或 GPU 性能基线 |
| 灯光／角色 | 真实主光旋转、转速为 0 时停止；四个 Animator 的世界位置均发生变化，4 Animator／4 TrailRenderer 齐全；开关通过实际 GUI 及画面对照 |
| 轨迹／恢复 | 原四轨迹单独均有非白像素；画外／第二相机、乘法重叠和交互带通过；停止发射后真实 5.204 秒，图恢复全白。草地禁用释放 GPU buffer，重新启用恢复实际非零可见数 |
| 缺失引用 | 场景 Missing Script=0；静态扫描 Assets／安装包 GUID，原三项悬空运行资源已解决；字体运行时实际加载并绘出，原 33 个 meta 全部保持 |

PC／Mobile 都通过真实管线分帧切换后的图像和控件检查；Mobile 仍是 Windows 上的 Forward 配置，不能代表 Android。两套相机回归仍保留原生 Overlay 边界：Mobile 比例 0.8 下，无草地场景 RGB 三通道绝对差之和的均值为 0.003552917；临时比例 1 为 0。同栈弯曲开关差异 0.06463214，Overlay 不拥有草地 buffer／Pass。交付比例保持 0.8，没有把原生缩放采样差异当作草地故障或逐像素一致性通过。

## 两个 Lit 材质升级对照

对照使用目标现代 Lit、临时材质副本、冻结相机／角色／灯光和归零风的临时草地材质；在同一目标版本比较参数差异。RGB 指标为归一化图像每通道绝对差均值，和上述相机辅助的三通道和指标不同。

| 条件 | PC | Mobile | 结论 |
| --- | --- | --- | --- |
| 两材质 queue 2000→2050 | 0 | 0 | 当前视角没有可见遮挡变化；保留官方 2000 |
| 恢复旧 `_Color` | 0.000000027 | 0 | 现代 Lit 使用 `_BaseColor`，旧字段同步差异接近零 |
| 将旧未转换发光值直接塞入现代 HDR Lit | 0.003510117 | 0.003543734 | 有实际差异，不能直接回填旧值冒充外观还原 |

Black Dynamic 的旧发光 `(2.6701574, 2.2857304, 0.8010472)` 经 `.linear` 精确得到现值 `(8.677231, 6.163892, 0.6056039)`。已安装 URP `Editor/AssetPostProcessors/MaterialPostprocessor.cs` 的 UpgradeV2 明确修正 50 队列偏移，UpgradeV3 明确补偿旧非 HDR 发光属性的 gamma→linear 转换；Lit.shader 的 `_EmissionColor` 现为 HDR，`_Color` 为隐藏旧字段。两个材质因此维持接手字节，没有手工改色或回退版本。没有在 Unity 2019 运行源项目，不宣称与源外观逐像素一致；原 HDR、SSAO、阴影距离和现代光照路径差异仍保留。

## 旧 GI／探针与是否重烘焙

本轮在目标 Unity 实测，补齐此前“仅 GUID／导入成立”的证据：

- LightingData 的目标内部 `isValid=True`、validityErrorMessage 为空；1 组颜色／方向光照贴图加载，18 个 Renderer 使用有效 lightmapIndex，原混合主光 baked=True。
- 8 个 LightProbes、18 个 tetrahedral cells、216 个 SH 系数均有限，系数范围约 -0.008735334～0.450762063；位置仍为源的局部小探针组，约 x=19.5～21.5、y=18.2～20.2、z=44.2～46.2。没有扩充为覆盖整个草场的逐草叶空间 GI。
- 实际默认反射是 `scene/ReflectionProbe-0.exr`，尺寸 128；LightingData 的四个依赖均由目标 AssetDatabase 解析。场景没有独立 ReflectionProbe 组件，不将默认反射误记为新增实时探针。
- 临时将已加载 lightmap 颜色换黑，PC／Mobile 图像差异分别为 0.008994145／0.009039126，证明旧贴图被使用。
- 保留原 BlendProbes／SampleSH(0) 语义。在关闭直接光、其他 Renderer 与轨迹并固定草地几何后，将 8 个已烘焙 SH 临时归零：插值 L0 从 `(0.128392085, 0.44110328, 0.3269763)` 变为 0；草地图像差异 PC=0.084479801、Mobile=0.093098193。恢复原 SH 后两者残差约 0.000000005，证明探针数据实际到达草地，而非只修改 ambientProbe。
- 天空渐变、雾与角色阴影已目视检查；雾开关图像差异 PC=0.114953086、Mobile=0.123676635。真实阴影和两套管线渲染回归见阶段 2～4 及本轮保存位置图。

据这些兼容加载、有限值、实际着色和恢复证据，**本阶段无需重烘焙**。保留 LightingData／EXR／方向图原字节，未新建 LightingSettings、改 GI 参数或覆盖现代设置。该结论是当前演示的有效性检查，不表示旧烘焙已与 Unity 2019 或新版本重新烘焙结果相同；场景几何／灯光若后续改变，应重新评估并在目标烘焙。

LightingData 工作区原始字节仍为 23,160，SHA-256 `72d97f8980b2c1eab0d82aacdc26417a61d24668086cfcd1e056934a6c98e7ae`。`.gitattributes` 的 binary 规则保持，未发生换行破坏。

## 失败、修正与证据

保留两项真实测试辅助问题：最初 inspect 请求与打开场景的请求跨队列竞态，误在 SampleScene 读取无 LightingSettings 的属性并抛异常；改为 TryGetLightingSettings 并在演示场景重跑。第一次 640×360 控件测试末尾的 RemoveCustomSize 使用了自定义相对索引，Unity 记录 Invalid index，尽管按钮断言通过，该轮不计最终通过；改为总索引、清理本轮命名的残留尺寸，并增加命令期间 Unity 错误计数检查，最终重跑通过。两次问题没有通过清空 Console 或隐去日志绕过。场景外部修改的 Reload 对话通过确认本轮场景变更后加载，没有保存或丢弃用户编辑。

本机完整证据目录：`E:\GithubRepositories\URP_Example\Logs\MigrationPhase5-20261027`；本轮复用核心辅助生成的文件也归档到其中 `CoreRegression`。截图包括 `04-visuals-PC-*`、`06-visuals-Mobile-*`、`10-ui-small-final-game.png`、`11-ui-Mobile-game.png`；原始日志 `Editor-phase5.log`。这些大产物不提交。

随仓库提交的证据：

- [Phase5-Operations.txt](Evidence/Phase5-Operations.txt)：真实负载、最终鼠标／快捷键／尺寸测试。
- [Phase5-Visuals.txt](Evidence/Phase5-Visuals.txt)：两管线材质／GI 图像对照及最终目标场景检查。
- [Phase5-CoreRegression.txt](Evidence/Phase5-CoreRegression.txt)：8 相机位、Overlay 边界、原四轨迹与恢复、最终 Shader 和退出／恢复输出。
- [Phase5-Checks.json](Evidence/Phase5-Checks.json)：新快照保护、GUID／meta、日志和图像 SHA-256，及当前验证边界。

复跑前核对编辑器和锁；本阶段辅助读取 `Logs/MigrationPhase5-20261027/request.json`，如 `{"id":"rerun-visuals","command":"visuals"}` 或 `{"id":"rerun-ui","command":"ui","width":640,"height":360}`，要求演示已 Play，等待同 ID txt。辅助不自动创建目录；先建立输出目录。核心辅助 open／play／quality／interaction 的入口仍见 Phase234，异步轨迹测试需同时检查 overlap／recovery 输出。不要同时排入跨辅助的依赖命令。

本轮源 88 文件及 master／HEAD／四项既有修改不变；目标 Packages、现代管线与 Renderer、两个 Lit、光照资源、原 meta 保持接手字节。唯一 ProjectSettings 改动是明确的构建场景入口。最终提交包含本报告与 Status，可由 `git log -1 -- Docs/Migration/Phase5.md` 定位，实际 SHA 在交接答复提供。

扩大扫描覆盖目标 Assets 的 408 处文本 GUID，草地演示无悬空引用，但不宣称全仓库序列化数据零悬空：原模板 `DefaultVolumeProfile.asset` 有 4 个缺脚本的孤立子资源（OutlineVolumeComponent、TestAnimationCurveVolumeComponent、OasisFogVolumeComponent、TestVolume），均不在序列化 `components` 列表中；实际加载的默认 Profile 为原 19 个有效组件、无 null。PC_Renderer 另保留 7 个旧 `probeVolumeResources` GUID，已安装 URP Deprecated.cs 明确这些资源从 2023.3 起移到 GraphicsSettings 的现代容器。两份文件均与接手哈希相同；未删除孤立项、补造效果或修改现代配置。详情和 GUID 清单见 Checks／CoreRegression 中的 `13-settings-baseline`。这些项不构成本次草地演示的运行缺项，阶段 6 仍需在真实 Player 中检查实际集成；不要把静态未解析项无条件等同于当前运行失败或全项目已无历史残留。

阶段 6 待用户单独批准：完整 Windows Player／启动、Profiler CPU／GPU 时间、峰值／长期稳定性和实际设备验收仍未执行。DX11 仍仅有历史资源编译结论，没有设备运行证据；不把本轮 Editor 统计面板当作最终性能验收。
