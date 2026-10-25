# 0.4.3 操作台与剖面修复

用户反馈右侧控制台难操作，尤其汽门推把；剖面模式显示大方块。复查时用户成功推动汽门，因此不是动力模拟完全失效。真机截图 `Builds/Evidence/v042-cutaway-before.png` 直接显示了锅炉内部的大白色包围盒。

## 操作台

原汽门以 `SpatialInputState.currentPosition` 的目标命中点位移控制动力，方向只取展台上方，固定 16 厘米行程。捏合手移动而注视点不动时不能可靠调节。0.4.3 在单手捏合汽门期间使用同一公开输入状态的 `devicePosition`；上抬或沿面板前推增加动力，下放或回拉减小，行程随展台缩放限制为 4–15 厘米。双手优先级、稳定 interactionId、松手停止和既有零件拖动保持原实现。

原控制台碰撞框多数在空父节点上，与子节点可见网格分离；现在 24 个控件对应 37 个可见网格命中目标，碰撞体和原生高光同处一节点，仍由父级 AtelierControl 处理动作。缩小展台时，各轴命中尺寸至少 14 毫米。删除空父级的旧碰撞框，保留保护盖及重置长按逻辑。

## 剖面

源模型的 `SteamWorkshop/SteamVolume` 和 `SteamWorkshop/CylinderSteamV7` 是 URP ray-march 体积 Shader，原本在 Cube 内裁出圆柱截面和水位上方蒸汽区域。旧转换只保留 Cube 和基础色，无法保留该 Shader 的采样/裁剪；源颜色 alpha=1，转换后正好成为挡住内部结构的白色方块。

生成器现在依据源 `_Bounds` 与 `_Floor` 烘焙圆柱截面几何，用可导出的 Standard 半透明材质（alpha 0.13/0.20）显示。替换 11 处蒸汽体积代理，保留锅炉内部、活塞和管路。关闭旧的 Shader 密度驱动组件，用 `SwanCutawaySteam` 更新汽缸蒸汽长度与可见性；这是几何教学示意，不是原来的体积 ray-march 效果。剖面静止时显示通路箭头，运转时仍按配汽相位显示流向。

首轮编辑器画面还暴露了 `SteamWorkshop/WaterBodyV8` 与 `WaterSurfaceV8` 的同类问题：颜色在 shader 内硬编码，没有 `_BaseColor`，旧转换回退为 alpha=1 的灰色。水体现按原 shader 的圆柱边界及 Y≤0.323529 水位烘焙，透明度 0.14；水面保留源网格，使用透明度 0.22 的蓝绿色材质。总计处理 12 处体积代理和 1 处水面，避免只修复蒸汽后仍留下挡住管束的水体方块。

## 验证与依据

`SwanConsoleChecks` 验证可见网格输入、固定注视下手部拖动、缩放/旋转后的前推、制动、空挡/后退、灯光、行驶开关、剖面开关、圆柱透明几何和停机通路。既有双手、拆装、白色轮廓、短动画及 FPS 检查继续运行。编辑器画面保存为 `Builds/Evidence/cutaway-editor.png`，在测试产生的临时零件完成延迟销毁之后捕捉，避免把测试残留当作剖面结果。`Tools/Inspect-CutawayExports.ps1` 检查 APK 内实际 GLB 材质的 BLEND/alpha 和圆柱网格顶点数，真实命中手感和剖面外观另行真机验收。

参考本地 Unity SpatialAdapter 6.1.0 已有输入、Collider/Renderer 同步与 GLB 材质导出实现。知识服务检索只返回 6.0 Kotlin 手部拖动与 Spatial Editor Shader Graph 文档，未作为 Unity 6.1 API 替代依据。未更改 vendor、旧版 SteamWorkshop 或搭档原始场景。

## 真机结果（2026-10-26）

0.4.3（17）已安装到 Swan PB3A10MGL9240036B，捕获 READY、推把以 hand 输入从 0 到 100% 调节并启停、制动和剖面点击日志。`v043-cutaway-after.png` 中锅炉管束与流向箭头可见，原实体方块已消失。用户确认：「推把正常，剖面方块消失」。APK 内 13 个相关节点均为 BLEND 透明材质，45 个白色装配轮廓导出仍通过检查。单一启动 Activity 保持；所查该 PID crash buffer 为空。完整记录见 `Release-0.4.3.json`。
