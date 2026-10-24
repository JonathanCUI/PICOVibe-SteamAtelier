# 注视高光与 FPS

## 当前状态：0.3.4 跟随已通过，0.3.5 调整可读性

0.3.4（8）于 2026-10-24 23:20:51 安装到 Swan，PID 30792。官方输入初始化/模式同步成功，头部姿态进入 Stage 中可用的坐标范围；两个不同视角的截图中，FPS 均停留于相同左上位置，日志记录转头超过 90°及位置变化。用户明确确认「现在能跟随视野」。文件为 `Builds/Evidence/v034-spatial-hud-a.png`、`v034-spatial-hud-b.png` 和 `device-v034-hud.log`。短时崩溃缓冲为空，45 组零件加载完成。

截图同时暴露文字偏暗、靠左裁切，0.3.5 将头部局部偏移从 (-0.48, 0.31, 1.1) 改为 (-0.32, 0.25, 1.1) 米，并提高字形材质基础颜色，保留尺寸、无背景、无碰撞体和相同追踪路径。0.3.4 Play Mode 回归通过；0.3.5 的视觉微调以构建及真机截图检查，不重复增加镜像实现的测试。最新安装与截图状态见 `Builds/SwanAtelier/delivery-verification.json`。

0.3.5（9）已于 23:25:36 安装启动，PID 300。构建 0 错误，45 组零件就绪、XR 输入正常、短时 crash buffer 为空。`Builds/Evidence/v035-spatial-hud.png` 已确认完整 `APP 90 FPS` 在左上方，无背景、无裁切；字形风格未另请用户确认。APK SHA-256 `96DE0728B2BE254CC3BA1B5412C43B8449F733E9F6F7C6FF779EE34D723835A2`，283161788 字节。所有版本验收分别留档于 `Builds/Releases`。

用户明确反馈 0.3.3「看不到 FPS，或位置不对」，所以此前各版的姿态日志、有效节点 ID、编辑器数学检查均不代表视野跟随成功。下文 0.2.x 至 0.3.3 内容为历史记录，不能覆盖此反馈。

0.3.4 增加同一本地 PICO Unity SDK 的 `com.bytedance.pico.xr` 6.1.1。Android 只启用 PXR_Loader，项目保持 `isSpatialAdapter=true`；该模式只启动 PICO XRInputSubsystem，继续由 SpatialAdapter 6.1.0 渲染。通过公开 Unity XR API 读取 Head 的 centerEye/device 位置和旋转，不再将原始 `GetHMDPose` 直接当成场景头部姿态。尝试使用输入系统支持的 Floor origin；无有效追踪时隐藏 HUD，避免把静态回退位置误当作跟随。

`SwanSpatialInputMode` 在 Android Player 的 Start 调用公开的模式同步方法。SDK 自带同步组件在 Windows Play Mode 也访问 Android JNI，已在本项目替换为平台限定调用，没有修改 vendor。网格文字、无背景、独立根节点及原有交互保留。

当前假设是此前原生 HMD 坐标与 Stage/Unity 坐标不一致。知识库只返回 Kotlin HMD/空间坐标文档，不能直接证明 Unity 的转换公式；因此用同版 SDK 的官方 Spatial 输入路径做真机验证，而非凭空补偿头部高度或朝向。日志标记 `SWAN_ATELIER_XR_SPATIAL_MODE_SYNCED`、`SWAN_ATELIER_XR_INPUT`、`SWAN_ATELIER_HUD_TRACKING_READY source=UnityXR`。APK 构建、输入值变化、画面位置及用户确认要分别记录。

## 历史：0.2.2

只修改 `SteamAtelierSwan`；保持 `com.picovibe.steamatelier` 包名，更新版本为 0.2.2（3）。旧 `SteamWorkshop` 工程和 `com.picovibe.steamworkshop` 不变。早期安装包分别保存在 `Builds/Releases/0.2.0`、`Builds/Releases/0.2.1`。

## 高光

使用项目 PICO SpatialAdapter 6.1.0 的公开 `SpatialAdapterHoverEffect` 组件，由系统处理注视、指向、近手悬停反馈；不读取原始眼动数据，也不以头部射线冒充眼动。

- 场景中 157 个交互目标，包括 24 个实体控制台/零件盘控件、机车部件和两个辅助按钮。
- 车身的 140 个可见网格目标直接带盒碰撞体与高光组件；零件父节点保留高光分组，不再使用覆盖子网格的大包围盒拦截视线。命中子网格后仍通过父级定位原拆装组，保留拆装逻辑。
- 45 个拆卸零件 prefab 同样把命中与高光挂在实际网格上，根节点保留分组效果。
- 整个工作台和整车根节点不添加高光，避免一个小控件触发全场景发亮。
- 只有文字的四个分类选项增加独立小底板，提供可显示高光的表面。新增 4 个网格渲染对象、48 个三角形；不复制整车网格，不使用逐帧材质克隆。

## FPS 与位置

`SwanFrameRateCounter` 按真实经过时间统计每半秒的 Unity Update/LateUpdate 次数；不受 `Time.timeScale` 影响。暂停后清空窗口，恢复后重新统计。较长帧会计入结果。

`SwanFrameRateHud` 显示 APP FPS 和 ms/帧。Swan 使用原生合成渲染，此数值是应用更新频率，不能用来代替原生渲染 FPS、合成器 FPS、GPU 耗时或屏幕刷新率。

位置使用当前 SDK 的公开 `SpatialAdapterRuntime.GetHMDPose(ref Pose)`，通过 `SpatialCamera.GetRootTransform().inverse` 转到 Unity 场景空间。面板固定在头部坐标左上方约 23°/16°、前方 1.1 米；独立于可拖动工作台。面板没有 Collider，不抢夺指向或捏合。

取到第一份有效头部姿态前，面板暂位于应用左上方；若 SDK 没有提供有效姿态，会保留该位置并记录诊断，不把静态面板宣称为已实现真机跟随。日志标记：`SWAN_ATELIER_HUD_TRACKING_READY`、`SWAN_ATELIER_HUD_POSE_UNAVAILABLE`、`SWAN_ATELIER_FPS`。编辑器使用预览相机验证布局。

## 接口核实与验证边界

0.2.1 已安装到 Swan。用户先报告高光与 FPS 跟随正常，随后明确补充：只有按钮高光正常，车身零件没有高光。因此不能把 0.2.1 记为车身高光通过。该次应用日志确认按钮操作与 APP FPS 采样运行，但 `headPose=False`；排查发现 `IsSessionInitialized()` 返回的是状态码，SDK 自身帧循环以 `0` 为会话就绪。0.2.2 已改用相同判断，并将车身/拆卸零件的高光与命中绑定到实际网格。

知识库返回的 6.0 Kotlin 文档说明原生 HoverEffect 的触发条件与子级传播，但不覆盖本项目的 Unity API。实际使用的 Unity 类型、公开成员已在本地 6.1.0 SDK 核实，记录见 `Builds/Evidence/spatialadapter-public-api.txt`。只调用 SDK 已公开的方法，没有自行声明私有 JNI/PInvoke 接口，也没有更改 vendor SDK。

自动检查覆盖高光目标/碰撞体/视觉表面、动态拆卸零件、60/90 FPS 时间采样、长帧、暂停重置、坐标转换及 HUD 不拦截输入。实际眼动亮起效果、头部运动时的跟随方向与稳定性，需要在 Swan 真机确认。

0.2.2 Play Mode 检查通过，包括新增的网格/碰撞体/高光同节点检查，以及 45 组零件拆卸、拒绝错误位置、重新吸附。最新 APK 校验与真机状态见 `Builds/SwanAtelier/delivery-verification.json`，视觉高光仍需与真机反馈分别记录，不能用组件数量代替验收。

0.2.2 APK 构建成功（0 个错误），包名/应用名/ARM64 校验通过，大小 265,419,090 字节，SHA-256 `F1A429D9A360F1DB201361E1EDC303E8EE5C150C323E09EC7264D8BBFC96F049`。Swan 重连后，于 21:21 安装成功，系统确认 0.2.2（3）。PID 12112 日志确认 45 组零件就绪、HMD 首次姿态成功，并持续返回 `headPose=True poseResult=0`。短时 APP FPS 样本为 60.9 / 88.7 / 89.9 / 61.8，这些是采样窗口，不代表长期性能基准或合成器帧率。该进程 crash buffer 查询为空，独立旧版仍为 0.1.2。用户随后在 Swan 真机确认：「车身高光与 FPS 跟随都正常」。车身高光与 FPS 视野跟随验收通过；该结论来自用户视觉反馈，不扩展为长期性能或所有交互场景验收。日志：`Builds/Evidence/device-v022-unity.log`。

真机诊断留档：0.2.1 部分运行期间有原生 `MeshModelBuilder: Need Positions` / `Empty Indices` 高频日志；历史退出记录为 SIGNALED/status 9，尚不能确定退出发起者。之后观察到进程持续运行、控制按钮操作及 APP FPS 采样。此处不把资源日志或退出原因宣称为已修复；原始证据在 `Builds/Evidence/device-v021-*.log`。

环境诊断：CLI 0.6.1（public），插件 0.5.0；doctor 按 Spatial 环境报告 SDK 6.0.0、知识库 6.0、模拟器 6.1.0、Editor 6.1.9。Unity 项目实际 SpatialAdapter 版本为 6.1.0。诊断提示性能分析工具依赖不完整、插件最新状态无法确认；本次未升级 SDK、插件或修改全局设置，设备安装不依赖这些可选性能工具。

## 0.3.1 修订与验收更正
用户最新反馈：0.3.0 双手移动、旋转、缩放、松手正常，但 FPS 没有随视角移动。因此此前 0.2.2 的 FPS 视觉通过记录不能作为当前功能通过证据。API 返回 0 也不能代替实际姿态变化或画面验收。0.3.1 改为小号单行 APP FPS、透明背景、固定柔和文字颜色；新增实际头部位置/旋转变化与 HUD 世界姿态日志以定位跟随问题。

## 0.3.2 直接同步文字节点
0.3.1 日志证明 HMD 位置与朝向持续变化，Unity HUD 包装父节点也在变化；用户明确反馈原生文字仍固定在空间中且较远。0.3.2 将可见 NativeText 独立为根节点，ApplyHeadPose 直接更新该节点的世界位置、旋转、缩放，不再只移动空包装父节点。新增公开 SpatialAdapterGameObjectTracker 的节点 ID/有效性诊断及 Stage 模式日志。Play Mode 检查覆盖可见节点连续移动/转头后的相对视角位置，以及暂停/恢复和无碰撞体。真机视觉跟随须另行确认。

## 0.3.3 网格字形 FPS
0.3.2 真机仍被用户明确判定不跟随。日志中文字和父节点的公开 tracker 都有效，且头部及 Unity 文字变换持续变化。0.3.3 改用可见 MeshRenderer 字形，脱离 NativeText 的文字布局路径；固定 APP/FPS 为一个网格，三位数字使用静态字形变体切换，每半秒更新一次，不重建网格、不创建材质。没有背景面、碰撞体或逐帧字体绘制。位置在控制器 Update 之后、SDK 渲染前更新；实际坐标和视觉跟随仍以真机验收为准。

0.3.3（7）于 2026-10-24 22:46:37 安装到 Swan PB3A10MGL9240036B，启动 PID 25144，45 组零件就绪，短时 crash buffer 为空。可见网格节点的 tracker 有效；当前截图存在系统距离障碍物过近提示，且后续头部姿态近乎不变，因此不将截图/日志记为视觉跟随通过。用户真机确认待补。APK SHA-256: EF11EC6D357E4BF5C47BE885E51EF27F7F7A6104B6C2127D530FF7CB5CD269E0，大小 265169016 字节。
