# 0.3.8–0.3.9 白色轮廓、双击归位与启动容器

## 使用方式

拿起零件时，其空置安装位显示半透明白色模型（RGB 1/1/1，alpha 0.34）。松手后零件可以悬停；看向这个零件，在同一个零件上连续完成两次短捏合，即可快速装入最近的空置同规格位置，沿用正常装配声效、占位和零件盘更新。

- 单次捏合持续不超过 0.28 秒；第一次松开至第二次开始不超过 0.40 秒。
- 每次触点移动不超过 2.5 厘米，来回拖动也按最大位移判断。
- 只统计捏合开始时已是散件的目标；从车身拆下或从零件盘拿出的动作不算第一次点击。
- 换目标、长按、拖动、取消、暂停、重置和双手操作会清除双击候选。
- 原有单手拖动和靠近安装位松手吸附保持不变。快速归位直接使用已有安装流程，不新增物理碰撞或持续动画。

## 启动窗口

0.3.7 的场景显式使用 Unconstrained Spatial Camera，但全局 `SpatialAdapterRuntimeSettings` 仍指向 Constrained 默认配置。旧 APK 的启动 Activity 被 SDK 写入了 `pico.spatial.windowcontainer.*` 声明，运行时又打开 Stage；真机记录显示 `SpatialAdapterPlayerActivity` 和 `SpatialAdapterSubActivity` 两个 Activity 同时存在。

生成器现在将全局默认配置与场景相机统一为 SDK 提供的 Unconstrained 配置。由 SDK 的 `SpatialAdapterManifestProcessor` 自动生成对应声明；不结束 Unity Activity、不更换渲染管线、不修改 SDK 源码或调用内部 Android 接口。0.3.8 真机已只存在启动 Activity，双击归位日志成功，但空间截图显示黑色背景；0.3.9 的项目构建回调在 SDK 写入后将公开的 `pico.spatial.stage.style` 设为 `Mixed`（SDK 枚举值 1），明确保留真实房间透视，避免 Automatic 的环境选择差异。

知识服务未提供 Unity 启动窗口的针对性说明，因此进一步检查了当前 6.1.0 SDK 自带的公开设置、Manifest 处理器与已有 Android 运行记录。用户随后确认多余窗口已消失、原有视觉效果正常、白色轮廓正常、双击归位正常；截图中的黑底不能单独证明头显透视失败。用户另反馈双手操作不易启动，由 0.4.0 继续改进。

## 检查

`SwanQuickInstallChecks` 验证正常双击、单击、慢击、长按、来回拖动、不同目标、取消、暂停、双手优先、兼容槽位和展台变换。原有 45 零件、轮廓、注视高光、FPS 与双手操作检查同时运行。

`Inspect-PlacementGhostExports.ps1` 验证 APK 内 45 份轮廓均为白色、BLEND、alpha 0.34。构建后还检查 SDK 生成的启动元数据及真机 Activity 列表。最终交付状态记录于对应 `Docs/Release-0.3.x.json`。

## 0.4.1 归位动画

用户确认双击归位可用后，进一步要求能看清安装去向。现在双击启动 0.22 秒的局部空间位移/旋转过渡，抵达目标才调用原有安装逻辑；目标轮廓保持白色。目标槽位在飞行期间预留，其他同类零件不能同时占用。整个展台仍可用双手调整。批量展开/装好各 0.28 秒，详见 `Exploded-View.md`。
