# 迁移状态与交接记录

更新日期：2026-10-27（Asia/Shanghai）。本文件记录当前执行事实与授权。审计见 [Audit.md](Audit.md)，阶段验收和交接方法见 [Plan.md](Plan.md)，仓库约束见 [AGENTS.md](../../AGENTS.md)。

## 当前摘要

- **当前阶段**：阶段 0 完成，以包含本状态文件的阶段 0 本地提交作为交接点。
- **整体迁移**：尚未实施；目标仍为原 URP 模板，没有迁入源资源。
- **当前授权**：用户仅批准阶段 0，允许创建目标本地迁移分支、写入下列四份文档并提交；不推送远程；暂不开始阶段 1。
- **下一阶段**：阶段 1／实施 A，尚未获批。等待用户在实施对话明确授权。
- **目标目录**：`E:\GithubRepositories\URP_Example`。
- **源目录**：`E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res`，资源与仓库保持只读；阶段 0 操作纠正见活动记录。
- **目标版本**：Unity 6000.6.3f1／URP 17.6.0；源为 2019.4.15f1／URP 7.3.1。
- **迁移分支**：`codex/urp6-migration`（目标仓库）。
- **本阶段接手 HEAD／回退基线**：`42c1fde2ef37cde1348cd587c8215494d99e9356`，初始分支 main，开始时目标工作区干净。
- **阶段 0 交接提交**：用 `git log -1 -- Docs/Migration/Status.md` 定位包含本文档的阶段 0 提交；交接答复提供实际 SHA，下一对话核验并记录接手 SHA。本文不填写自身提交哈希。

## 阶段总表

| 阶段 | 工作 | 授权 | 状态 | 验证摘要 |
| --- | --- | --- | --- | --- |
| 0 | 基线、审计、计划与交接文档 | 已批准 | 完成 | 版本、文档引用、阶段字段、四文件范围及受保护内容哈希检查通过；以文档提交交接 |
| 1 | 资源迁入与引用接线 | 未批准 | 未开始 | 未导入源资源 |
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

## 验证记录

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
| 旧 Pass、剔除边界、Forward+、Shader 和弯曲纹理契约 | 静态审计已定位，等待阶段 2～4；当前没有任何迁移运行成功结论 |
| FPS Counter 脚本和字体缺失 | 等待阶段 5 恢复或基于真实数据做等价实现；原样字体／插件不可得时必须记录差异 |
| 旧 Preset、Timeline 设置及闲置天空贴图引用 | 已分类，不将旧设置覆盖到目标；后续按是否为运行依赖处理 |
| LightingData 在 Unity 6 的有效性 | 静态外部引用齐全，但导入／效果未验证；必要时在目标重烘焙 |
| 验收平台 | 尚未新增用户选择；计划优先 Windows，Android 单独验证，不承诺尚无证据的设备结果 |
| 目标编辑器占用 | 历史观察到有 Unity 进程，本阶段未启动或关闭编辑器；实施时重新核对项目锁和可用会话 |

## 下一对话：实施 A／阶段 1

阶段 0 提交并完成校验后，由用户在相同项目下创建 Local 对话，串行接手现有分支。以下提示由用户发送时才构成阶段 1 授权：

```text
批准实施阶段 1：资源迁入与引用接线，按仓库 Plan.md 执行。

目标目录：E:\GithubRepositories\URP_Example
源目录：E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res（只读）
目标版本：Unity 6000.6.3f1，URP 17.6.0。

先读取：
E:\GithubRepositories\URP_Example\AGENTS.md
E:\GithubRepositories\URP_Example\Docs\Migration\Audit.md
E:\GithubRepositories\URP_Example\Docs\Migration\Plan.md
E:\GithubRepositories\URP_Example\Docs\Migration\Status.md

核对实际目标仓库、迁移分支、HEAD 与阶段 0 交接提交；保留用户已有改动。
只实施阶段 1，按资源迁入验收；明确记录预期编译阻断，不宣称项目可运行。
执行实际可做的导入／引用检查，更新 Status 并给出下一阶段提示。
允许提交本对话产生的本地改动，不推送远程，不自动进入阶段 2。
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
