# 阶段 1：资源迁入与引用检查

日期：2026-10-27（Asia/Shanghai）。结论：**资源迁入完成，待 API 适配；项目当前编译失败，不能运行验收。** 本记录对应 [Plan.md](Plan.md) 阶段 1，不包含阶段 2～4 的修复或阶段 5 的缺项恢复。

## 接手与迁入范围

- 目标：`E:\GithubRepositories\URP_Example`；分支 `codex/urp6-migration`；接手 HEAD／阶段 0 交接提交 `8350698ed22734ede4ddf59533479e60796241bd`。已核验该提交仅包含四份协作文档，接手时目标工作区干净。
- 源：`E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res`；Git 根即 Res；master，HEAD `cd9f9be6296e21ec7c49b678c3d4cce8c7bc64cd`。复制当前工作区，保留两个材质、packages-lock.json、QualitySettings.asset 的既有改动，没有用源 HEAD 替代工作区资源。
- 从 ProjectVersion、manifest、packages-lock 和 Library/PackageCache 的 package.json 重新核验：Unity `6000.6.3f1 (45d8eee7de74)`，URP／Core／Shader Graph `17.6.0`，Input System `1.20.0`。
- 完整迁入 `Assets/URPMobileGrassInstancedIndirectDemo` 及根目录 meta：26 个资源、33 个 meta（包括 7 个目录）。原 GUID 全部保留；没有补造 GUID、修改代码、删除效果或修复旧 Pass。
- 原 MIT 许可和署名原字节归档于 [LICENSE.txt](../ThirdParty/URPMobileGrassInstancedIndirectDemo/LICENSE.txt)；原说明归档于 [SourceREADME.md](../ThirdParty/URPMobileGrassInstancedIndirectDemo/SourceREADME.md)。作者 `ColinLeung-NiloCat`，版权年份 2020。原说明中的设备／性能数据和截图属于历史资料，不是本项目验证证据。

## 场景入口与管线接线

演示场景位于 `Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/TestScene/scene.unity`。编译修复后可从 Project 窗口打开；本阶段保留模板 SampleScene 及原 Build Settings，未将不可编译的演示切为启动场景。场景、脚本、材质、Compute、四个 Trail Prefab、AnimatorController／AnimationClip 和天空资源的原引用保留。

旧 `UniversalRenderPipelineAsset.asset` 与 `UniversalRenderPipelineAsset_Renderer.asset` 保留原路径和 GUID，供参数及 Feature 子资产对照，**不是目标有效管线**。导入日志包含官方 `UpgradeAsset` 调用栈；磁盘上两个旧资产仍与源逐字节一致（RPAsset 的 k_AssetVersion 仍为 5），不能把一次导入等同于完整升级。Feature 编译失败，相关升级／保存与接线尚未验收；阶段 4 应把 RenderGraph Feature 接入目标现代 Renderer 并验证，不启用这套旧 Renderer 代替目标配置。

目标有效 PC／Mobile RPAsset -> Renderer 引用链、PC Forward+、Mobile Forward、SSAO、Graphics／Quality／Build 设置及现代包依赖保持原样。GraphicsSettings 默认管线仍为空，由原 QualitySettings 的管线覆盖生效；没有导入源 ProjectSettings、旧依赖、lock 或缓存。

## 实际导入与编译结果

| 项目 | 执行与结果 |
| --- | --- |
| 环境 | Windows Editor，Unity 6000.6.3f1，URP 17.6.0，Direct3D 12；项目路径为上述目标目录 |
| 编辑器占用检查 | 已有目标编辑器 PID 46016，窗口为 SampleScene，存在 UnityLockfile；未发现源 Unity 2019 编辑器，未新增或强制终止编辑器进程 |
| 执行方式 | 先在目标 Logs 下暂存完整资源与 meta，再迁入 Assets；既有会话自动触发 AssetDatabase Refresh。曾尝试 WScript.AppActivate 激活该会话，返回失败，未发送 Ctrl+R；实际导入证据来自自动 Refresh |
| 资源导入 | 日志中有 7 个目录及 20 个非 C# 资源的正常 Importing／Imported 记录；6 个 C# 文件进入 Unity 自身 Assembly-CSharp 编译输入。Refresh `c6317ece2bfde7041b26a1e76747e6ac`，15.800 秒，domain reloads=0；随后材质升级 Refresh `986cc029cc151c4439af118745fafd12` |
| C# 编译 | **失败（预期阻断）**：GrassBendingRTPrePass.cs:18，Configure override，CS0115；同文件 :31，Execute override，CS0115。Unity 编译器退出码 1，不是外部独立编译替代验证 |
| 旧 Renderer | **失败（预期后果）**：`UniversalRenderPipelineAsset_Renderer is missing RendererFeatures`，提示 missing scripts or compile error。Feature 的 GUID／子资产引用静态存在，类型因编译失败不可用；没有删除它来消除报错 |
| Shader／Compute | 实际调用了 3 个 ShaderImporter 和 1 个 ComputeShaderImporter；本次日志切片未报告 Shader／Compute 错误。仅证明导入发生，不证明目标变体、Shader 标签、Forward+ 光照或 GPU kernel 运行正确；这些仍未验证 |
| 场景与 ExecuteAlways | 保持 SampleScene 打开，没有打开迁入场景、进入 Play 或进行运行时检查；本轮编译失败且无 domain reload，场景、Prefab、代码文件均未出现序列化写回 |
| Play／GPU／构建／视觉 | **未执行／未验证**；未证明草地显示、剔除计数、光照、弯曲或旧烘焙效果可用 |

当前两项 C# 错误定位清楚，但修复后可能暴露其他编译问题；本阶段不把未出现的诊断预先计为通过，也不展开阶段 2～4 的 API 修复。

## 导入写回差异

59 个迁入文件中 57 个保持源字节，两个 Lit 材质由 Unity 官方材质升级器写回；全部 33 个 meta、6 个 C#、3 个 Shader、1 个 Compute、场景和旧管线资产原字节保留。逐文件源／目标 SHA-256 见 [Phase1-Inventory.tsv](Evidence/Phase1-Inventory.tsv)。

| 资源／设置 | 真实变化与处置 |
| --- | --- |
| Black Dynamic.mat、GrassGround.mat | AssetVersion 1 -> 10，Material serializedVersion 6 -> 8；关键词迁入 m_ValidKeywords，补充现代 Lit 参数及 MOTIONVECTORS 禁用记录。保留升级后的材质及原 GUID，没有手工调整视觉公式 |
| 两材质的渲染队列 | 官方升级器从 2050 改为 2000，_QueueOffset 仍为 0；此差异尚无画面比较，阶段 5 必须核对遮挡／绘制顺序，不能称外观一致 |
| Black Dynamic.mat 的颜色 | _BaseColor 仍为黑色；_Color 从白色同步为黑色；_EmissionColor 从 (2.6701574, 2.2857304, 0.8010472, 1) 改为 (8.677231, 6.163892, 0.6056039, 1)。这是实际升级差异，视觉影响待阶段 5 验证 |
| GrassGround.mat 的颜色 | _BaseColor 保留约 (0.1731043, 0.3773585, 0.13705945, 1)；旧 _Color 被同步到该颜色。阶段 5 对照源工作区和实际画面检查 |
| ProjectSettings/URPProjectSettings.asset | Unity 自动补写 `m_ProjectSettingFolderPath: URPDefaultResources`；核对官方包该字段默认值及唯一 diff 后，恢复导入前原字节。只还原本次自动新增字段；最终全部 70 个目标基线文件哈希与接手时相同 |

## 引用检查

静态扫描目标 Assets 与已安装 PackageCache 的 meta；没有缺失 meta、迁入 GUID 重复或与目标／包冲突。299 个非空同文件 PPtr 均有 YAML anchor，外部文本 PPtr 共 148 处：Assets 88、包 33、Unity 内置 24、已知悬空 3。可检查的外部 YAML anchor 均存在；4 个 `m_SourcePrefab` 的 `fileID: 100100000` 属于 Prefab 资源入口，单独分类，不误判为缺少文本 anchor。24 处 importer 生成的外部 fileID 只完成资源／GUID 分类，尚未在可编译场景中做运行引用检查。

包内 Lit 材质 `31321ba15b8f8eb4c954353edc038b1d` 的 20 处场景引用全部可解析；其余包引用（Lit Shader、AssetVersion、相机／灯光数据、旧管线脚本及内置 Shader 等）也能在目标已安装包中定位。包 GUID 汇总见 [Phase1-Checks.json](Evidence/Phase1-Checks.json)。

| 保留的悬空 GUID | 当前分类／后续 |
| --- | --- |
| e4ff4051e75dc9443879b116bd65c930 | scene.unity:1642，缺 Advanced FPS Counter；阶段 5 恢复或基于真实数据实现等价统计 |
| fb80074802966ad438ec681fae1be1c2 | scene.unity:1727，缺统计字体；阶段 5 处理，记录替代差异 |
| 606abc37fcc31474699772fde405e2b4 | SkyBox.mat:23，闲置 _MainTex，现有天空 Shader 不读取；本阶段保留并记录 |

LightingData.asset 的二进制 v21 头为 `2019.4.3f1`；重新解析外部引用表，场景／颜色光照图／方向光照图／反射 Cubemap 四个 GUID 均解析到迁入资源。实际 NativeFormatImporter 已导入，但 GI、LightProbes 和烘焙兼容性仍未验证。源 Preset／Timeline 悬空引用只存在于未复制的旧 ProjectSettings，不是新增目标资源缺项。

## 证据与保护结果

可提交的小体积证据：

- [Phase1-Inventory.tsv](Evidence/Phase1-Inventory.tsv)：59 个文件的路径、GUID（meta）、源与最终目标 SHA-256、升级分类。
- [Phase1-Checks.json](Evidence/Phase1-Checks.json)：检查时间、环境、基线摘要、引用统计、二进制外部表、包 GUID、缺项、实际编译错误。
- [Phase1-UnityImport.txt](Evidence/Phase1-UnityImport.txt)：真实 Editor 输出节选，行号对应本地原始切片。

完整本地证据位于 `E:\GithubRepositories\URP_Example\Logs\MigrationPhase1-20261027`（Git 忽略）：before.json、prepare.py、check.py、finish.py、两个材质 diff、自动写回设置副本、Editor-import.log。原日志为 `E:\GithubRepositories\URP_Example\Logs\Editor.log`，本次切片从字节偏移 2363677 开始。检查命令为 `python Logs\MigrationPhase1-20261027\check.py` 和 `python Logs\MigrationPhase1-20261027\finish.py`；脚本只保存在本机证据目录，原始日志和大缓存不提交。

按 Status 阶段 0 的同一算法重新计算：原有目标 70 文件摘要仍为 `e14bd1fe1f07a002b270243bf810326777b056cd8dabddb7321d8c5ee7b59c50`；源 88 文件摘要仍为 `5fc3140fbb1d2d37f9beab286f80d0ea6b78eb0ee367271394ccdbb399626075`。源分支、HEAD 和四项既有 Git 改动保留。许可和原 README 的归档副本分别与源字节一致。

暂存范围核对为 66 文件（59 个迁入文件、2 个许可／说明归档、5 个状态／报告／证据文件）。全量 `git diff --cached --check` 返回 2：185 处行尾空白、2 处文件末尾空行，均位于原迁入文件、Unity 序列化材质或原 README 中；完整输出留 `Logs/MigrationPhase1-20261027/git-diff-cached-check.txt`。本阶段保留这些原始／工具生成格式，没有为消除提示重写源代码或 meta；针对新写 `Docs/Migration` 的同一空白检查通过。这里不把全量检查记为通过。

## 交接结论

阶段 1 按资源迁入验收完成：清单齐全、GUID 保留、静态引用及原缺项分类清楚、目标 Unity 实际导入及预期编译阻断有证据、目标原配置与源基线受保护。**整体迁移尚未完成。** 下一入口为实施 B／阶段 2～4 联合迁移；仅在用户另行批准后修复 CPU／Compute／Indirect、Shader 和 RenderGraph，共享契约与运行结果必须联合验证。阶段 5 另行处理 FPS／字体、材质升级视觉差异和烘焙有效性。
