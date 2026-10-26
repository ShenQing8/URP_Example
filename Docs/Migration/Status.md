# 迁移状态与交接记录

更新日期：2026-10-27（Asia/Shanghai）。本文件记录当前执行事实与授权。审计见 [Audit.md](Audit.md)，阶段验收和交接方法见 [Plan.md](Plan.md)，仓库约束见 [AGENTS.md](../../AGENTS.md)。

## 当前摘要

- **当前阶段**：阶段 1／实施 A 完成（资源迁入验收）；详细证据见 [Phase1.md](Phase1.md)。
- **整体迁移**：26 个源资源及 33 个 meta 已迁入；目标 Unity 实际导入，C# 因旧 Configure／Execute override 报两项 CS0115，项目当前不可运行验收。阶段 2～4 尚未实施。
- **当前授权**：用户批准本对话仅实施阶段 1，允许阶段内资源迁入、导入／引用检查、Status／证据更新及本地提交；不推送远程，不自动开始阶段 2。本轮已完成该范围。
- **下一阶段**：实施 B／阶段 2～4 联合迁移，尚未获批。等待用户明确批准；不在本轮修复旧 API 或 Shader。
- **目标目录**：`E:\GithubRepositories\URP_Example`。
- **源目录**：`E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res`，资源与仓库保持只读；阶段 0 操作纠正见活动记录。
- **目标版本**：Unity 6000.6.3f1／URP 17.6.0；源为 2019.4.15f1／URP 7.3.1。
- **迁移分支**：`codex/urp6-migration`（目标仓库）。
- **本阶段接手 HEAD**：`8350698ed22734ede4ddf59533479e60796241bd`；开始时目标工作区干净，迁移分支正确。初始 main 基线仍为 `42c1fde2ef37cde1348cd587c8215494d99e9356`。
- **阶段 0 交接提交**：已核验 `8350698ed22734ede4ddf59533479e60796241bd`，仅包含 AGENTS、Audit、Plan、Status 四份文档。
- **阶段 1 提交与工作区**：用 `git -C E:\GithubRepositories\URP_Example log -1 -- Docs/Migration/Status.md` 定位包含本文的阶段 1 提交；实际 SHA 和提交后工作区状态在交接答复给出，下一对话补记。提交内容仅为本阶段迁入资源、许可归档和证据／状态；原有目标 70 文件、源 88 文件内容均保持基线。

## 阶段总表

| 阶段 | 工作 | 授权 | 状态 | 验证摘要 |
| --- | --- | --- | --- | --- |
| 0 | 基线、审计、计划与交接文档 | 已批准 | 完成 | 版本、文档引用、阶段字段、四文件范围及受保护内容哈希检查通过；以文档提交交接 |
| 1 | 资源迁入与引用接线 | 已批准 | 完成（资源迁入） | 26 资源／33 meta 齐全；GUID／PPtr 检查通过，原三处缺项保留分类；Unity 已实际导入，两项预期 CS0115 阻断编译；不等于可运行 |
| 2 | CPU／Compute／Indirect | 未批准 | 未开始 | 未运行 Unity／GPU 验证 |
| 3 | Shader／光照／动画 | 未批准 | 未开始 | 未编译目标 Shader |
| 4 | RenderGraph 弯曲 Pass | 未批准 | 未开始 | 未实现／验证 |
| 5 | 演示完整性与缺项恢复 | 未批准 | 未开始 | 原缺失插件／字体等仍未处理 |
| 6 | 构建、性能与最终运行 | 未批准 | 未开始 | 未构建或运行迁移项目；Android 也未验证 |

## 阶段 0 文件与原因

| 文件 | 内容／原因 |
| --- | --- |
| `E:\GithubRepositories\URP_Example\AGENTS.md` | 把授权检查、源只读、功能保留、RenderGraph 和证据要求提供给每个接手代理 |
| `E:\GithubRepositories\URP_Example\Docs\Migration\Audit.md` | 保存静态发现、资源与 API 证据、纠正过的 GUID 判断和风险 |
| `E:\GithubRepositories\URP_Example\Docs\Migration\Plan.md` | 七阶段范围、各阶段原因／文件／验证／未解决问题及多对话交接规范 |
| `E:\GithubRepositories\URP_Example\Docs\Migration\Status.md` | 当前授权、进度、真实验证和后续入口 |

阶段 0 不修改 Assets、Packages、ProjectSettings，不启动 Unity，不做阶段 1 的资源复制／导入。允许的目标 Git 元数据变化是新建迁移分支及其文档提交。

## 阶段 0 验证记录（历史）

| 检查 | 当前结果／证据 |
| --- | --- |
| 目标 Git 初始状态 | 通过；main，HEAD 42c1fde2ef37cde1348cd587c8215494d99e9356，无工作区改动 |
| 目标／源版本 | 通过；直接读取两边 ProjectVersion、manifest，目标 URP 17.6.0／源 7.3.1 |
| 写入前内容快照 | 已采集；SHA-256 清单：目标 70 文件、源 88 文件；涵盖 Assets、Packages、ProjectSettings，以及存在的 README／LICENSE／.gitignore／.gitattributes |
| 源仓库既有改动 | 四项保留：两个材质、packages-lock.json、QualitySettings.asset；未暂存或提交源改动 |
| 文档本地引用／范围／阶段一致性 | 通过；四份文档链接可解析，阶段 0～6 各含原因、验证、验收及未解决问题，Markdown 围栏与空白检查通过 |
| git diff --cached --check | 通过；暂存内容无空白错误 |
| 写入前后受保护文件哈希比较 | 通过；目标 70／70、源 88／88，新增 0、删除 0、内容变化 0 |
| 暂存范围 | 通过；仅 AGENTS.md、Audit.md、Plan.md、Status.md 四份文档；无既有跟踪文件改动 |
| 提交与最终工作区 | 以本文件所属阶段 0 提交及交接答复中的提交后 Git 检查为准；只允许上述四文件，不推送 |
| Unity 导入／编译／Play／GPU／构建 | 未执行，本阶段不以这些为通过项 |

受保护内容清单的汇总 SHA-256（不含 Git 元数据、缓存及新增协作文档）：

| 目录 | 文件数 | 清单摘要 |
| --- | --- | --- |
| 目标 | 70 | `e14bd1fe1f07a002b270243bf810326777b056cd8dabddb7321d8c5ee7b59c50` |
| 源 | 88 | `5fc3140fbb1d2d37f9beab286f80d0ea6b78eb0ee367271394ccdbb399626075` |

复算方法：对上述范围每个文件读取原始字节计算 SHA-256；用相对于各项目根的 POSIX 路径排序，每项编码为 `路径 + TAB + 小写 SHA-256 + LF`，拼接后以 UTF-8 编码再计算 SHA-256。该值固定的是阶段 0 内容基线；实施后目标摘要预期改变，源摘要应保持不变。

## 未解决事项

| 事项 | 当前处理／影响 |
| --- | --- |
| 旧 Pass、剔除边界、Forward+、Shader 和弯曲纹理契约 | 阶段 1 已实际观察 Configure／Execute 的 CS0115 和旧 Renderer 缺 Feature 错误；等待获批阶段 2～4 联合修复／验证；当前没有迁移运行成功结论 |
| FPS Counter 脚本和字体缺失 | 等待阶段 5 恢复或基于真实数据做等价实现；原样字体／插件不可得时必须记录差异 |
| 旧 Preset、Timeline 设置及闲置天空贴图引用 | 已分类，不将旧设置覆盖到目标；后续按是否为运行依赖处理 |
| LightingData 在 Unity 6 的有效性 | 四个二进制外部 GUID 齐全且已实际导入，但 GI／LightProbes／烘焙效果未验证；必要时在目标重烘焙 |
| Lit 材质自动升级差异 | Unity 将两个材质升级至 AssetVersion 10，队列 2050 -> 2000，部分颜色／发光值变化；完整 diff 在本地证据目录，数值摘要见 Phase1.md。阶段 5 必须实际比较，不宣称外观一致 |
| 旧管线资产升级与接线 | 两旧资产已导入且发生官方 UpgradeAsset 调用，磁盘仍为源字节；未接入目标有效管线，Feature 因编译失败不可用。阶段 4 使用现代 PC／Mobile Renderer 接入 RenderGraph，不以旧 Renderer 替换 |
| 验收平台 | 尚未新增用户选择；计划优先 Windows，Android 单独验证，不承诺尚无证据的设备结果 |
| 目标编辑器占用 | 阶段 1 使用现有 PID 46016／SampleScene／DX12 会话自动导入，没有新增或强制关闭编辑器；后续重新核对锁与会话 |

## 阶段 1 文件与实际验证

| 范围 | 改动与原因 |
| --- | --- |
| Assets/URPMobileGrassInstancedIndirectDemo 及根 meta | 完整复制源工作区 26 个资源、33 个 meta，保留身份和场景／材质／Prefab／动画接线；57 文件源字节一致，仅两个 Lit 材质由 Unity 自动升级 |
| Docs/ThirdParty/URPMobileGrassInstancedIndirectDemo | 原 LICENSE 与 README 原字节归档，保留 ColinLeung-NiloCat MIT 署名；历史性能及截图不作为新证据 |
| Docs/Migration/Phase1.md、Evidence/Phase1-* | 记录实际导入、编译阻断、引用分类、双边 SHA-256 清单和自动写回差异 |
| Docs/Migration/Status.md | 更新本轮授权、验收边界与下一实施入口，保留阶段 0 历史 |

| 检查 | 实际结果／边界 |
| --- | --- |
| 环境 | Unity 6000.6.3f1 (45d8eee7de74)、URP／Core／Shader Graph 17.6.0；Windows Editor、Direct3D 12；项目 E:\GithubRepositories\URP_Example |
| 资源／GUID | 26 资源／33 meta 齐全，33 GUID 保留，无重复／目标或包冲突；未复制源 Packages、ProjectSettings 或缓存 |
| PPtr／包引用 | 299 处同文件 PPtr 均解析；148 处外部文本引用分类为 Assets 88／包 33／内置 24／已知缺失 3；4 个 Prefab 特殊入口 fileID 单列；24 处 importer 生成 fileID 未做运行引用验收 |
| 缺项 | 与 Audit 一致：FPS Counter、字体、闲置天空 _MainTex；未删除配置，未伪造替代项；前两项阶段 5 处理 |
| LightingData | 二进制 v21 外部表四 GUID 均解析；NativeFormatImporter 实际导入；GI／LightProbes／旧烘焙效果仍未验证 |
| Unity 实际导入 | 使用既有 PID 46016，保持 SampleScene；自动 Refresh 记录 7 目录／20 非 C# 资源导入、6 C# 编译输入；未启动第二会话。WScript 激活尝试失败，未发送快捷键，自动导入不受影响 |
| Unity C# | **失败（预期）**：GrassBendingRTPrePass.cs:18 Configure、:31 Execute，均 CS0115；编译退出码 1；旧 Renderer 同时报 missing RendererFeatures。留给阶段 2～4，不绕过编译 |
| Shader／Compute | 3 Shader／1 Compute importer 实际运行，切片未报告相关错误；完整变体、GPU kernel 执行和各 API 后端均未验证 |
| 序列化写回 | 接受并记录两个 Lit 材质官方升级；还原唯一非必要目标设置补写字段；代码、Shader、Compute、场景、Prefab、meta、旧管线资产原字节保留 |
| 受保护内容 | 原有目标 70 文件／源 88 文件逐项 SHA-256 与阶段 0 一致；源 master／HEAD 与四项未提交改动保留；目标包版本与 PC／Mobile 原设置保持 |
| Git 范围／空白 | 暂存仅本阶段 66 文件；新写 Docs/Migration 空白检查通过。全量 git diff --cached --check 返回 2，187 处源资源／原 README／Unity 材质序列化格式提示（185 行尾空白、2 EOF 空行），保留并记录，未谎报通过 |
| Play／GPU／构建／视觉 | **未执行／未验证**；本阶段只完成资源迁入，不称项目可运行 |

完整本地证据：`E:\GithubRepositories\URP_Example\Logs\MigrationPhase1-20261027`；原 Editor.log 切片起点为字节 2363677。可提交的简要导入输出、引用摘要及哈希清单见 [Phase1.md](Phase1.md) 的证据链接。Logs、Library 和大体积日志不提交。

## 下一对话：实施 B／阶段 2～4（待批准）

在相同项目下串行接手现有分支。以下提示由用户发送时才构成阶段 2～4 授权；本轮仅提供入口，未开始实施：

```text
批准实施阶段 2、3、4：CPU／Compute／Indirect、Shader／光照／动画和 RenderGraph 弯曲，按仓库 Plan.md 联合实施与验证。

目标目录：E:\GithubRepositories\URP_Example
源目录：E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res（只读）
目标版本：Unity 6000.6.3f1，URP 17.6.0。

先读取：
E:\GithubRepositories\URP_Example\AGENTS.md
E:\GithubRepositories\URP_Example\Docs\Migration\Audit.md
E:\GithubRepositories\URP_Example\Docs\Migration\Plan.md
E:\GithubRepositories\URP_Example\Docs\Migration\Status.md
E:\GithubRepositories\URP_Example\Docs\Migration\Phase1.md

核对实际目标仓库、迁移分支、HEAD 与阶段 1 交接提交；保留用户已有改动和只读源工作区。
阶段 1 已迁入 26 资源／33 meta；Unity 导入后旧 Configure／Execute 有两项 CS0115，旧 Renderer Feature 不可用。
参考 Evidence/Phase1-*；两个 Lit 材质已有官方升级差异，缺失 FPS／字体与烘焙效果仍留阶段 5。
本轮只批准阶段 2～4，修复共享 C#／Compute／Shader／RenderGraph 契约直到联合验收；不得用旧兼容路径、固定计数或删除效果绕过问题。
启动 Unity 前核对占用，优先使用现有会话；记录真实编译、GPU counter／ID、光照、轨迹纹理与运行证据。
未取得联合证据的项目保留“待联合验证”；结束时更新 Status，列出失败、未验证项和下一步。
允许提交本对话产生的本地改动，不推送远程，不自动进入阶段 5。
```

## 活动记录

### 2026-10-27：阶段 0 获批并开始

- 用户批准保存现有审计、计划、约束及交接规范；允许目标本地分支／文档提交；明确暂不开始阶段 1。
- 已读取 writing-for-agents 技能，采用简短根指引和按需读取的审计／计划／状态文档，保持授权与验收分离。
- 写入前确认目标 main 和干净基线；采集目标 70／源 88 个受保护文件的内容哈希清单供本轮比较。
- 创建分支时一次命令误指向源仓库，产生源 Git 分支切换。发现后已向用户说明；依据 reflog 确认原分支 master，立即切回 master 并删除误建的 codex/urp6-migration 分支。源文件未编辑或提交，原四项未提交改动保留；源 reflog 留有这次操作历史，不把源 Git 元数据宣称为完全未变。
- 随后显式使用 git -C 指向目标，创建目标 codex/urp6-migration 分支；后续 Git 写命令固定使用目标绝对路径。
- 已编写四份文档，检查链接、阶段字段、格式和范围通过；阶段开始前后目标 70／源 88 个受保护文件逐项哈希完全一致，源原分支及四项既有改动保留。
- 暂存范围仅四份文档，git diff --cached --check 通过；以本次文档提交交接，实际 SHA 和最终工作区状态在交接答复给出，下一阶段核对 Git。未启动 Unity，未迁入资源，未开始阶段 1，未推送远程。

### 2026-10-27：阶段 1 获批，资源迁入验收完成

- 用户明确只批准阶段 1，允许本轮产生的本地提交；已读 AGENTS／Audit／Plan／Status，核验分支、干净工作区及阶段 0 交接提交 8350698ed22734ede4ddf59533479e60796241bd。
- 版本四处证据相符；先采集目标 70／源 88 文件 SHA-256 和源 Git 状态，再将源当前工作区的 26 个资源及 33 个 meta 迁入。原 LICENSE／README 归档；源没有编辑、切分支、暂存或提交。
- 目标已由 Unity 6000.6.3f1／DX12／PID 46016 占用；使用该会话自动导入，未启动第二实例或打开源项目。激活会话的 WScript 尝试返回失败，未发出快捷键；后续检查真实 Refresh 日志确认资源导入完成。
- 实际 Unity C# 编译失败：GrassBendingRTPrePass.cs:18／31 两项 CS0115；旧 Renderer 报 missing RendererFeatures。保留旧代码／效果，没有通过删 override、禁用 Feature 或兼容模式消除错误；API 修复不在本轮范围。
- Unity 自动升级两个 Lit 材质，接受并记录版本、队列、颜色／发光数值差异；对唯一目标设置补写字段核对默认值和 diff 后还原原字节。其余 57 个迁入文件、全部原有目标文件保持内容；迁入场景仍未打开，Play／GPU／构建未验证。
- GUID 无冲突，299 个同文件 PPtr 与可检查外部 anchor 均解析；Prefab 100100000 特殊入口及 importer fileID 单独标注。包内 Lit 的 20 处引用、LightingData 四个二进制外部 GUID 均可追溯；已知三处悬空不变。
- 源 88 文件和原有目标 70 文件摘要均与阶段 0 相同，源 master、HEAD、四项既有改动保留。静态证据和实际 Unity 日志节选已写入版本化 Evidence，完整脚本／日志／diff 留本地 Logs。
- 暂存范围 66 文件核对通过；新写 Docs/Migration 的 git diff --cached --check 通过，全量检查因原始／Unity 序列化格式返回 2，共 187 处空白提示，完整结果留本地证据，未为格式提示改写资源字节。
- 以包含本 Status 的阶段 1 本地提交交接；最终 SHA、暂存检查及提交后工作区状态以交接答复为准。阶段 1 仅按资源迁入完成，整体不可运行；阶段 2～4 未获批、未实施，不推送。
