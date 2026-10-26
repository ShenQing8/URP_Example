# Unity 6 URP 间接实例化草地示例

将 ColinLeung-NiloCat 的 Unity 2019.4／URP 7 草地示例迁移到 **Unity 6000.6.3f1／URP 17.6.0**，演示 CPU Cell 剔除、GPU Compute 剔除、间接实例化绘制，以及通过 RenderGraph 实现的草地交互弯曲。

**当前状态（2026-10-27）**：阶段 2～4 已完成 Windows Editor／Direct3D 12 核心联合验收；演示完整性恢复为阶段 5，完整 Player 构建和最终运行验收为阶段 6。核心功能已在 Unity 中实际运行，整个迁移项目尚未完成最终验收。最新进度以 [迁移状态](Docs/Migration/Status.md) 为准。

## 环境与渲染配置

| 项目 | 当前配置 |
| --- | --- |
| Unity | 6000.6.3f1 |
| Universal Render Pipeline | 17.6.0，与 Core／Shader Graph 配套 |
| PC | Forward+、Render Scale 1，保留 SSAO |
| Mobile | Forward、Render Scale 0.8 |
| 自定义渲染通道 | RenderGraph，不依赖旧 Configure／Execute 或兼容模式 |
| 已运行验证环境 | Windows Editor、Direct3D 12、NVIDIA GeForce RTX 4060 Laptop GPU |

运行设备需要支持 Compute Shader、GPU Instancing、间接绘制及本项目 Shader 所需能力（草地 Shader target 4.5）。**Mobile 是一套 URP 配置的名称，不代表 Android 实机已经验证通过。**

## 快速运行

1. 使用 Unity Hub，以 **Unity 6000.6.3f1** 打开本仓库根目录，等待 Package Manager、资源导入和脚本编译完成。
2. 在 Project 窗口打开演示场景：

   ```text
   Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/TestScene/scene.unity
   ```

3. 确认当前质量配置使用目标现代 URP 资产；Windows 默认使用 PC 配置。PC／Mobile Renderer 均已接入 `Grass Bending and Depth (RenderGraph)` Feature。
4. 点击 Play，在 Game 视图观察草地、角色轨迹和控制界面。

默认是 **100 万株草、绘制距离 125**。提高数量或距离会增加内存及计算／绘制负担，建议逐步调整。

项目保留模板 `SampleScene`，当前 Build Settings 的入口仍是该模板场景。只打开项目或直接按现有 Build Settings 构建，不会自动进入草地演示；完整构建入口将在后续阶段验收。

场景中的原 Advanced FPS Counter 脚本和字体尚未恢复，可能显示相应缺失引用；它们已列入阶段 5。看到这些遗留提示不应删除组件来隐藏问题，也不应将其误认为核心迁移已经通过完整场景验收。

## 演示操作

控制界面沿用原示例的 IMGUI，主要面向 Game 视图中的鼠标／指针操作。

| 控件 | 用途 |
| --- | --- |
| Instance Count | 在 100 万～1000 万实例之间调整，保持示例的生成密度 |
| Draw Distance | 在 25～200 之间调整绘制距离 |
| shouldBatchDispatch | 开关连续可见 Cell 的 Compute 调度合批 |
| Move camera | 在 8 个预设相机位置之间平滑切换 |
| Main Light On/OFF | 开关主方向光 |
| PointLight Light On/OFF | 开关点光源 |
| Light Rotate Speed | 调整主方向光转速 |

四个角色由循环动画自动移动，其 TrailRenderer 在草地上留下弯曲区域。轨迹寿命约 5 秒，颜色随时间变化使草地逐渐恢复；无需键盘控制角色。

Cell／dispatch 标签显示实际调度统计。`Application.targetFrameRate = 60` 只是目标帧率设置，不代表已经测得或保证 60 FPS。

## 实现流程

```text
世界坐标位置生成
  -> Cell 分桶、排序与位置 buffer 上传
  -> 每相机 CPU Cell 视锥／距离粗剔除
  -> GPU Compute 实例剔除，Append 可见实例 ID
  -> CopyCount 将真实可见数量写入间接参数
  -> Graphics.RenderMeshIndirect 绘制草地

角色动画 -> TrailRenderer
  -> RenderGraph 俯视绘制 512×512 弯曲图
  -> 草地颜色／深度／法线几何使用一致的弯曲与风动画
```

- **CPU 与 GPU 两级剔除**：CPU 筛选 Cell，GPU 再判断实例；连续 Cell 可合并调度。Compute 同时检查本次任务长度和总实例范围，支持空数据、非整线程组数量和退化区域。
- **真实间接参数**：使用 `GraphicsBuffer.IndirectDrawIndexedArgs`；实例数来自 GPU Append counter，经 `CopyCount` 写入，没有用固定数量代替剔除结果。
- **草地外观**：保留程序化三角形、随机宽高、朝向相机、远距离增宽、颜色渐变、三组风、半 Lambert／透光效果、高光、环境 SH、主光／附加光、接收阴影及雾。
- **交互弯曲**：俯视独立剔除轨迹，白底 R8 纹理通过乘法混合记录踩踏强度；设备不支持所需 R8 渲染／混合能力时实现了 RGBA8 分支，该分支尚未在已验证设备上实测。
- **RenderGraph 依赖**：弯曲图在草地消费者之前生成；PC 的草地补充深度／法线通道显式声明纹理和 buffer 读取，随后供 SSAO 使用。Mobile 不强制增加其原本没有的法线预通道。
- **相机与资源**：Game／SceneView 使用独立可见集；Overlay 不重复提交草地或建立可变草地 buffer；组件禁用时释放 GPU buffer 和程序 Mesh。

光照为适配 Forward+ 移到了片元阶段，性能成本与原版顶点光照不同。原草地没有 ShadowCaster Pass，本项目也未将“草叶投射阴影”作为已有功能；接收其他物体阴影仍被保留。

## 代码与资源入口

| 文件／目录 | 作用 |
| --- | --- |
| [InstancedIndirectGrassRenderer.cs](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/InstancedIndirectGrassRenderer.cs) | Cell、逐相机剔除、GPU buffer、Indirect 提交和生命周期 |
| [InstancedIndirectGrassPosDefine.cs](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/InstancedIndirectGrassPosDefine.cs) | 位置生成、密度及数量／距离控制 |
| [CullingCompute.compute](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/CullingCompute.compute) | GPU 实例剔除与可见 ID 输出 |
| [InstancedIndirectGrass.shader](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/InstancedIndirectGrass.shader) | 草地颜色、深度和法线 Pass |
| [GrassCommon.hlsl](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/GrassCommon.hlsl) | 所有草地 Pass 共用的几何变形和光照逻辑 |
| [GrassBendingRTPrePass.cs](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/GrassBending/GrassBendingRTPrePass.cs) | RenderGraph 弯曲图与补充深度／法线通道 |
| [GrassBending 资源](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/GrassBending) | 轨迹 Shader、材质和 TrailRenderer Prefab |
| [TestScene](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/TestScene) | 演示场景、角色动画、相机与灯光控制 |
| [Assets/Settings](Assets/Settings) | 实际生效的现代 PC／Mobile 管线及 Renderer 配置 |
| [Editor 验证辅助](Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Editor) | 在现有 Unity Editor 会话中执行 GPU、渲染和资源构建验证 |

TestScene 下保留的旧 `UniversalRenderPipelineAsset` 及 Renderer 仅供迁移对照，**不是当前有效管线**，不要用它们替换现代 PC／Mobile 配置。

外部代码可通过 `SetPositions` 替换世界坐标列表；原地修改列表内容后调用 `MarkPositionsDirty`，使同数量位置修改也能重新上传。

## 已执行的验证

以下是阶段 2～4 保存的真实验证结果，不是本 README 编写时重新运行的测试。完整方法、失败纠正和证据入口见 [阶段 2～4 交接报告](Docs/Migration/Phase234.md)。

| 范围 | 已验证内容 | 边界 |
| --- | --- | --- |
| GPU 数据链 | 0／1／63／64／65 等边界、可见 ID 范围与唯一性、counter 与间接实例数一致、合批集合与 CPU 参考集一致 | Windows Editor／DX12 |
| 容量与刷新 | 实际 1000 万位置及可见 ID buffer 分配／剔除／释放、同数量位置刷新、三次禁用重启 | 不等于帧率、长期稳定性或驱动峰值测试 |
| 光照与动画 | PC Forward+／Mobile Forward 的主光、点光、阴影、风、SH、雾、颜色纹理与草形对照 | Mobile 配置在 Windows DX12 上测试，不是移动设备结果 |
| 交互弯曲 | 四条原轨迹投影、画外／多相机、重叠乘法、无轨迹白底、真实经过约 5.2 秒后的恢复 | 当前设备使用 R8 |
| 图与相机 | PC 深度法线／SSAO 依赖、8 个相机位置、Overlay 和层掩码隔离 | 实时反射相机、XR 未验证 |
| Shader／Compute 资源 | Windows DX11／DX12 资源包严格编译，DX12 实际加载构建资源后验证绘制与剔除 | DX11 未切设备运行；资源包不是完整 Player |

小体积文本／JSON 证据保存在 [Docs/Migration/Evidence](Docs/Migration/Evidence)。完整日志、截图与资源包在本机 `Logs/MigrationPhase234-20261027`，由 Git 忽略，不随克隆自动提供。Editor 验证辅助的复跑方法见交接报告；普通运行无需创建测试请求文件。

## 后续工作与已知限制

**阶段 5：演示完整性与缺项恢复。**

- 恢复原 Advanced FPS Counter／字体，或以真实运行数据实现等价统计并记录差异。
- 对照两个 Lit 材质自动升级后的渲染队列、颜色和发光值，验证场景外观；尚未证明与 Unity 2019 原版一致。
- 检查旧 LightingData、GI 与 LightProbes 的实际有效性，必要时在目标项目重烘焙；保留 BlendProbes 不等于已经验证旧烘焙效果。
- 完整检查演示操作、剩余资源引用和构建场景入口。

**阶段 6：最终构建与运行验收。**

- 完整 Windows Player 构建与启动运行。
- Profiler CPU／GPU 帧时间、内存峰值及长时间启停／运行稳定性。
- 按实际设备条件验证 DX11、Android、Vulkan 等目标；Metal／XR 也没有运行结论。

Mobile 的原始 0.8 Render Scale 在空 Overlay 对照中存在已记录的边缘采样差异，禁用草地也可复现；临时比例 1 的对照消除了该差异。当前保留原配置，详见阶段 2～4 报告，不将这一现象混同为已通过逐像素一致性验收。

原项目 README 中的历史手机帧率和截图不作为本迁移的性能承诺。1000 万容量测试通过，也不代表所有设备都适合该规模。

## 迁移文档

- [审计报告](Docs/Migration/Audit.md)：原功能、资源引用、版本差异和风险。
- [实施计划](Docs/Migration/Plan.md)：七阶段范围、验收标准及交接规范。
- [当前状态](Docs/Migration/Status.md)：最新阶段、限制与下一对话提示。
- [阶段 1 交接](Docs/Migration/Phase1.md)：资源迁入及旧 API 阻断的历史证据。
- [阶段 2～4 交接](Docs/Migration/Phase234.md)：核心实现、联合验收与平台边界。
- [代理协作约定](AGENTS.md)：源只读、授权范围、功能保留及真实验证要求。

## 原项目与许可

原示例作者为 **ColinLeung-NiloCat**，Copyright © 2020；迁入的原示例遵循 MIT 许可。原许可及原 README 原字节归档，保留署名：

- [MIT 许可原文](Docs/ThirdParty/URPMobileGrassInstancedIndirectDemo/LICENSE.txt)
- [原项目说明与上游链接](Docs/ThirdParty/URPMobileGrassInstancedIndirectDemo/SourceREADME.md)
