# PICO XR 房间透视工程 · 2026-11-04

仓库：`JonathanCUI/PICOVibe-SteamAtelier`；本次唯一更新分支：`codex/steam-atelier-xr-track-lights`。继承伙伴提交 `458ccacbaa3cd0f1a22f1243aacf485ddfd4c2ff`，保留其注视、捏合与快速安装修改。

## 本次完整内容

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

Android / ARM64 / IL2CPP，PICO XR Loader。构建入口为 `SteamXrTrialBuild.BuildCompletionFeedback`；它从保存的 Swan 源场景派生 XR 场景，运行 70 项快速安装检查后输出 `Builds/XRTrial/SteamAtelierXRRoom-CompletionCelebration.apk`。不要使用旧工坊生成器覆盖当前设计场景。

Android 工具链使用当前编辑器配置，可通过 `ANDROID_SDK_ROOT`、`ANDROID_NDK_ROOT`、`JAVA_HOME` 指向本机的 SDK、NDK、JDK。SDK 需支持 API 34。

已测试 APK 为 **241072689 bytes**，SHA-256：

`b72a048f099fef9bdc05e700412f4569119e04d598797777ef83fd9c354635f0`

[本次安装包与测试说明](https://github.com/JonathanCUI/PICOVibe-SteamAtelier/releases/tag/xr-room-workshop-polish-20261104)。包名 `com.picovibe.steamatelier.xrtrial.room`，可覆盖已有 XR Room 测试版。Release 标签对应本次源码提交；APK 单独放在 Release，不放进源码仓库。Library、缓存、日志、签名私钥及构建临时目录不入库。

## 已验证与实机复核

C# / Shader 构建成功，70 项快速安装检查通过。模拟器巡检 17 步预览和整车实体、透视、剖面三种模式，目标重叠面几何检查为 0 对。60 Hz 模拟器完成效果约 59.7–60 FPS，山谷行驶约 60 FPS；这不是头显 90 FPS 验收。先前模拟器异常降到 2 FPS 的失败记录已排除，原因仍未证实；首次 GPU 编译卡顿也不能据此宣称消失。

实机重点复核第一步火箱底沿及走行板近看是否闪动、纸张抓取和正反翻页/收起、完成金色扫描/彩带/音效、进入与返回山谷、夜间自动车灯和手势。详细记录：`XR-Surface-Repair-20261104.json`、`XR-Assembly-Celebration-20261104.json`、`XR-Paper-B-20261104.json`、`XR-Paper-Roll-Audio-20261104.json`。
