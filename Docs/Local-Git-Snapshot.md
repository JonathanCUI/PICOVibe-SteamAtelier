# 0.3.5 本地保存点

本仓库保存「蒸汽工坊·新版」Unity 工程，包名 `com.picovibe.steamatelier`，版本 `0.3.5`，Android versionCode `9`。

- 包含 `Assets`（含 .meta、搭档原始场景及 Swan 派生场景）、`Packages`、`ProjectSettings`、构建工具和说明。
- `Library`、`Temp`、`Logs`、`UserSettings`、导出的 SpatialAdapter 原生载荷、APK 和构建目录不入库。
- 超过 100 MB 的原始网格使用 Git LFS，本地对象保存在本仓库 `.git/lfs`；备份整个仓库时应一并保留 `.git`。
- 已安装 APK 继续保存在 `Builds/Releases/0.3.5/SteamAtelier-Swan.apk`，其校验与验收记录另存为 `Docs/Release-0.3.5.json`。
- 逻辑回归检查对应 0.3.4；0.3.5 仅内移并提亮 FPS 字形，已通过构建和真机截图验证。视野跟随由用户在 0.3.4 明确确认。

## 本地依赖

工程沿用 `Packages/manifest.json` 的本地包路径，依赖同级 `unity/vendor` 中的 PICO SpatialAdapter 6.1.0、PICO XR 6.1.1、Plattar UnityGLTF、Unity USD，以及 `unity/toolchain/android-sdk`。这些共享 SDK / 工具链在本仓库目录之外，本次未复制或改动它们。版本及包元数据校验见 `Docs/Local-Dependencies.json`。恢复到其他目录时需保留相同布局或重新配置本地依赖路径。

Unity 版本为 `2022.3.62f2c1`；构建入口为 `Tools/Build-SwanAtelier.ps1`。本地标签 `v0.3.5` 标记此保存点；本次不配置远端、不推送。
