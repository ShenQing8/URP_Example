# 分阶段迁移计划与交接规范

日期：2026-10-27（Asia/Shanghai）。本文件定义实施范围和验收要求，不是授权记录。当前授权与进度以 [Status.md](Status.md) 及用户最新指令为准。审计事实、完整功能、风险编号及官方依据见 [Audit.md](Audit.md)；仓库执行约束见根目录 [AGENTS.md](../../AGENTS.md)。

## 1. 交付目标与阶段组织

在 `E:\GithubRepositories\URP_Example` 内，将只读源 `E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res` 的草地演示适配到 Unity 6000.6.3f1、URP 17.6.0。保留真实 CPU／GPU 剔除、GPU counter 驱动的 Indirect、光照／风／雾与轨迹弯曲，以及原演示操作。

新自定义 Render Pass 使用 RenderGraph。原则上保留脚本层 CPU／Compute 调度并通过 RenderMeshIndirect 接入 URP；不为迁移而强制把全部计算也写成自定义 RG Pass。如实现需要将 buffer 接入图，必须增加 ImportBuffer／UseBuffer 等对应依赖，不能用 Unsafe Pass 隐藏依赖。具体相机调度和 buffer 选择必须在阶段 2～4 联合验证后定案。

| 对话 | 阶段 | 原因 |
| --- | --- | --- |
| 当前协调对话 | 0：基线与文档 | 保存审计和约束，后续只协调范围／验收 |
| 实施 A：资源迁入 | 1 | 核对迁入范围和 GUID；明确仍未完成 API 适配 |
| 实施 B：核心迁移 | 2、3、4 | C#／Compute／Shader／RenderGraph 共享契约，在同一对话内联合实现与验证 |
| 实施 C：演示完整性 | 5 | 恢复操作、统计、场景和光照资源 |
| 实施 D：最终验收 | 6 | 验证整体行为、构建和运行证据 |

所有对话串行使用同一本地目录和已确认的接手分支，不并发写入；不为每个阶段建立另一份 Unity 项目。阶段 0～4 使用过 `codex/urp6-migration`；用户发布远程后当前接手分支已为 `main`，具体 HEAD／上游以 Status 和实际 Git 为准，不自动切换历史分支。阶段 2、3、4 的中间提交可以尚待集成，但在取得实际 Unity／GPU 证据前不标记各项运行验收完成。

远程仓库为 [ShenQing8/URP_Example](https://github.com/ShenQing8/URP_Example)，克隆地址为 `https://github.com/ShenQing8/URP_Example.git`。本计划中的 E 盘路径记录原实施环境；异机接手先确认实际仓库根，再解析仓库内相对路径。原始审计与验证路径／哈希不批量替换；新环境测试生成自己的证据。

## 2. 阶段 0：冻结基线与文档

- **原因**：新对话不依赖聊天记忆；把已确认的约束、证据和范围保存为版本化记录。
- **允许修改**：目标根 AGENTS.md，以及 Docs/Migration/Audit.md、Plan.md、Status.md；创建目标本地迁移分支并提交这些文档。
- **工作**：保存已完成的审计、七阶段计划、当前授权、证据边界和接手提示；记录初始提交与源既有改动。
- **验证**：核对版本、Git 状态、文档引用、阶段编号和授权范围；git diff --check；对阶段开始前后源／目标 Assets、Packages、ProjectSettings 及既有根文本文件进行 SHA-256 清单比较；确认提交仅包含四份文档。
- **验收**：文档可供独立对话接手；有实际本地提交；源／目标受保护文件内容不变；未开始资源迁移。
- **未解决**：Unity 编译和运行全部未验证；源缺失资源仍待处理；阶段 1～6 仍未获批。

## 3. 阶段 1：迁入资源与引用接线

- **前置**：阶段 0 已完成，当前对话获准阶段 1；核对实际 HEAD 与阶段 0 提交。
- **原因**：先保证资源身份和包引用可追溯，避免后续 API 修复同时混入 GUID 丢失。
- **文件范围**：迁入目标 Assets/URPMobileGrassInstancedIndirectDemo 及相关 meta、原 LICENSE／署名的合适归档位置；按实际需要处理演示场景入口和旧管线资产。保护目标 Packages 版本和现代设置。
- **工作**：完整迁入 26 个源资源及 meta；分类引擎／包资源、悬空引用和旧配置；旧管线资产由目标版本升级或适配，不能把旧序列化字段直接覆盖现代 PC／Mobile 配置。核对数量和内容一致性。
- **验证**：迁入清单／GUID／PPtr 静态检查；使用目标 Unity 导入并记录实际出现的错误。导入可能运行源 ExecuteAlways 脚本，其产生的序列化变化要检查并说明。编辑器占用时按仓库约定处理，不对源项目启动 Unity。
- **验收**：迁入内容齐全，所有缺项和编译阻断已定位，已有目标设置未被整体覆盖。结论只能是“资源迁入完成，待 API 适配”，不能称项目可运行。
- **未解决**：旧 Configure／Execute、Shader 及 Indirect 尚待阶段 2～4；缺失 FPS 插件／字体尚待阶段 5。无法实际导入时记录“待验证”，不能把导入验收记为通过。

## 4. 阶段 2：剔除与 Indirect 数据链

- **前置**：阶段 1 的资源清单已接收，实施 B 获准阶段 2～4。
- **原因**：修复 R01～R07、R16、R17，保持 GPU 实例数和可见 ID 的真实数据链，避免新 API 下看似渲染却实际未剔除。
- **主要文件**：Core/InstancedIndirectGrassRenderer.cs、InstancedIndirectGrassPosDefine.cs、CullingCompute.compute；Shader 的配套 Indirect ID 接口由阶段 3 同步。
- **工作**：增加 dispatch 区间及总数保护；处理空／退化区域；覆盖变形的 Cell／整批 Bounds；明确位置刷新契约、初始化／释放、相机最终矩阵及提交时序；使用 RenderMeshIndirect、IndirectDrawIndexedArgs 和实际 CopyCount。Cell offset 可预计算，保留合批开关和真实统计。
- **验证**：在目标 Unity 中实际派发 0／1／63／64／65 和非整组数量；单点、单轴、非方形、移动区域；检查 ID 范围、唯一性和 Append counter 与间接实例数一致；合批开关可见集一致；旋转／切换相机及视锥边缘不丢草；反复重建／禁用释放资源。
- **验收**：真实 CPU 粗剔除、GPU 细剔除和一次主颜色 Indirect 绘制成立；没有固定可见数或全量绘制替代；有 GPU 数据证据。测试用例不是运行时假数据替代品。
- **未解决**：Shader、灯光、弯曲图与多相机契约在阶段 3、4 联合验证前均待验证；不能在旧 Pass 仍阻断编译时宣称 GPU 测试通过。

## 5. 阶段 3：Shader、光照与动画适配

- **原因**：解决 R08～R11 和 R15，适配 PC Forward+、保持 Mobile Forward 及原草地外观。
- **主要文件**：Core/InstancedIndirectGrass.shader、GrassBending/GrassBendingRTPrePassTrailShader.shader；必要时新增共用变形 HLSL；对应材质仅按实际接口需要调整。SkyBox Shader、RotateLight.cs、ApplicationFPS60.cs 均检查并保留，不预设必须改写。
- **工作**：修正管线标签、资源／CBUFFER 布局、能力 target 和 URP HLSL 采样；接入 UnityIndirect ID；保留三组风、随机宽高、billboard、弯曲、远距离增宽和着色公式；正确实现 clustered 附加光与主光／附加光阴影语义。若为目标 SSAO 补 DepthOnly／DepthNormals，所有 Pass 共享真实变形。
- **验证**：目标 Unity 实际编译相关 Shader 变体；分别在 PC Forward+／Mobile Forward 检查主光和点光开关、级联阴影、风参数、雾、颜色纹理、随机草形、环境光；比较颜色和深度／法线轮廓。检查构建变体保留而非仅编辑器显示正确。
- **验收**：附加光真实影响草地，阴影来自真实采样；颜色／几何与深度法线一致；没有删掉原效果以消除错误。Forward+ 片元路径的性能与外观差异有记录。
- **未解决**：弯曲图的生产时机、投影和生命周期仍依赖阶段 4；目标后端实际执行情况按平台记录。原例无草叶 ShadowCaster，不把新增投射阴影暗中列入必做或宣称已经存在。

## 6. 阶段 4：RenderGraph 交互弯曲

- **原因**：旧 Pass 入口已移除；修复 R12～R15 并满足不依赖兼容模式的约束。
- **主要文件**：GrassBending/GrassBendingRTPrePass.cs、轨迹 Shader／材质、目标 Assets/Settings/PC_Renderer.asset、Mobile_Renderer.asset；必要时同步消费者声明和 RPAsset 配置。
- **工作**：RecordRenderGraph 中创建 512×512 线性 R8 白底图，保持轨迹乘法混合；核对设备渲染／混合格式能力并记录必要等价格式；使用匹配俯视区域的 CullContextData 和 RendererList；SetRenderAttachment、UseRendererList、DrawRendererList；SetGlobalTextureAfterPass 发布并检查读取依赖。明确 GPU 投影／翻转和状态恢复。执行时机早于所有采样弯曲的草地 Pass。
- **验证**：Render Graph Viewer 检查纹理生产／消费、Pass 剔除和生命周期；Frame Debugger 或等价实际抓帧检查 R8 内容及绘制顺序；四角色轨迹位置、重叠和约 5 秒恢复；画外轨迹、无轨迹白底、Feature 禁用／重新启用、多相机与质量切换；检查后续场景矩阵未被污染。
- **验收**：核心草地在目标 Unity 实际编译运行；CPU／GPU／Indirect、光照动画及交互弯曲联合通过；阶段 2～4 各验证条目有结果。实现不含旧兼容路径，不修改 PackageCache，不通过 Unsafe Pass 绕过资源依赖。
- **未解决**：完整统计面板和旧烘焙资源仍属阶段 5；未测试设备／图形 API 继续标记未验证。

## 7. 阶段 5：演示完整性与缺项恢复

- **前置**：阶段 2～4 核心联合验收已通过，当前对话获准阶段 5。
- **原因**：核心草地显示正常不等于原演示完整；处理 R18 和遗留引用。
- **主要文件**：TestScene/scene.unity、MoveCamera.cs、两个 Lit 材质、动画／controller／轨迹 Prefab、SkyBox 材质、光照资源及必要新增统计组件／字体；按需要调整目标场景和 Build Settings。
- **工作**：恢复数量／距离／合批 UI、8 个相机位置、灯光操作、四角色动画／轨迹和 60 FPS 目标设置。优先恢复原 FPS 组件／字体；资源不可得时按已记录配置实现真实 FPS／毫秒／均值极值／内存／设备统计，记录与原组件的差异，无法取得的指标不伪造。检查材质自动升级、天空、烘焙 GI／探针；必要时在目标重新烘焙并记录前后差异。
- **验证**：逐项操作功能清单；检查 Missing Script／资源和播放错误；统计随真实负载变化；材质、天空、雾、阴影、角色运动和恢复时间有视觉证据；关闭草地再启用、改变数量与相机仍正确。检查新字体／烘焙资产的可交付引用。
- **验收**：原演示已实现功能完整；遗留缺失运行引用已解决；任何等价替代与视觉差异明确列出，不宣称缺少原插件／字体时已经逐像素复原。
- **未解决**：平台选择、设备条件和性能基线可能仍需用户输入；最终构建稳定性属于阶段 6。

## 8. 阶段 6：集成、构建与运行验收

- **前置**：阶段 5 功能清单已通过，当前对话获准阶段 6。平台未指定时先 Windows；不以 Windows 通过代替 Android 验证。
- **原因**：补齐从源码／编辑器到实际可运行产物的证据。
- **文件范围**：必要的 Unity 集成验证脚本、构建配置、实际问题修复及本文档／状态记录。构建产物与日志另记路径，不默认提交大文件。
- **验证**：实际 Unity 导入与编译、Play Mode 操作清单、Windows Standalone 构建并启动运行；真实 GPU counter／ID／绘制链证据；数量／距离与合批边界；PC／Mobile 质量切换；持续运行、反复启停与 buffer／Mesh／RT 释放；Profiler 记录 CPU／GPU 时间及内存峰值。Android 若纳入目标，实际编译指定图形 API 并在支持 Compute／Indirect 的设备上运行，记录设备、API 和结果。
- **验收**：无项目相关未处理 C#／Shader／Compute 错误、Missing Script 或缺失运行资源；五项核心功能与演示交互有真实证据；构建产物能运行；遗留差异和未测试平台明确。只有实际执行且通过的范围可称为迁移完成。
- **未解决**：若设备、图形 API 或原始资源不可得，相关项保持未验证或列出限制；不能把缺少证据的整体验收标成完成。

## 9. Git 与证据规范

初始目标提交：`42c1fde2ef37cde1348cd587c8215494d99e9356`，作为可回退的原模板基线保留在历史中；main 现已承载迁移与文档提交。每次开始核对实际分支、HEAD、origin 地址和上游跟踪关系，按当前获准范围串行实施；若需新功能分支，先明确该阶段的分支安排，不自动回到旧迁移分支。源仓库不参与提交。后续阶段记录接手提交及本阶段提交，不在文档中伪造尚未生成的 SHA。

每次提交仅包含本阶段自有改动。阶段中间不可运行的提交在说明中标明“待适配／待联合验证”，不能用“编译成功”标题误导。用户已授权的范围内可做合理检查点提交；不擅自推送远程或丢弃用户改动。

验证记录至少包含：日期、目标 Unity／URP、项目路径、平台／图形 API、执行方式或命令、输入条件、通过／失败／未执行、日志或截图路径、问题和原因。启动工具不等于测试通过；只有 -batchmode 退出也不等于画面和交互已验证。

阶段状态可用：未开始、进行中、待联合验证、待验证、受阻、完成。将授权与工作状态分列。完成以验收证据为依据；阶段 1 可按资源迁入验收完成且项目不可运行，阶段 2～4 完成依赖联合证据，阶段 6 完成依赖实际运行产物。

## 10. Status 与对话交接规范

每次结束更新 Status 的当前摘要，并追加一条有日期的活动记录。保留历史失败、修正和关键决策，不覆写成只剩成功结论。记录：

1. 当前阶段、授权范围、接手 HEAD 和实际工作区状态。
2. 修改的文件／共享接口、修改原因及范围变化。
3. 已实际执行的验证与证据；失败和未执行分别列出。
4. 尚未解决的问题、风险编号及是否阻挡下一阶段。
5. 本阶段提交与下一阶段入口。包含 Status 本身的提交可以用 `git log -1 -- Docs/Migration/Status.md` 定位，在交接答复给实际 SHA；下一阶段接手时补记该 SHA，避免自引用提交哈希。
6. 可直接复制的下一对话提示。上一阶段条件未满足时，继续当前阶段，不通过新对话绕过阻断。

用户负责按顺序批准／启动实施对话、确认阶段结果与视觉检查；代理负责读取仓库交接资料、实施、修复、验证及更新状态。已授权阶段中的例行工作无需反复请求许可；扩大范围或确实缺少必要输入时才澄清。不能假定新对话自动获得旧聊天历史。

### 下一实施对话提示模板

```text
继续 Unity 草地项目迁移。
仓库：https://github.com/ShenQing8/URP_Example.git
目标目录：当前克隆的仓库根（本机 E:\GithubRepositories\URP_Example）
源目录：E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res（只读）
目标版本：Unity 6000.6.3f1，URP 17.6.0。

先从已确认的目标根目录读取：
AGENTS.md
Docs/Migration/Audit.md
Docs/Migration/Plan.md
Docs/Migration/Status.md

核对实际工作目录、接手分支、HEAD、origin／上游、工作区与上一阶段证据，不自动切回历史迁移分支。
本对话批准实施的阶段范围：【由用户填写，例如阶段 1】。
在该范围内实施和修复直到满足验收标准，保留已有用户改动。
结束时更新 Status，说明修改原因、真实验证、未验证项、问题和下一步。
允许提交本对话产生的本地改动，不推送远程。
完成指定范围后停止，不自动进入未授权阶段。
```
