蒸汽工坊 · V23 — 伙伴修改基线 0.23.23
保存日期：2026-10-31
Git 分支：codex/steam-atelier-v23-swan

这是当前 Swan 真机运行版本的 Unity 源工程。旧版 0.5.6 保留在
codex/steam-atelier 分支；本分支不覆盖该分支。

1. 当前状态
Unity：2022.3.62f2c1（92e6e6be66dc）。不要直接升级引擎或 PICO SDK。
应用：蒸汽工坊·V23，包名 com.picovibe.steamatelier.v23，版本 0.23.23 / code 23。
构建配置：Development + IL2CPP Release / OptimizeSpeed。
用户已在真机确认按钮点击、双手旋转和缩放恢复。
保留注视高光、双击零件归位、单手搬台、整体变换与山谷按需加载。
性能仍未通过验收，用户观察约 30–40 FPS；不要将这一版标记为 90 FPS 完成版。
9 组回归、110 项性能保护、263 项包内检查及 APK v2 签名通过。
精确发布记录见 Docs/V23-Swan-Release.json。

2. 获取工程
需要 Git 和 Git LFS。使用 git clone，首次打开 Unity 前先运行 git lfs pull。
不要只用 GitHub 的 Download ZIP 来代替完整的 LFS 资源检出。

git clone --branch codex/steam-atelier-v23-swan https://github.com/JonathanCUI/PICOVibe-SteamAtelier.git SteamAtelierSwan
cd SteamAtelierSwan
git lfs pull

仓库为私有仓库，伙伴需要已有访问权限。

3. 本地 SDK 依赖
本工程的 Packages/manifest.json 使用本地包路径，以下依赖在仓库外，
需要从有授权的 PICO / Unity 开发环境取得。SDK 分发包不包含在本次 Git 提交内。
准确版本和 package.json 校验值见 Docs/Local-Dependencies.json。

建议目录布局：
  Work/
    SteamAtelierSwan/     本 Git 工程（Assets、Packages、ProjectSettings）
    vendor/
      PICO-Unity-SDK-main/
        XR/              com.bytedance.pico.xr 6.1.1
        SpatialAdapter/  com.bytedance.pico.spatialadapter 6.1.0
      com.plattar.unitygltf/  1.142.0
      com.unity.formats.usd/  3.0.0-exp.5
    toolchain/android-sdk/  Android 打包工具链

运行 Tools/Check-V23Dependencies.ps1 可只读检查这些包的版本、元数据校验和 LFS 文件。
检查脚本不下载 SDK、不修改 manifest、不覆盖本地包。
缺失 SDK 时先补齐依赖；不要通过删除 PICO 脚本或随意更换包版本消除编译错误。

4. 修改入口
伙伴原始场景：Assets/AtelierJourney/Scenes/SteamJourney.unity。
Swan 派生场景：Assets/SwanJourney/Scenes/SteamJourneySwan.unity。
启动场景：Assets/SwanJourney/Scenes/SteamJourneyBoot.unity。
山谷派生预制体：Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab。
Swan 生成器与运行适配：Assets/SwanJourney/Editor、Assets/SwanJourney/Scripts。

模型、美术、场景源数据优先在原始场景/源资源中修改，保留 .meta / GUID、
装配对象引用、轴心、父子关系及活动零件边界。
Assets/SwanJourney/Generated 与 Swan 场景是生成结果，下一次构建可能覆盖。
不要只改派生网格后把它当作长期修改入口。
不要运行 AtelierBuild.Run 或伙伴的全量源场景重建命令覆盖当前保存场景。

5. 下一轮模型优化重点
优先：安装提示、扫描轮廓、预览副本的低面数代理。
定点：锅炉紧固件、铆钉、螺纹、过密圆管截面；保留重要外轮廓。
结构：同一刚性零件内部合并细碎网格及相近材质。
车轮、活塞、连杆、可拆装组与按钮仍需独立，不要合并为一整台不可拆机车。
模型统计与已测耗时摘要见 Docs/V23-Performance-Baseline.json。
资源总三角数包含隐藏对象，不是实时可见三角数；实际收益需要真机对照。

6. 重新打包
先关闭该工程的 Unity 编辑器，再在 Windows PowerShell 中执行：
  ./Tools/Build-SwanJourney.ps1
脚本默认使用本机 Unity 2022.3.62f2c1 与同级 toolchain/android-sdk。
可通过 -EditorPath 指定相同版本的 Unity.exe 路径。
脚本会从伙伴源场景生成 Swan 内容、执行回归检查，然后构建 APK。
输出：Builds/SwanJourney/SteamAtelier-V23-Swan.apk。
不要使用旧版 Tools/Build-SwanAtelier.ps1 来构建 V23。

7. 本次仓库范围
包含 Assets（含 .meta、源模型和当前派生内容）、Packages、ProjectSettings、
构建和检查工具、交接说明。保留旧版本资源，未借上传任务清理模型。
不包含 Library、Temp、日志、Builds、APK、签名密钥、Python 缓存与 SDK 私有分发包。
完整历史诊断见 AGENTS.md、Docs/V23-Swan-Integration.md；以最新记录为准。
