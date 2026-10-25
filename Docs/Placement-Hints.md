# 零件安装轮廓（0.3.6–0.3.8）

## 故障原因

搭档的 `LocomotiveFreeAssembly.AimSocket/Ghostify` 已实现半透明零件提示，但它属于已停用的桌面交互循环。Swan 控制器单独处理手势，只显示金色球形接口；场景生成器还将 `ghostMaterial` 清空。因此原场景有轮廓不代表 Swan 的手势路径会调用它。

## 修正

- 在原有 Swan 控制器中接回轮廓：拆下车身零件、从零件盘拿取、重新拿起散落零件都会显示。
- 提示与吸附共用 `NearestSlot`：只选空置且规格匹配的位置，同规格多个位置时显示最近目标的实际形状。
- 45 个轮廓预制体复用已经优化的零件网格，生成时烘入同一半透明 Standard 材质，通过序列化引用进入 SpatialAdapter 的构建资源。0.3.6 为淡青色 alpha 0.22；用户真机反馈“能看到，但是有点淡，位置是正确的”，0.3.7 提亮并把 alpha 调到 0.34；0.3.8 按用户要求改为白色，保留 alpha 0.34。
- 轮廓位姿采用现有散件的坐标约定，跟随展台移动、Y 轴旋转、26%–150% 缩放。仍在同一目标时复用实例。
- 轮廓不含碰撞体、注视高光或交互脚本，不投射阴影；正常情况下取代金色小球。
- 松手、取消、双手操作接管、暂停/追踪重置、禁用控制器、重新装配或全部装好时隐藏并销毁提示。
- 沿用当前吸附距离和装配声效，不修改 FPS、双手变换及旧版应用。

## 验证

`Tools/Build-SwanAtelier.ps1 -SceneOnly` 生成场景；`-ChecksOnly` 运行 Play Mode 回归，其中 `SwanPlacementHintChecks` 覆盖材质/几何/无碰撞、缩放旋转、兼容槽位切换、重新抓取、零件盘、取消和清理。原 45 个零件拆装检查逐件确认轮廓。

编辑器截图输出 `Builds/Evidence/placement-ghost-editor.png`。`Tools/Inspect-PlacementGhostExports.ps1` 直接检查 APK 内 45 份轮廓 GLB 的透明混合和 alpha。0.3.6 已通过 Play Mode 检查、导出检查与真机位置确认；0.3.7 仅调整材质外观，最终构建、安装及清晰度验收状态以对应 `Docs/Release-0.3.x.json` 为准。

PICO 工具当前配置是 Spatial 路线，通用 CLI/知识服务可用；本项目继续使用既有 Unity 2022.3 和本地 SpatialAdapter 6.1.0 构建入口，不切换环境。知识检索返回的是 Kotlin 6.0 材质资料，不能用于确认 Unity 6.1 导出行为；该行为通过项目现有预制体路径和实际构建资源验证。
