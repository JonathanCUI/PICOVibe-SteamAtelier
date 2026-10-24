# 蒸汽工坊·新版（Swan）

来自搭档交付的 `SteamAtelier-Partner-20261024-1422.zip`，使用保存的 `Assets/Atelier/Scenes/SteamAtelier.unity`。原交付文件已完整解压、逐文件校验；适配在独立工程进行。

| 项目 | 新版 | 旧版（保留） |
| --- | --- | --- |
| 工程目录 | `unity/SteamAtelierSwan` | `unity/SteamWorkshop` |
| 应用名 | 蒸汽工坊·新版 | 蒸汽机车工坊 |
| Android 包名 | `com.picovibe.steamatelier` | `com.picovibe.steamworkshop` |
| 版本 | 0.3.5（9），验证状态见下文 | 0.1.2（3） |
| 装配组 | 45 | 44 |

## 打开与构建

- Unity：2022.3.62f2c1，Android / IL2CPP / ARM64 / Vulkan。
- PICO Spatial Adapter：6.1.0；0.3.4 新增同一本地 SDK 目录的 XR 6.1.1 包以启用 Spatial 头部输入，仍使用 SpatialAdapter 渲染。
- Swan 场景：`Assets/SwanAtelier/Scenes/SteamAtelierSwan.unity`。
- `Tools/Build-SwanAtelier.ps1 -SceneOnly`：从保存的搭档场景生成 Swan 场景及资源。
- `Tools/Build-SwanAtelier.ps1 -ChecksOnly`：运行 Play Mode 交互逻辑检查。
- `Tools/Build-SwanAtelier.ps1`：重新生成并构建 `Builds/SwanAtelier/SteamAtelier-Swan.apk`。
- 依赖同级 `unity/vendor` 与 `unity/toolchain/android-sdk`；Unity 使用其自带 OpenJDK。

请勿运行搭档的 `AtelierBuild.Run()` 来重新生成设计场景，该流程会恢复搭档已手动删除的桌子、房间。Swan 生成器仅派生保存场景。

## 操作

- 双手调整：看向机车或展台，两手分别捏合拇指和食指；两手一起移动时平移，转动两手连线时只绕世界 Y 轴旋转，拉开/靠近时缩放。大小限制为初始尺寸的 26%–150%。松开任意一手停止；再次捏合继续。FPS 面板不随展台缩放或旋转。
- 同时捏合优先调整整个展台。单手捏合保留短暂的双手识别窗口（0.18 秒），普通快速点击仍会响应；退出双手模式后仍捏着的那只手不会自动触发按钮或拆件。
- 汽门：捏住金色手柄上下拖动；红色手柄制动。
- 换向：点击在前进、空挡、后退间切换；换向后重新加汽。
- 模式：停车后切换原地演示或短轨行驶。行驶到轨道终端自动减速。
- 汽笛：捏住可持续鸣笛；灯光、剖面开关为点击操作。
- 拆装：捏住已安装部件向外拖；靠近相容的金色接口松手安装；未对齐的部件留在空间中，可再次捏取。
- 零件盘：选择分类、翻页，从空缺零件槽中捏取。已安装部件不重复生成。
- 重新装配：打开保护盖，再捏住红按钮 1.2 秒；松手取消。
- 点击车门、炉门可分别开关。下方提供“全部装好”和“捏住移动展台”辅助控件。
- 注视或指向零件、控制柄、按钮与分类选项时，使用 Swan 系统悬停高光确认当前目标；拆下的零件同样支持。
- FPS 为视野左上角的无背景、小号单行 `APP xxx FPS`，每半秒更新一次。0.3.4 改用官方 Spatial XR 头部输入后，用户已确认「现在能跟随视野」。0.3.5 仅把文字内移、提亮，解决截图中靠左裁切和偏暗问题。该数字是 Unity 应用更新帧率，不代表 Swan 合成器或屏幕刷新率。

## 适配范围

保留搭档的机车、45 组装配数据、物理控制台、分类零件盘、机械运动、音效和灯光。选用搭档的独立头显网格等级，去除重复隐藏网格，合并不动的表面，并保留动画轴与控制柄。文字改用 Swan 原生文字，烟雾使用已验证的透明材质。

Swan 输入由 `SwanAtelierController` 接管。原 `AtelierExperience` 仅保留设计引用、分类和零件盘刷新；原桌面键鼠循环停用。模型运动停下后避免每帧重复更新整车姿态。

`Builds/SwanAtelier/` 包含构建结果、场景预算、Play Mode 检查结果和编辑器预览。编辑器检查不等同于头显手势验证；真机的手柄命中、拖动舒适度、文字大小及帧率需要戴上头显验收。

## 0.3.0 双手交互验证（2026-10-24）

- 增加双手平移、Y 轴旋转、有限缩放；操作整个展台根节点，机车、控件与已拆零件一起变换。
- 使用 SpatialAdapter 的公开双路捏合事件与 `devicePosition`，按稳定 `interactionId` 区分手势，避免 primary/secondary 次序变化导致翻转。无目标的空中捏合不由当前接口转发，需要先看向本应用的可交互物体。
- Play Mode 检查通过：三维平移、旋转轴、缩放上下限及边界反向响应、再次抓取、无效姿态/追踪跳变、双手优先、输入次序变化、松手后阻止误触、快速点击与去重、取消/暂停清理，并通过原有 45 组零件拆装回归。
- 0.3.0（4）已安装到 Swan；真机日志已捕获双手捏合开始、平移、旋转、缩放与结束。用户已确认双手移动、旋转、缩放和松手正常。最新 FPS 跟随问题单独记录在 Docs/Hover-And-FPS.md。
- 当前版本的构建、安装和真机手势状态见 `Builds/SwanAtelier/delivery-verification.json`；编辑器检查不能代替双手操作的真机验收。详细设计见 `Docs/Two-Hand-Manipulation.md`。

## 0.2.2 修正与验证（2026-10-24）

- 根据真机反馈修正车身零件高光：140 个实际网格直接绑定命中区域与原生高光，45 个拆卸零件 prefab 使用相同规则。模型三角形数未增加。
- 修正 FPS HUD 的会话状态码判断：`IsSessionInitialized()` 返回 `0` 才代表就绪。
- Play Mode 回归检查通过；Android 构建成功，0 个错误，APK 为 0.2.2（3）/ ARM64。
- 21:21 已安装 0.2.2（3）并启动到 Swan `PB3A10MGL9240036B`，旧版仍为 0.1.2（3）。日志确认 `SWAN_ATELIER_READY pieces=45`、`SWAN_ATELIER_HUD_TRACKING_READY` 及持续 `headPose=True poseResult=0`；应用进程存活，所查该进程 crash buffer 为空。用户随后在 Swan 真机确认：「车身高光与 FPS 跟随都正常」，本轮功能验收通过。最新状态与 APK 哈希见 `Builds/SwanAtelier/delivery-verification.json`。

## 0.2.0 基线验证（2026-10-24）

- Unity 构建成功，0 个构建错误；APK 265,075,222 字节，ARM64。
- Play Mode 检查通过：45 组零件逐一拆卸、拒绝错误位置、正确吸附；汽门、制动、空挡、倒车、轨道终点、剖面、保护盖长按复位、取消、分类、翻页、车门与炉门、原生文字绑定及新包名。
- APK 已安装到 Swan `PB3A10MGL9240036B`。设备查询确认新版 `com.picovibe.steamatelier` 为 0.2.0（1），旧版 `com.picovibe.steamworkshop` 仍为 0.1.2（3）。
- 已发送启动命令，进程存在。检查时设备为 `mWakefulness=Asleep`；尚未获得 `SWAN_ATELIER_READY` / 场景可见的真机证据，因此完整真机启动、手势与帧率验收待唤醒头显后进行。原始启动日志留在 `Builds/Evidence/device-startup.log`，其中有两条未定位对象的 missing-script 警告，需要在唤醒后的完整加载日志中复核。
- 原搭档保存场景 SHA-256 未改变；旧版工程 Git 工作区仍干净。
- APK SHA-256：`705073D39559A213FF13206A90190A2A8C4EF94E4932F9B89244005C857442E3`。



