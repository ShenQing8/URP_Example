# 迁移状态与交接记录

更新日期：2026-10-27（Asia/Shanghai）。本文件记录当前执行事实与授权。审计见 [Audit.md](Audit.md)，阶段验收和交接方法见 [Plan.md](Plan.md)，仓库约束见 [AGENTS.md](../../AGENTS.md)。

## 当前摘要

- **当前阶段**：阶段 6 完成，结论限本机 Windows x64／Mono Player 的 DX12／DX11；阶段 2～5 的历史验收保持。报告、真实数据与限制见 [Phase6.md](Phase6.md)，未把未测设备标为完成。
- **当前授权**：阶段 6 实施授权与提交已完成；本轮用户要求复核阶段 6 交接和相关文档，并按 AGENTS 记录复核结论。本轮不启动新增平台／耐久测试，不新增提交或推送。
- **接手 Git**：实际根 `E:\GithubRepositories\URP_Example`；main 跟踪 origin/main；接手 HEAD／阶段 5 提交为 `707212f087763d0c55704166fbe1c870605b1f58`。开始时唯一未提交改动为下方阶段 5 复核记录，全文保留并随本轮 Status 交接。origin 为 `https://github.com/ShenQing8/URP_Example.git`，查询远程默认 main／HEAD 为 `8b79517a4128c21ade3b42f4fb5af4cf3ed267c3`；不切旧分支、不推送。
- **版本与会话**：重新核对 Unity `6000.6.3f1 (45d8eee7de74)`、URP／Core／Shader Graph `17.6.0`、Input System `1.20.0`；全程复用目标 Editor PID 32380，无源编辑器或第二项目会话。最终全部 Player 已正常退出；Editor 为 Edit／SampleScene／dirty=False／PC，C# compilationFailed=False。
- **构建与普通运行**：最终 Release、显式诊断 Development、普通 Development 均由 Unity 成功构建，0 error／0 warning。最终 Release 分别以 DX12／DX11 真实启动，普通 Development 以 DX12 启动，首场景、画面、字体及正常退出通过；普通产物没有诊断类型／复制钩子，即使传入诊断参数也不启动辅助。
- **Player 功能**：DX12／DX11，PC／Mobile 各 8 相机位、GPU counter=Indirect instanceCount、ID 唯一／范围及 CPU 参考集、合批集合、数量至 1000 万、距离边界、0～5003 尾线程、四 Animator／原四轨迹通过。真实 R8 图、PC 深度／法线消费者及 5.351／5.337 秒恢复成立；活动对象组件、19 个有效 Volume 组件、内置字体、18 个 lightmap Renderer、8 个有限 SH 探针和默认反射实际加载。
- **操作**：真实 Windows 鼠标操作及物理反引号扫描码后，统计隐藏／恢复、相机、主光／点光、转速、数量／距离、合批均有 Player 状态读回和截图。测试后未保存场景改动。
- **Profiler**：四组 Player 帧时及短时二进制捕获已保存和离线导入。剔除初始记录扰动后，DX12 PC／Mobile／10M 的 GPU 中位数约 0.750／0.257／0.758 ms；DX11 PC 约 0.639 ms。CPU／GPU／P95／内存完整表在报告／Evidence；总帧周期含限帧等待，Editor 统计未冒充 Player 性能。
- **持续运行／释放**：DX12 20 次禁用／重启、5 次场景往返及实际 600.038 秒持续运行；DX11 5 次禁用、5 次场景往返、20.040 秒短期运行。旧 positions／visible／args 失效、旧 Mesh 销毁和重载恢复通过；后段程序 Mesh=1、RT=14，未见持续增长；新增 Unity Error／Exception／Assert=0。RT 图池保留和退出 Immediate 内存摘要均记录，不宣称全天／引擎零残留。
- **保护**：源 88 文件摘要仍为 `5fc3140fbb1d2d37f9beab286f80d0ea6b78eb0ee367271394ccdbb399626075`，源 master／HEAD／四项既有改动不变；33 原 meta 保留。包、全部现代设置、两个 Lit、旧 GI／烘焙和场景字节保持；本轮唯一既有 Assets 内容变化是把既有观察钩子限定扩展到显式诊断构建。70 Assets GUID 无重复、无包冲突、无缺 meta。
- **保留差异／未验证**：FPS／字体仍为等价替代；两个 Lit 官方升级和局部 BlendProbes／旧 GI 未重烘焙的边界保持。Mobile 0.8 空 Overlay 差异保留原记录，未新做 Player 像素等价验证。Android／Vulkan／Metal／XR／其他设备、RGBA8 fallback、小时／全天耐久及全部驱动瞬态峰值未验证。退出 Immediate 分配摘要未完全归因，见报告，不隐藏为零。
- **证据／提交**：小证据为 Evidence/Phase6-*；完整日志、截图、CSV／Profiler 捕获在 `Logs/MigrationPhase6-20261027`，三个构建在 `Builds/Phase6`，大产物不入 Git。阶段 6 提交为 `f4ce488f14a7c449f93ff0212735f35f0b0c33c0`，包含阶段 5 历史复核记录；本轮仅更新本 Status，保留未提交。
- **下一步**：阶段 6 交接复核通过，本机 Windows 范围的计划验收已完成，无需自动进入额外阶段。新增平台／设备与更长耐久另定范围，不自动推送。启动提示见下方。

### 阶段 6 交接复核（2026-10-27）

- 已核验阶段 6 提交 `f4ce488f14a7c449f93ff0212735f35f0b0c33c0`，父提交为阶段 5 的 `707212f087763d0c55704166fbe1c870605b1f58`。当前 main 跟踪 origin/main，本地跟踪状态领先 2 个提交，复核开始时工作区干净；本轮没有重新查询远程引用。
- 阅读 AGENTS／Status／Plan／Audit／Phase6／README、全部证据类型和 18 文件的阶段差异，检查构建辅助、场景注入、运行诊断、Profiler 分析及 RenderGraph 观察钩子的条件编译。诊断仅进入显式 GRASS_PLAYER_VALIDATION Development 构建；普通 Release／Development 不含诊断类型和图复制钩子。未发现阻挡已执行 Windows 范围验收的问题。
- 独立重算 Phase6-Checks 中 118 份本地原始证据的 SHA-256，全部匹配；4 份已提交文本汇总包含的 29 份原始报告正文逐一匹配。三套构建逐文件重算完整清单、文件数／体积及 EXE／程序集摘要，均与交接清单一致：Release 188 文件／106,458,857 字节，显式诊断 Development 307 文件／173,541,658 字节，普通 Development 307 文件／173,499,659 字节。
- 从四份原始性能 CSV 独立重算去重、剔除初始 15 秒后的统计和采样内存峰值，与 Phase6-Performance.json 完全一致；有效稳定帧分别为 DX12 PC 2699、Mobile 2700、10M 1800 和 DX11 PC 1800。Profiler 无效列未解释为零耗时，限帧周期未解释为 CPU 实际工作量。
- 源 88 文件摘要仍为 `5fc3140fbb1d2d37f9beab286f80d0ea6b78eb0ee367271394ccdbb399626075`，33 个原 meta 哈希保持。相对阶段 5，Packages／ProjectSettings／现代 Assets/Settings 无变化；64 处协作／迁移文档及 README 的本地链接可解析。
- 复核接受的完成范围为本机 Windows x64／Mono、DX12／DX11 Player；保留退出 Immediate 分配摘要未完全归因的限制，不宣称零泄漏。其他设备／图形 API、RGBA8 fallback、小时／全天耐久和 Player 空 Overlay 像素等价仍未验证，不能据本轮结论扩大验收范围。
- 本轮为现有代码、构建和真实执行证据的独立复核，没有重新启动 Unity、构建或运行 Player。仅更新本 Status，保留未提交；没有修改功能代码、资源或设置，没有提交或推送。后续无需必须实施的新阶段，新增测试须先明确设备和范围。

### 阶段 5 交接复核（2026-10-27）

- 已核验阶段 5 提交 `707212f087763d0c55704166fbe1c870605b1f58`，父提交为 `8b79517a4128c21ade3b42f4fb5af4cf3ed267c3`；当前 main 跟踪 origin/main，本地跟踪状态领先 1 个提交，复核开始时工作区干净。本轮没有重新查询远程引用或推送。
- 阅读 Phase5／Status／Plan／README、18 文件的提交范围、统计组件及关键场景／构建入口差异。统计值来自真实帧数和经过时间，内存估计和替代差异有明确说明；未发现阻挡进入阶段 6 的交接问题。
- 逐项重算 Phase5-Checks 中 110 份原始日志／图像的 SHA-256，全部匹配；3 份已提交汇总中的 17 份原始报告内容逐一匹配。查看 640×360 最终 UI 截图及 PC 场景图，与记录一致；这是复核已有产物，不是本轮重新运行 Unity。
- 源 88 文件摘要仍为阶段 0 的 `5fc3140fbb1d2d37f9beab286f80d0ea6b78eb0ee367271394ccdbb399626075`；33 个原 meta 保留。相对阶段 5 接手提交，Packages／现代 Assets/Settings／ProjectSettings 仅构建入口有预期变化；两 Lit 与烘焙资源保持，LightingData 为 23,160 字节，SHA-256 与阶段 1 清单一致。56 处 README／协作／迁移文档的本地链接可解析。
- 阶段 6 应首先在真实 Player 构建和启动中检查内置字体、Input System 操作、首场景、草地绘制及旧 GI 资源；原模板的孤立 Volume 子资源和废弃探针字段继续作为集成检查项，不能仅据 Editor 活动列表有效就宣称 Player 已通过，也不能无条件删除历史残留来规避错误。
- Profiler CPU／GPU 帧时、内存峰值、持续运行与反复启停、DX11 和实际设备均仍待阶段 6；Editor 统计和历史资源包编译不能替代这些证据。本轮没有启动 Unity、构建或新测试，只更新本 Status，保留未提交；阶段 6 接手时保留这份复核记录。

### 阶段 2～4 交接复核与中文 README（2026-10-27，历史记录）

- 本轮用户授权为复核阶段 2～4 的交接和相关文档，通过后编写中文 README；不包含阶段 5 实施。本轮没有启动 Unity 或重跑核心验证，也不把历史测试当作本轮新测试。
- 已核验核心迁移提交 `b22b027790e627a80a02e48a110feed605842448`，分支为 codex/urp6-migration，接手工作区干净。与阶段 1 比较为 40 个文件改动；检查阶段报告、关键 C#／Compute／Shader／RenderGraph 接线及后续阶段边界，未发现阻挡进入阶段 5 的交接问题。
- 独立读取并重算 Checks 中 13 份本地原始测试输出的 SHA-256，全部匹配；PC／Mobile 两份最终 Main Camera 图原始 JSON 的摘要均匹配；82,750 字节 Shader 资源包的 SHA-256 与交接一致。图中弯曲、补充深度／法线、SSAO、Opaque 顺序及消费者依赖与报告相符，Mobile 没有新增法线预通道。
- 源 88 文件清单摘要仍为阶段 0 基线；33 个原迁入 meta 哈希不变。核对目标最初 70 文件相对初始提交的逻辑内容，变化仅为已记录的 PC／Mobile Renderer；版本、Packages 和 ProjectSettings 保持。28 处既有迁移文档本地链接均有效。
- 根目录原无 README.md；本轮新建中文项目说明，覆盖环境、启动场景、IMGUI 操作、渲染数据链、代码入口、实际验证范围、阶段 5／6 待办及原示例许可。保留第三方 SourceREADME.md 和 LICENSE.txt，不用新说明覆盖上游归档。
- 文档校验通过：README／Status 共 27 处本地链接可解析，代码围栏与行尾空白检查通过，git diff --check 通过；实际写入范围仅 README 新文件和 Status 修改。未新增运行测试结果。
- 本轮文档改动为 README.md 与本 Status.md；未修改功能代码、Assets、Packages 或 ProjectSettings，不新增 Git 提交或推送。接手提交仍为 b22b027，下一阶段需识别并保留这两份文档的未提交改动，在获准的提交范围内处理。
- 当前复核结论是“可按计划进入阶段 5”，不是整体迁移完成；FPS／字体、Lit 外观和 GI／烘焙仍未恢复或验收，完整 Player／设备与长期性能测试仍为阶段 6。下一对话继续使用下方阶段 5 入口，核对实际 HEAD，并在阶段 5 完成后同步 README 的状态与限制。

## 阶段总表

| 阶段 | 工作 | 授权 | 状态 | 验证摘要 |
| --- | --- | --- | --- | --- |
| 0 | 基线、审计、计划与交接文档 | 已批准 | 完成 | 文档与受保护基线核验通过 |
| 1 | 资源迁入与引用接线 | 已批准 | 完成（资源迁入） | 26 资源／33 meta；真实导入及旧 API 阻断有历史证据 |
| 2 | CPU／Compute／Indirect | 已批准 | 完成（DX12 联合验收） | GPU 边界、ID／counter／CPU 参考集、合批、位置刷新、相机与 10M 容量实际通过 |
| 3 | Shader／光照／动画 | 已批准 | 完成（当前 Windows 范围） | 两管线实际光照／阴影／动画／SH／纹理；DX11／DX12 资源编译及 DX12 构建资源加载通过 |
| 4 | RenderGraph 弯曲 Pass | 已批准 | 完成（DX12 联合验收） | 最终 Main Camera 图依赖、R8 内容、四轨迹、重叠／交互带／恢复、深度法线与多相机通过 |
| 5 | 演示完整性与缺项恢复 | 已批准 | 完成（Windows Editor／DX12） | 真实统计／内置字体、实际 GUI 操作、两 Lit／旧 GI 对照及缺项引用通过；无需重烘焙 |
| 6 | 构建、性能与最终运行 | 已批准 | 完成（Windows x64／DX12／DX11） | Player 构建／操作／GPU／Profiler／释放；DX12 持续 600 秒，其他设备未验证 |

当前没有未取得证据却标为成功的联合项目：各阶段完成限于各自报告范围，阶段 6 新增 Windows Player／DX11／DX12 证据；其他设备与更长耐久不借此标记完成。以下阶段 0／1 章节保留历史事实，授权与当前进度以上述摘要及最新活动记录为准。

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

## 未解决事项（阶段 1 交接时的历史状态）

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

## 下一对话入口（Windows 验收及交接复核已完成）

```text
继续 Unity 草地项目，具体新任务由本次用户说明；现有 Windows 验收及交接复核已完成。
目标为当前仓库根（本机 E:\GithubRepositories\URP_Example）；源 E:\GithubRepositories\URP_MeshInstancedIndirectExample\Res 保持只读。
先读 AGENTS.md、Status.md、Plan.md、Phase6.md，并核对实际根、origin、main／origin/main、HEAD 和工作区。
阶段 6 提交为 f4ce488f14a7c449f93ff0212735f35f0b0c33c0；保留阶段 5／6 的历史复核记录，识别本 Status 尚未提交的复核更新。
当前完成范围为本机 Windows x64 Mono Player／DX12／DX11，含真实 GPU、Profiler、资源释放和 DX12 600 秒持续运行。
保留 Mobile 0.8 Overlay、局部 BlendProbes、材质／字体替代和 Immediate 内存摘要的边界；不宣称其他设备或全天耐久通过。
不自动开始新阶段；新增平台／设备测试需明确实际设备和授权范围，提交／推送按用户明确授权执行。
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

### 2026-10-27：阶段 2～4 获批并完成 Windows 联合验收

- 用户明确批准三阶段联合实现和验证；核验目标分支、干净接手状态与阶段 1 提交 8d23a576。源及其四项改动保持只读，没有启动源 Unity 或目标第二会话。
- 修复共享位置／Cell／Compute／args／Indirect ID 契约、相机与释放、三组风和光照，改用 RenderGraph 独立俯视剔除及 R8 图；颜色／深度／法线共享变形。真实 GPU 测试、主相机图、资源编译与构建资源实际加载通过，详见 Phase234。
- 记录并修正实际中间 C#／测试错误，未用旧 Configure／Execute、兼容模式、固定计数或删效果通过。最初误标 Mobile 的同回调质量切换结果作废；实际管线分帧复测通过。
- 保留 Probe Blend、原根部上下 0.5m 交互范围；四原轨迹与真实 5.204 秒恢复通过。关闭草地层后禁止消费缓存深度。叠加相机原生 0.8 缩放差异经无草地／临时 1 对照定位，交付比例保持。
- 更正 Audit 的目标 Mobile 比例：阶段 1 HEAD 为 0.8，PC 为 1。官方升级旧管线资产和自定义材质序列化，材质 Shader 数值逐项不变；现代配置保留。丢弃构建生成的 PC_RPAsset 编译预筛选字段并恢复准确初始字节。
- 最终源 88 文件／分支／HEAD／状态一致，33 原 meta 一致；目标初始 70 文件仅两现代 Renderer 预期改变，Packages／ProjectSettings 不变；GUID／meta／包冲突检查通过。已恢复 SampleScene 并退出 Play，编辑器保持运行。
- 仅提交本轮代码、Renderer、官方序列化差异、验证辅助及报告／小证据，不提交大日志、截图、资源包或缓存，不推送。阶段 5 尚未获批；最终提交 SHA 与工作区状态在交接答复给出。

### 2026-10-27：用户发布远程仓库，更新协作入口

- 用户说明阶段 5 尚未开始，已将项目推送至 https://github.com/ShenQing8/URP_Example.git，并授权直接调整此次发布影响的文件；未授权开始阶段 5 或由代理新增远程推送。
- 本轮开始核验：当前 main／origin/main／远程 main 均为 `d52ece0d33408bb2b7b85d50a2bc20faf5dc1623`；远程默认分支 main；本地工作区干净。原 codex/urp6-migration 本地分支同指 d52ece0，不切分支、不重写历史、不修改 remote 配置。
- README 新增当前仓库身份、克隆步骤和异机路径／缓存／证据说明；AGENTS 将仓库内读取路径相对化并明确核对 origin／上游、按实际根写入和推送授权；Plan 更新发布后的 main 接手与可回退历史基线；Status 更正旧“未提交”状态并更新阶段 5 提示。
- 保留 Audit／Phase1／Phase234、原始 Evidence、第三方 README／许可中的历史路径、分支和“本轮未推送”等事实；用户本次推送不追溯改写代理历史，也不等于验证阶段 5 或 Player 成功。
- .gitignore 现有规则适用；git ls-files 未包含 Library、Temp、Logs、UserSettings、Obj、Build 或 Builds 路径。未因发布仓库改写 Unity 功能代码、管线设置或包依赖。
- 二进制属性扫描发现 .gitattributes 的 *.asset text 覆盖到 LightingData.asset；本机 core.autocrlf=true，Git filters 将原始 23,160 字节变为 23,175 字节，工作区与该转换结果完全一致。HEAD 原始 blob 的 SHA-256 与阶段 1 迁入清单相同，证明是检出换行变化，不是新的烘焙内容。补充 **/LightingData.asset binary，并恢复精确原始字节；本轮不重烘焙、不修改 GI 参数、不宣称 GI 外观验收通过。
- 验证通过：30 处本地文档链接可解析，格式及 git diff --check 通过；全部跟踪二进制扫描未再发现强制 text。LightingData 的工作区／HEAD／Git filters 模拟检出字节均相同，大小 23,160，SHA-256 `72d97f8980b2c1eab0d82aacdc26417a61d24668086cfcd1e056934a6c98e7ae`，与阶段 1 清单一致。仅针对该文件重算索引属性／状态，暂存内容仍为空。
- 本轮新增 Git diff 限五个文件：四份文档及 .gitattributes；LightingData 恢复为既有提交内容，历史 Evidence 不改。HEAD／分支／origin 均未修改，改动留本地待后续获准提交和推送后生效于远程。未启动 Unity，未运行新 GPU／构建测试；阶段 5 仍未开始。

### 2026-10-27：阶段 5 获批并完成演示完整性验收

- 用户明确批准阶段 5、本地提交、不推送；接手实际 main／origin/main／远程 main 为 8b79517，干净工作区。发布后的四文档与 .gitattributes 已在该提交中，保留其内容；没有切回历史分支。
- 新增 DemoPerformanceStats 及内置字体接线，恢复真实统计和显隐；原缺失插件／字体不可得，差异明确记入 Phase5。四处 GUI 使用小屏缩放和状态恢复，MoveCamera 按钮路由与空引用保护；天空只清闲置悬空 PPtr，演示加入构建首场景。
- 复用 PID 32380／Unity 6000.6.3f1／URP 17.6／DX12。最新 Unity C# 与 Shader 检查、真实 GameView 鼠标／Input System 快捷键、两管线／两尺寸、8 相机、四角色、数量到 10M、GPU counter／args、合批／重启、轨迹及 5.204 秒恢复通过。不是 Player／Profiler／设备验收。
- 两 Lit 按已安装官方 UpgradeV2／V3 解释并实测对照；queue 2000／2050 无可见差异，_Color 变化接近零，旧发光的 linear 转换与现值精确相同；材质维持接手原字节，不声称 Unity 2019 外观相同。
- 目标 LightingData isValid、1 组贴图／18 Renderer、8 SH 探针／默认反射加载及真实着色响应成立；SH 归零／恢复和光照贴图归零对照通过，决定无需重烘焙。保留局部探针边界，烘焙文件及 binary 属性原字节不变。
- 记录并修正两个辅助错误：初次 inspect 误读 SampleScene 空 LightingSettings，以及首轮小尺寸清理使用错误索引。前者改为安全读取，后者不计最终通过；修正后重跑并检查 Unity 错误数，无新增错误。没有清除历史失败来宣称成功。
- 最终只读源 88 文件／Git 状态、原 33 meta、包与现代管线、两个 Lit／全部烘焙资产保持；仅新增获准的构建入口。退出 Play 并恢复 PC／1280×720／SampleScene，无并发编辑器。
- 最终扫描 408 处文本 GUID，演示无悬空；另外分类保留原模板 4 个未列入 components 的缺脚本孤立子资源和 7 个已废弃探针调试资源字段。实际默认 Profile 的 19 组件全部有效；没有借删除现代设置清空静态残留。
- 本地提交只含本阶段代码、场景／入口、统计和字体接线、报告／小证据；完整日志与截图留忽略目录。提交 SHA 由最终答复和 git log 定位；不推送。阶段 6 尚未获批。

### 2026-10-27：阶段 6 获批并完成 Windows Player 验收

- 接手 main／707212f，唯一未提交的阶段 5 复核记录已保留。用户明确批准阶段 6、必要实现／修复、本地提交及中文 Summary，不推送；全程目标单会话、源只读。
- 新增显式构建请求、诊断场景处理器、Player 验收辅助和离线 Profiler 分析；只有显式诊断构建有自定义定义及复制钩子。普通 Development 的门控另做实际构建／启动验证，无 Missing Script；未改生产 GPU 计数、Shader 变形、画质或旧 GI。
- Unity 构建和最终普通产物启动通过。DX12／DX11 的 PC／Mobile 8 视角、GPU ID／counter／args／CPU 参考、容量／距离／合批、真实原轨迹与恢复、字体／GI／默认 Volume、真实鼠标／反引号操作均有证据，详见 Phase6。
- Profiler CPU／GPU 数据来自四组实际 Player 采样及导入的 Player 二进制捕获；无效 recorder 列不解释成 0 成本，CPU Cell 从 RawFrameDataView 提取，计时窗口没有同步 GPU 读回。
- DX12 完成 20 次启停、5 次场景往返和 600.038 秒持续运行；DX11 5 次启停、5 次场景往返和 20.040 秒短期运行。旧 GPU buffer／Mesh 释放断言和实际重载通过；内存峰值、RT 图池稳定性及 Immediate 退出摘要分别留证，不承诺全驱动／全天无泄漏。
- 保留早启动、辅助 CS0122／CS0165、CPU 读取图全局状态错误和首轮 OS 按键未隐藏等失败；均修正或按证据分类重跑。官方未启用 DOF／Panini 剥离信息保持，不改包或屏蔽 Shader 错误。
- 构建自动生成的预筛选、运行列表、Graphics 默认字段、Input System preload 和测试包临时资源已核对；现代配置恢复准确接手字节。源 88 文件、33 原 meta、材质／场景／烘焙／包保持；新增 4 个脚本及 meta，唯一既有代码改动为观察钩子的显式构建条件。
- 已更新本 Status、Phase6 报告、README 和小证据；大产物保留忽略目录。阶段完成限定 Windows 已执行范围，Android 等实际设备未验证。阶段本地提交和最终工作区以交接答复及 git log 定位；没有推送。
