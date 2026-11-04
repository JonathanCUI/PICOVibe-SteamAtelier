# PICO XR 房间透视工程 · 2026-11-04

仓库：`JonathanCUI/PICOVibe-SteamAtelier`；本次唯一更新分支：`codex/steam-atelier-xr-track-lights`。继承伙伴提交 `458ccacbaa3cd0f1a22f1243aacf485ddfd4c2ff`，保留其注视、捏合与快速安装修改。

## 本次完整内容

- 工坊使用预计算的柔和环境反射，减轻金属表面的突兀白块；山谷保持原有环境反射。新增资源位于 `Assets/SwanJourney/Branding/VisualIntegration/`。
- 山谷车底新增一组柔和接触阴影（一个绘制对象、8 个三角形）；隧道使用已有遮光数据按车身位置渐暗/渐亮，保留车灯和车窗发光，不新增实时光源或实时反射捕捉。
- 单件安装后播放 0.52 秒暖色亮边并恢复原材质；原有整车完成庆祝与音效继续保留。
- 17 页手稿教学图、纸张纤维材质、初始摆位 B、柔软拖动惯性、卷起/展开/翻页动画及四个用户提供的纸张音效。
- huocheBGM 替换原背景音乐并沿用原 GUID；zhuanchang 配合两模式独立的自下而上扫描显现。
- 机车、水箱、工作台及控制件材质；太阳放射线、月亮、云运动、日夜平滑车灯与车厢窗光。
- 金色轮廓与从车头到驾驶室的完成扫描，彩色卷曲纸带和碎花；用户指定完成音效，保持原速播放。
- 第一件火箱底圈和走行板支撑的重叠面修复；5 个派生显示网格保留原三角数、UV 和材质槽，原源网格与碰撞体保持不变。
- 对应脚本、Shader、材质、贴图、音效、生成网格、保存场景和山谷预制体及所有 Unity `.meta` 一并入库。

## 获取与打开

使用 Unity **2022.3.62f2c1**。先拉取本分支，再执行 `git lfs pull`；GitHub 的普通 Source code ZIP 不能替代 LFS 检出。

打开 `Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity`。山谷为按需加载的 `Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab`。本分支为 Unity XR SDK 房间透视版，头显里看到身边的真实房间；模拟器的房间背景来自模拟环境。

SDK 依赖继续沿用伙伴已有布局和 `Docs/Local-Dependencies.json`：工程同级 `vendor/PICO-Unity-SDK-main/{XR,SpatialAdapter}`、`vendor/com.unity.formats.usd`；UnityGLTF 在仓库内的 `LocalPackages/com.plattar.unitygltf`。不将本机临时 `LocalPackages/PicoTrial` 路径提交。发布前逐文件核对临时 SDK 与原 vendor，XR 1848 个文件和 SpatialAdapter 371 个文件均一致。可执行 `Tools/Check-V23Dependencies.ps1` 核对包版本、元数据与 LFS。

## 构建与安装包

Android / ARM64 / IL2CPP，PICO XR Loader。构建入口为 `SteamXrTrialBuild.BuildVisualIntegration`；它从保存的 Swan 源场景派生 XR 场景，运行 70 项快速安装检查后输出 `Builds/XRTrial/SteamAtelierXRRoom-VisualIntegration.apk`。不要使用旧工坊生成器覆盖当前设计场景。

Android 工具链使用当前编辑器配置，可通过 `ANDROID_SDK_ROOT`、`ANDROID_NDK_ROOT`、`JAVA_HOME` 指向本机的 SDK、NDK、JDK。SDK 需支持 API 34。

已测试 APK 为 **241450928 bytes**，SHA-256：

`a5ce97023e01aec269d2e76ad039de148716ad645e04cf37493ef25a49ad019c`

[本次安装包与测试说明](https://github.com/JonathanCUI/PICOVibe-SteamAtelier/releases/tag/xr-room-visual-integration-20261104)。包名 `com.picovibe.steamatelier.xrtrial.room`，可覆盖已有 XR Room 测试版。Release 标签对应本次源码提交；APK 单独放在 Release，不放进源码仓库。Library、缓存、日志、签名私钥及构建临时目录不入库。

## 已验证与实机复核

C# / Shader 构建成功，70 项快速安装检查通过。新增安装反馈的触发、恢复、连续安装、完成庆祝、山谷往返和重置检查均通过。两轮 60 Hz 模拟器对照测试中，工坊、山谷行驶与隧道平均约 60 FPS，夜间约 59.8–60 FPS；第二轮夜间出现一帧 34.64 ms，未见持续性能下降。这不是头显 90 FPS 验收。前版 17 步预览、三种观察模式与闪面修复记录继续保留，不能把前版巡检当成本次全流程实机验收。

实机重点复核金属反射、车底阴影贴地与透视背景、隧道明暗过渡和夜间灯光、单件安装与最终庆祝、纸张操作和模式切换。新增详细记录：`XR-Visual-Integration-20261104.json`；前版记录：`XR-Surface-Repair-20261104.json`、`XR-Assembly-Celebration-20261104.json`、`XR-Paper-B-20261104.json`、`XR-Paper-Roll-Audio-20261104.json`。
