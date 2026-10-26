# Steam Atelier Swan

用户指定后续蒸汽工坊改动均在本新版工程完成。不要修改同级 `SteamWorkshop` 旧版工程。

- 继承工作区 `../../AGENTS.md` 与 `../../PICO-SPATIAL-AGENTIC-TOOLS.AGENTS.md` 的适用指导。本工程为 Unity，不是根目录 Kotlin SpatialMemory 工程。
- 使用 Unity 2022.3.62f2c1 与本地 PICO SpatialAdapter 6.1.0。新应用包名固定为 `com.picovibe.steamatelier`、应用名为「蒸汽工坊·新版」。后续正常更新沿用此包名并递增版本号，不能改用旧版 `com.picovibe.steamworkshop`。
- 活跃场景是 `Assets/SwanAtelier/Scenes/SteamAtelierSwan.unity`，由 `SwanAtelierBuild.Generate` 从搭档保存的 `Assets/Atelier/Scenes/SteamAtelier.unity` 派生。应修改生成器，避免直接修改生成场景后被下次构建覆盖。
- 不运行搭档的 `AtelierBuild.Run()`，它会恢复搭档手动删除的环境。原始搭档场景应保留。
- 构建入口 `Tools/Build-SwanAtelier.ps1`；`-SceneOnly` 生成场景，`-ChecksOnly` 运行 Play Mode 验证。脚本使用 Unity 自带 OpenJDK 和同级 toolchain Android SDK。
- 交互在 `SwanAtelierController`；原 `AtelierExperience` 只保留数据与零件盘刷新，桌面输入循环停用。
- 双手输入在该控制器的 partial 文件 `SwanAtelierGestures.cs`，使用稳定 interactionId 区分触点；不要把 primary/secondary 当作固定左右手。双手变换根节点为展台，FPS 保持独立；缩放绝对范围 0.26–1.5。详见 `Docs/Two-Hand-Manipulation.md`。
- 注视反馈用公开 `SpatialAdapterHoverEffect`；不要把头部射线宣称为眼动。FPS HUD 统计 Unity 应用更新频率，不能称为原生合成器或屏幕 FPS。0.3.4 接入同一本地 SDK 的 XR 6.1.1 Spatial 输入子系统，通过 Unity `XRNode.Head` 获取姿态；用户已确认视野跟随正常，0.3.5 只内移并提亮文字，详见 `Docs/Hover-And-FPS.md`。
- Android 构建成功和编辑器逻辑检查不代表眼动、捏合、头部跟随已通过真机验证。设备未连接时，交付 APK 并准确标记未验证事项。

- FPS 当前为 SwanFpsMeshText 的普通网格字形（SwanFpsMeshBuilder 生成），不能回退到先前视觉跟随失败的 NativeText HUD。头部位置在 Update 中直接作用于可见网格根节点；Unity XR 姿态直接使用，不重复套用原生导出坐标逆变换。仅启用 PXR_Loader，且 `isSpatialAdapter=true`；不要切成普通 XR 渲染。`SwanSpatialInputMode` 只在 Android Player 调用公开模式同步方法，防止编辑器访问 JNI。保持真机验收与代码检查分开记录。
- 0.5.5 将 PXR_Loader 初始化延后至 Spatial 场景启动后：`XRGeneralSettings.InitManagerOnStart=false`，由 `SwanSpatialInputMode` 在 Start 后一帧且应用获焦点后通过公开 XR Management API 初始化、启动，并负责销毁时清理。桌面感知等待其 Ready。不要恢复 BeforeSplash 自动初始化而丢失已记录的休眠启动问题背景；实际验收状态见 `Docs/Release-0.5.5.json`。
