蒸汽工坊 · PICO XR 房间透视版 · 2026-11-04

仓库：JonathanCUI/PICOVibe-SteamAtelier
分支：codex/steam-atelier-xr-track-lights
Unity：2022.3.62f2c1
应用：Steam Atelier XR Room
包名：com.picovibe.steamatelier.xrtrial.room

这是当前完整 Unity 源工程，包含 17 页手稿、纸张交互与音效、材质与昼夜表现、扫描显现、组装完成金色扫描和彩带，以及火箱和走行板闪面修复。
本次追加工坊柔和金属反射、山谷车底接触阴影、隧道进出逐段明暗过渡和单件安装亮边反馈，保留已有内容与伙伴交互修改。

已有仓库请切到上述分支并拉取，再执行 git lfs pull。
首次获取：
git clone --branch codex/steam-atelier-xr-track-lights https://github.com/JonathanCUI/PICOVibe-SteamAtelier.git SteamAtelierSwan-XRTrial
cd SteamAtelierSwan-XRTrial
git lfs pull

打开 Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity。
构建入口：SteamXrTrialBuild.BuildVisualIntegration。
依赖继续使用伙伴已有的同级 vendor 目录，精确版本见 Docs/Local-Dependencies.json。
详细打开、构建、变更与验收说明：Docs/XR-Room-Track-Lights.md。

本次安装包和测试说明：
https://github.com/JonathanCUI/PICOVibe-SteamAtelier/releases/tag/xr-room-visual-integration-20261104

安装包 SHA-256：
a5ce97023e01aec269d2e76ad039de148716ad645e04cf37493ef25a49ad019c

本次两轮 60 Hz 模拟器工坊、山谷与隧道平均约 60 FPS，夜间约 59.8–60 FPS；未见持续下降。模拟器结果不能代替头显 90 FPS 验收，实机帧率、立体画面和手势仍需复核。
历史 Swan / SpatialAdapter 版本资料保留在 README-Swan.md 和 Docs 中，不作为本分支当前运行配置。
