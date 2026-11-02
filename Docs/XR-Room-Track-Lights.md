# PICO XR 山谷车灯测试工程

本分支是 SteamAtelierXRRoom-TrackLights.apk 的 Unity 工程。使用 Unity 2022.3.62f2c1，打开工程根目录。先运行 `git lfs pull`，按 `Docs/Local-Dependencies.json` 和 README 配置仓库外的 SDK。

- 打开 `Assets/SwanJourney/Scenes/SteamJourneyXRTrial.unity` 查看已保存的测试场景。山谷从 `Assets/SwanJourney/Resources/SwanJourneyCanyon.prefab` 按需加载。
- 使用 Android 平台、PICO XR Loader 和 `SteamXrTrialBuild.Build` 构建。这个入口会从 Swan 场景重新生成 XR 场景。构建器中的 SDK、NDK 和 OpenJDK 路径来自测试机，请在本机指向对应的 Android 工具链。
- 在山谷夜间切换车灯，从斜侧面观察双灯光束、车前枕木和沙地。沙地植被清理、烟囱尾烟、昼夜、锅炉火和蒸汽的资源及生成代码随工程提供。

发布包的 SHA-256 为 `38831087d8be4f79634879fc436f5606ac014240c4686b6e285f7948cf338f40`。[测试安装包与说明](https://github.com/JonathanCUI/PICOVibe-SteamAtelier/releases/tag/xr-room-track-lights-20261102)
