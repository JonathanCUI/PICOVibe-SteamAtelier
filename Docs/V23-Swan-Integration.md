# V23.1 → Swan 集成与验收

## 0.23.23 输入恢复候选

- 2026-10-31 白天真机确认0.23.22输入回归：注视高光正常，但按钮、单手和双手捏合均无响应。10:35日志XR_STAGE_READY与INPUT_READY正常，无INPUT_BEGIN；不能把夜间安装成功当交互验收。23候选恢复此前Development + IL2CPP Release/OptimizeSpeed编译配置，保留21/22几何与碰撞优化；增加只读5秒INPUT_HEALTH及原生Began日志。尚未证明Non-Development/Master是根因，实际恢复需真机确认。证据Builds/Evidence/session-output-20261031-v2323-input-recovery。默认构建已恢复Development，旧22发布记录标记输入失败。


## 0.23.20 变换过滤缓存（2026-10-31）

用户完成19版测试，日志已收到完整山谷静止与双手窗口：00:16:03.859的151帧均为双手操作，TransformFilter平均3.793ms、输入0.087ms；00:16:07.767的应用窗口约30.0Hz。前后稳定静止约42–45Hz；四个明确静止窗口过滤均值0.00124ms，较宽六个非初始idle窗口含残留根变化，均值0.164ms。不得将所有idle窗口描述为0.001ms，也不能将LateBehaviourUpdate未测部分全部归SDK。原始证据保存在 `Builds/Evidence/session-output-20261030-v2319-cpu-timing/runtime-user-test.log`，本轮比较在 `Builds/Evidence/session-output-20261031-v2320-transform-filter/`。

20版只优化已测到的过滤遍历：缓存root/GameObject，使用本机Unity2022.3文档确认的公开GetLocalPositionAndRotation，Entry改连续结构体并以ref更新。保留dirty、本地TRS、父级、激活和warmup判定，真实动画不被过滤；不改SDK、场景父级或模型材质。102项性能/加载检查包含新加入的激活、重挂接、销毁、新增对象与局部姿态回归，9组回归、263包内检查及v2签名通过。三个场景/预制体GLB与19版逐字节相同。

APK为 `Builds/SwanJourney/Releases/0.23.20/SteamAtelier-V23-Swan-0.23.20.apk`，785207519字节，SHA256 `43b2486dc0c8009ffec7296edf20ccdafbab6b56017c1913964e80f08d421045`。改前备份 `Builds/Backups/0.23.19-before-transform-filter-cache/source.zip`。这是局部优化候选，真机耗时收益及FPS尚待新日志，90FPS和冷启动问题仍未解决。

## 0.23.19 CPU 分项诊断（2026-10-30，诊断日志已取得）

新增22个工程回调的计时：Transform过滤、输入、UI与材质桥、粒子、介绍卡、云烟、控制台、模拟、日夜和加载等。原回调正文逐字节校验保持不变，未修改SDK。定长计数数组与结构体scope不做逐次托管分配；日志每5秒汇总，单独记录双手操作帧数。仅Android Development Swan场景启用，Editor和原桌面版默认不采集；退出或暂停重置，单一所有者管理。

`SWAN_JOURNEY_CPU_WINDOW` 各项为每帧平均毫秒/单次调用最大毫秒/调用次数。它测量墙钟时间，包含回调内部原生调用和等待，不是纯CPU Running。`lateRecordedMs` 来自上一帧ProfilerRecorder，不能逐帧相减后把剩余值称为SDK耗时；混合窗口与纯静止/纯双手窗口分开。读取工具为 `Tools/Summarize-JourneyCpuTimings.py`。

目的为分清上一轮平均18.484ms的LateBehaviourUpdate来源；此包不代表FPS已改善。89项性能/加载检查与9组回归已通过，263项包内检查与v2签名通过；APK785208535字节，SHA256 `d2ec06586b7ae4cdc85985ca21bc221acf9da8900ba6456dc39d81b2dd61be10`。三个GLB与18版逐字节相同。Swan安装Success，设备确认版本19/PID24942；10月31日已取得山谷分项计时；其余原生视觉未逐项复验。改前备份 `Builds/Backups/0.23.18-before-cpu-timing/source.zip`；本轮证据 `Builds/Evidence/session-output-20261030-v2319-cpu-timing/`。

## 0.23.18 静态场景物理预算（2026-10-30）

最新真机反馈：静止约42 FPS，双手移动/缩放约30 FPS，捏合与按钮点击通过。相较17版用户报告30–35 FPS，静止体验提升，但操作期间仍明显降低。此为用户观察，不是控制视角/缩放条件的新性能追踪；性能目标未通过。

根据上一轮有效采样，Unity PhysicsFixedUpdate共2317次、平均2.132ms、累计约4.939秒；这是独立的可优化开销，不是已确认的原生CPU尖峰根因。当前生成场景与按需山谷无Unity Rigidbody、ArticulationBody、CharacterController、Cloth；Swan使用原生输入，桌面Physics射线路径已门控。新增场景所有者仅在Android Swan关闭Unity自动物理步进，保留Collider数据和机制Transform动画；发现加载场景依赖动态物理时恢复，退出也恢复原值，桌面自动行为不变。未来若新增动态实例，必须补足验证/运行时恢复，不能沿用静态假设。

9组回归、80性能/加载检查、263包内检查与v2签名通过。三个场景/山谷预制体GLB与0.23.17哈希完全相同，未删模型、降低精度或更改材质。0.23.18已安装且包管理器确认运行PID24670，存档保留、两个旧包仍在；22:58:11.085真机日志确认物理策略生效，真实渲染完成、原生交互与FPS增益仍待验。证据在 `Builds/Evidence/session-output-20261030-225000/`，交付记录在 `Builds/SwanJourney/Releases/0.23.18/`。

当前还存在原生绘制/阴影与CPU预算问题，未发现可确认生效的Unity侧原生阴影开关，不改SDK或假造API。库存几何计数包含未激活轮廓/装配网格，不能等同实时GPU三角数。伙伴detailMeshes的运行时高低精度切换与Adapter之间尚缺实际同步证据，本版未修改。

## 0.23.17 重复碰撞体清理（2026-10-30）

最新佩戴反馈：山谷静止及双手移动、旋转、缩放均为30–35 FPS；山谷、轨道、列车一起跟随，无明显操作异常。此项操作通过，持续性能未通过。没有新的受控追踪数据，不能将此反馈写成碰撞清理的量化收益；工坊交互、冷启动和资源释放仍待单独验证。

用户已确认0.23.16完整山谷、轨道、列车可以一起移动和缩放，保留该预制体方案。当前剩余主要问题是低帧率。

本轮45秒采集已结束并通知用户可以摘机。有效数据在 `Builds/Evidence/session-output-20261030-214552/performance-data/perf-data-20261030-214846/`；同目录上层有完整HTML报告与SQL证据。Unity长帧349.421ms中等待321.462ms，原生空间主线程同时运行329.952ms；对应原生帧CPU338.999ms、GPU16.733ms。绘制5678次，其中4098次阴影。由此定位原生CPU尖峰与持续渲染负担，不能从尚未细分的SpatialMFrame直接推断某个底层函数。

候选优化只改派生场景：删除642个被现有盒形命中区完全覆盖、状态/触发/材质/层相同且无脚本序列化引用的MeshCollider，减少886804个重复碰撞三角；未证明等效的1582个保留。现有刚性合并因此可处理更多安全叶节点，比0.23.16再减少90个Renderer和节点。最终APK主场景可见三角3668257、山谷532970保持不变，木材/石子像素门禁和按需峡谷预制体结构均通过。

0.23.17已经安装并启动，存档保留。9组回归、70项性能/加载检查、263项包内检查、v2签名通过。首次构建被预计数量大于1000的错误测试假设挡住，未出包；保留失败日志，改为校验真实删除计数和覆盖安全条件后全部通过。实际FPS改善、原生高光/归位和山谷操作复验仍待用户反馈，不能宣称达到90FPS。构建归档在 `Builds/SwanJourney/Releases/0.23.17/`，源码快照不包含全部美术资源，不能代替完整工程备份。

## 0.23.16 按需预制体候选（2026-10-30）

0.23.15真机失败：山谷仍独立留在物理地面；明确低头截图为 `Builds/Evidence/session-output-20261030-v2315/canyon-on-floor.png`。Unity中挂接到stage和13+4项UID自检成功，未能证明原生对象映射一致，不能作为成功验收。截图还见部分白色棋盘状表面，原因尚未独立确认。

新候选将已烘入米制的山谷保存为Resources预制体，点击出发才异步读取并在现有presentation根下实例化。保留完整山谷网格、支撑引用、材质、18组进度和手势逻辑。Build Settings仅保留轻量Boot与Workshop；旧派生Canyon场景保留为生成/检查中间产物，不再进入APK场景列表。返回工坊时销毁实例并清理引用，再请求释放未使用资源；原生缓存回收效果仍需测量。

这一调整基于附加场景路径已连续原生失败，以及本地SDK明确提供Prefab导出/实例化路径。知识库仍返回Spatial Kotlin6.0资料，不能作为Unity6.1支持证据；只读核查本地Exporter与Runtime用于理解流程，不修改SDK、不手动指定UID、不调用反编译得到的内部函数。未知的原生映射根因保持未知，最终仍以佩戴画面和真实双手操作为准。

## 0.23.15 山谷跟随修复候选（2026-10-30）

0.23.14 用户确认枕木和石子已恢复，但山谷贴住真实地面，与列车/操作台分离，无法随手势操作。日志已收到双手事件，四个山谷 UID 检查点匹配；因此不能把 Unity 位姿和 UID 检查通过当成原生跟随通过。最新截图覆盖有限，用户佩戴反馈是本轮视觉失败的直接证据。

0.23.15 保留按需加载与米制网格，导出 identity 场景容器，将山谷作为普通子节点；原生导入帧之后通过 Unity 公共 SetParent 接入可移动的展台。Follow 补偿父级比例，避免双重缩放；原场景句柄与转移后的 payload 分开管理，退出时先清理视觉/支撑引用并销毁 payload，再卸载原始附加场景。编辑器验证两轮载入、变换、返回/清理以及18组进度不变，最终 APK 另验容器/子节点和材质。此方案还须佩戴验证，不宣称已修复实际跟随、加载速度或 FPS。

0.23.14 实测阶段时间：首次资源开始到工坊交接36.770秒，点击出发到驾驶状态5.966秒；仍有明显加载等待。未取得同流程新性能采样，常态帧率尚未达到90 FPS。其修复候选备份位于 `Builds/Backups/0.23.14-before-canyon-parent/source.zip`。

## 0.23.14 修复候选（2026-10-30）

0.23.13真机验收失败：静止约33 FPS、双手约20 FPS；资源1/2满后长等；26根枕木虽在包内但实际不可见，石子全黑；山谷巨大、云/月可随缩放而地形不跟随。佩戴截图及日志在 `Builds/Evidence/session-output-20261030-v2314`。此反馈取代0.23.13此前“待验”记录。

材质：检查旧APK内最终ORM像素，木材和石子的AO红通道均为0；Unity源遮蔽在绿色通道约0.927。导出器在金属/AO共用同一图时丢失AO。现在仅给遮蔽生成独立灰度PNG，保留金属贴图与源资产，最终包用 `Tools/Inspect-JourneyNative14.py` 检查实际嵌入的ORM像素及26根枕木。不能仅根据Unity材质面板判断原生正确。

山谷：在派生网格、子节点位置和碰撞几何中烘入0.035米制换算，保持世界几何/法线/拓扑，Follow按烘入因子换算，支持双手操作。532970三角不变，编辑器实测边界误差4.33e-7米。清理无引用空叶及仅编辑器使用的溯源标记，山谷节点3974→333；工坊合批后移除1407个已证实无引用叶，9177→7770节点。初始化/真实局部运动仍同步，静止时不再跑额外整树TRS过滤。扫描与轮系闪光环复用。

加载：资源读取占前半条；暂停场景激活，在原生准备标签完整同步两帧后才允许SDK导入，避免第一段走满后无说明等待。不伪造原生百分比。这改善阶段提示，不等于已经实测缩短原生导入耗时。

观察到0.23.13山谷管理层UID存在非连续复用，尚不能单凭编号顺序断言原生映射错误；未修改SDK或分配UID。新尺度方案还需真实山谷画面及手势验收。未宣称达到90 FPS。0.23.13修改前备份：`Builds/Backups/0.23.13-before-native-fixes/source.zip`。


本文记录 2026-10-29 的代码与交接资料对应关系。“保留”表示数据、调用链或实现仍在，不是头显验收结论。**0.23.9阶段修复版本 / 9 已完成 Android 构建（Succeeded/errors=0），七组编辑器回归与263项最终APK包内检查通过，并已成功安装、启动于 Swan（PID 1140）。设备在场景载入前休眠，尚未取得本轮有效佩戴画面或 Unity 诊断，真机验收仍待结果。**首张黑屏截图不证明场景、FPS、棕块或驾驶效果。

0.23.8 已在 Swan `PB3A10MGL9240036B` 安装并启动（进程 25813）。佩戴截图确认灰黑大台面已移除、说明卡铜框与深色背景存在，但头顶大棕块仍在、正文和 FPS 未正常显示；用户随后反馈驾驶峡谷巨大且载入很慢。证据及明确的失败结论已记录在 `Builds/SwanJourney/Releases/0.23.8/release-validation.json`。0.23.7 图标交付当轮“仅安装、未启动”是历史记录，不是当前状态。

**历史失败继续保留：0.23.1 用户真机验收未通过；0.23.2 出现说明卡 shader 空值异常并 LOW_MEMORY 退出；0.23.3 也因 LOW_MEMORY 被系统终止。**0.23.4 的短时存活和 0.23.5–0.23.7 的编辑器、构建及包内检查均不等于完整原生体验通过。新版本的视觉、交互、持续运行和性能结论必须绑定本轮证据。


## 0.23.10 双击快速安装恢复（用户真机验收通过）

用户已确认：拆下一个零件、松手悬空后，看向它快速两次捏合—松开，“能带动画正常归位”。此项通过，不扩展为山谷、启动恢复或全部交互通过。

当前展示台待装件和已暂放件均可双击捏合安装；第一短击只记录，第二短击走原0.22秒归位与教学安装逻辑。有手姿时用实际手位移判拖动，释放空命中不再清掉有效双击。阈值为单次0.40秒、间隔0.50秒、位移4厘米。原顺序、暂停/验证/载入锁、取消及双手接管保留。新增29项真实应用路径的EditMode检查并纳入第8组构建检查，尚无真机双击验收。

0.23.9佩戴补验：`Builds/Evidence/session-output-20261029-v239/v239-drive-worn.png`仍有巨大山谷遮挡，APP FPS文字可见；`v239-drive-worn.log`记录总Unity切入20.358秒，其中首个原生加载帧边界18.955秒。原生尺度/加载问题未解决，0.23.10仅修改快速安装，不把此问题标为通过。此次用户已确认存档可继续进入山谷。

## 当前入口与应用身份

| 项目 | 当前值 |
| --- | --- |
| Unity 工程 | `D:/Projects/PICOVibe/unity/SteamAtelierSwan` |
| Unity 版本 | `2022.3.62f2c1` |
| 本地 SDK | PICO SpatialAdapter 6.1.0；同一 vendor 中的 PICO XR 6.1.1 |
| Android 包名 | `com.picovibe.steamatelier.v23` |
| 应用名 | `蒸汽工坊·V23` |
| versionName / versionCode | 当前0.23.10 / 10构建通过；8组回归、29项快速安装和263项APK检查通过，已安装启动，设备确认0.23.10 / 10、PID9377；双击手势待验，山谷仍未解决 |
| 原保存场景 | `Assets/AtelierJourney/Scenes/SteamJourney.unity` |
| 启动派生场景 | `Assets/SwanJourney/Scenes/SteamJourneySwan.unity` |
| 出发时附加载入的环境场景 | `Assets/SwanJourney/Scenes/SteamJourneyCanyon.unity` |
| 生成 / 构建入口 | `SwanJourney.Editor.SwanJourneyBuild.Generate()` / `BuildAndroid()` |
| PowerShell 入口 | `Tools/Build-SwanJourney.ps1` |
| 生成资源 | `Assets/SwanJourney/Generated/` |
| APK 输出 | `Builds/SwanJourney/SteamAtelier-V23-Swan.apk` |

这是独立的 V23 应用身份。旧 Swan 应用 `com.picovibe.steamatelier`（“蒸汽工坊·新版”）及更早的 `com.picovibe.steamworkshop` 不作为本次安装目标；不能通过改回旧包名覆盖它们。后续正常更新应沿用 V23 包名并递增 versionCode。

**0.23.1 历史产物记录：**该 APK 经标准 Gradle 重建，清除了增量打包留下的 ZIP 空洞，实际长度为 **1,572,376,252 bytes**；SHA-256 为 `0A1DB4BEE7BFE19C13BFD6B023EE44F6C6659FF7E35C9783EB06AA02B0624970`，设备 `pm` 曾确认安装成功。此长度、哈希和安装证据仅属于 0.23.1，不能用作 0.23.2、0.23.3 的产物身份或验收结果。新构建会复用 APK 输出路径；交付时须重新核对实际文件、版本、长度、哈希与本轮日志，不能沿用旧 `build-result.json` 或目录中已存在的 APK 宣称成功。

交接依据是工作区根目录的 `SteamAtelier_V23_1_详细功能与技术交接说明.pdf`、`SteamAtelier_V23_1.mp4` 和伙伴保存场景。伙伴版本名为 CarriageLightV23_1；V23.1 的专门修复是三节客车共 30 扇窗的夜间发光。PDF 中的 Windows/RTX 4090 数据不是本项目的 Swan 实测值。

## 0.23.9 节点一致性、说明卡与峡谷载入修复（构建与包内检查通过，真机待验）

**大棕块已有明确根因，不再沿用“可能是说明卡 Image”的假设。**0.23.8 原生 GLB 保留了五个 `EditorOnly` 云节点，Unity Player 却自动剥除了它们。SDK 按运行时深度优先遍历顺序分配节点 UID，导致后续节点整体少 5：真实 FPS 网格在 GLB 中是 9039，运行日志却报告 `visualNode=9034`，而 GLB 的 9034 恰好是 `Move whole scene touch surface` 黄铜色立方体。FPS 跟随把手势触面写到头前并应用 1.05 比例，解释了巨大棕块随视野运动、FPS 和搬台把手异常。单纯删除说明卡装饰没有解决它，0.23.8 新截图已排除该旧假设。

峡谷场景另有五个 `EditorOnly` 根及其子树，共 134 节点也会在 Player 中消失。SDK 的 UID 管理器在附加场景间共用，不能假设峡谷 root 的本地索引 0 就是运行时绝对 UID。驾驶代码在 Unity 侧已记录 `scale=0.035`；原木展盘 32.72×23.76 模型单位正常应约 1.145×0.832m，若原生根仍使用导出的单位缩放则约大 28.57 倍。现有节点错配必须先修复，不能用再乘一个缩放系数掩盖。**峡谷巨大现象的具体原生根变换仍需新版本日志和画面验证，不能仅凭 Unity 的 .035 日志宣称原生缩放正确。**

| 路径 | 0.23.9 实现 | 验证边界 |
| --- | --- | --- |
| 节点一致性 | `SwanJourneyNodeAlignment.NormalizeContent` 仅在派生内容中把 `EditorOnly` 改为 `Untagged`，保留激活状态、组件、几何及引用；拆分后校验两个场景均无 `EditorOnly` / `DontSaveInBuild`，记录 DFS 顺序和关键节点 | `node-alignment.json` 为生成门禁；`SwanJourneyNodeAlignmentDiagnostics` 只读取公开 tracker UID 与父 UID，检查 FPS、把手、卡片及放置锚点，不重写 SDK ID。运行时无 mismatch 仍不能代替画面验收 |
| 说明卡文字 | Swan 的六个正文/标题标签改走原有按钮已使用的 3D `TextMeshPro` 路径，开启 orthographic 字号度量保留原布局，把文字前移以避开板面；保留中文字体、文字、锚点、预导出层级与 14 个背景/边框网格。桌面仍使用 `TextMeshProUGUI` | 卡片位于五云之前，正文不显示不能归咎于该五节点偏移；本轮独立检查 3D TMP 网格、字号和深度，并记录 `SWAN_JOURNEY_CARD_GLYPH`。最终头显可读性待验 |
| 峡谷无损通道减重 | `SwanJourneyCanyonMeshBudget.Apply` 仅克隆派生峡谷网格至 `Generated/CanyonLean`，移除无用途的 UV1 和严格全白 RGBA 顶点色；非白颜色保留，指向同源网格的 MeshCollider 同步改为克隆。验证无 lightmap、无实际使用 secondary UV 的 detail 材质，逐项核对位置/法线/切线/其余 UV/索引/边界不变 | 本轮生成与最终GLB均已核对，详见下表。保留全部节点及三角；不改源 FBX/mesh、铁路、隧道、交互或热力机械流程。包内变小不等于已测得原生内存或加载时间下降 |
| 延迟载入诊断 | `SwanJourneySceneLoader` 增加请求、Unity async 调用/返回/完成、绑定完成、两个帧边界返回、驾驶模式应用等阶段耗时与真实 Unity anchor/payload scale 日志 | 所有阶段明确 `nativeReady=unverified`；不把 Unity 加载完成或两个 yield 当原生完成。仍仅在装配完成后点击“出发”载入峡谷，不以启动预载掩盖等待 |

本轮生成报告通过：461份独立网格中434份生成克隆，重绑1,358个MeshFilter和8个MeshCollider，426份移除UV1、367份移除全白颜色，23份非白颜色保留；Unity缓冲估计从348,324,812降至244,446,112 bytes，边界误差0、引用三角数不变。下列数据另由**最终APK中的实际GLB**读取，已排除“只改Unity配置，导出器又补回默认通道”的情况。

| 最终包内指标 | 0.23.8 | 0.23.9 | 差异 |
| --- | ---: | ---: | ---: |
| 峡谷 GLB bytes | 385,188,688 | 280,956,944 | 减少104,231,744（约27.1%） |
| 峡谷独立几何缓冲 bytes | 349,144,712 | 245,243,992 | 减少103,900,720（约29.8%） |
| 峡谷节点 / 引用三角形 | 3,974 / 4,720,438 | 3,974 / 4,720,438 | 不变 |
| 启动场景 GLB bytes / 节点 | 291,741,148 / 9,073 | 291,750,264 / 9,073 | 增加9,116 bytes；节点不变 |

这些是资源数据量的真实下降，尚不是设备运行内存、GPU负载或加载速度测量。峡谷几何仍约245 MB、472万三角形，后续是否需要进一步优化须由载入阶段日志与原生性能证据决定；本轮未采用较低LOD。

0.23.8 日志的 `UNITY_READY` 为 22:47:17.171，驾驶模式日志为 22:47:34.485，相差 17.314 秒。旧代码之间只有两次帧等待与暂停门控，并未主动等待 17 秒；期间有图像解码/内存分配日志，帧末原生载入是重点检查对象，尚未获得完整 CPU/GPU 耗时拆分。驾驶首个十秒窗口 appUpdateHz=8.7、APP delta p99=4944.45ms，后续窗口约 30.6/33.9Hz。这些是 Unity APP 数据，不能当合成器 FPS；新版本的实际载入与运行改善仍待相同流程验证。

**0.23.9 正式Android构建Succeeded/errors=0，七组聚合回归全部通过，其中附加场景检查78项通过；最终APK反馈检查263项通过，含节点一致性与两份场景检查。**`canyon-mesh-budget.json`、`feedback-checks.json`、`deferred-checks.json`以及本轮包内读取支持上述结论。最终签名/版本/哈希与安装记录见 `Builds/SwanJourney/Releases/0.23.9/release-validation.json`；**棕块消失、说明卡/FPS/把手、峡谷尺寸、实际载入速度和持续运行仍待真机验证。**源码修改前副本在 `Builds/Backups/0.23.8-before-node-and-canyon-fixes/`；原始0.23.8产物与失败验收记录继续保留。

本轮安装返回Success，启动指令成功；`Builds/Evidence/session-output-20261029-v239/v239-installed-app.json` 确认设备 `PB3A10MGL9240036B`、versionName `0.23.9`、versionCode `9`、running=true、PID1140。对应证据为同目录 `v239-install.log` 与 `v239-launch.log`。设备随后休眠，场景尚未出现有效画面，`v239-first-view.png` 黑屏不能作为原生通过证据；等待佩戴及点击“出发”后的诊断和反馈。此次未重置应用数据。

## 0.23.8 用户反馈修复与验收边界（已安装，原生验收失败）

本轮源文件修改前保存在 `Builds/Backups/0.23.7-before-feedback-fixes/`。包名继续是 `com.picovibe.steamatelier.v23`，不会覆盖另外两个旧应用；伙伴原保存场景、模型、源材质及 SDK 不做替换。热力、气动、机械联动、18 组装配顺序与教学规则不因本轮视觉修复而改变。

| 路径 | 本轮实现 | 已知边界 |
| --- | --- | --- |
| 说明卡 | `StudioPartCards` 的原 TMP、中文字体、Canvas 和锚点子树在生成阶段完整序列化；实际 `Start()` 复用预导出对象，不重建空卡片。`SwanJourneyCardExport` 只将两张卡片的 14 个装饰 Image/CanvasRenderer 转为已有普通网格表面，保存原深色面板、铜色边线和 alpha | 0.23.8 保留文字节点的 CanvasRenderer 与原层级；佩戴截图确认铜框/背景显示，但正文未正常显示。0.23.9 的独立 3D TMP 修复见上节 |
| 空中棕块 | 0.23.8 去除 Image 装饰后棕块仍在，因此该假设被排除；随后确认五个 EditorOnly 云造成 UID 错位，FPS 误更新 MoveWorkbench 黄铜触面 | 没有批量删除 Cube 或功能部件。根因证据及 0.23.9 节点一致性修复见上节；棕块消失仍需新版本画面确认 |
| 灰黑大台面 | 派生生成只剥离 `Presentation backdrop/Neutral display floor` 和 `Surface V2 baked environment/Baked local tabletop receiver` 两个已核对叶节点的 Renderer/MeshFilter/Collider，并清理控制器的相应 floor 引用 | 0.23.8 佩戴截图已确认大台面移除。根节点、原源资产、铁轨木底座、仪器浅台、峡谷展盘和驾驶操作台保留；支撑边界断言通过不等于桌面吸附已复验 |
| FPS | 保留 `SwanFpsMeshText` 普通网格字形与独立头部跟随根；V23 专属材质改为 `Unlit/Color`，颜色 (.65,1,1)，偏移 (-.14,.10,.65)m、视觉比例 1.05 | 最终 APK 须检查实际 `KHR_materials_unlit`，不能以 Shader 名代替导出证据。显示仍是 Unity APP 更新频率，不是合成器 FPS；位置、亮度、跟随待真机确认 |
| 单手搬台把手 | `SwanJourneyPresentation` 将把手邻接到工坊“一键拆开”行和驾驶“车灯”行，并维持至少 .32×.06m 的触面；与邻居留 .025m 世界间距，稳定状态缓存边界和布局 | 保留共同 stage 所属、hover、手势分派和桌面约束；不可缩入会隐藏的内容根。此处是把手最小尺寸保护，不改变整场景 0.26–1.5 缩放范围 |
| 重复相机绘制 | Swan 分支保持原空间渲染，`SwanJourneyRuntimeDiagnostics.EnforceNativeCamera` 强制 Unity 预览相机 `cullingMask=0`；检查调用真实 `BuildUI/UpdateText` 路径，防止作者 UI 初始化恢复相机绘制 | 这是源码确认的重复绘制风险修复，尚无 GPU trace 证明它是严重卡顿的唯一原因。原桌面路径和原生 SDK 不改 |
| 烟雾与软粒子 | 生成的 Surface/Emission 图分别携带透明密度与发光密度，边界 RGB/alpha 明确归零，所有软效果保持 Standard BLEND。烟团使用 216 三角形圆盘交叉网格，流路标记使用 48 三角形圆盘网格；烟、光束和光晕发光随 alpha 衰减 | 旧 APK 的 baseColorTexture 与 alpha 本来存在且 BLEND 正确；未遮罩发光、非零边框和方片轮廓是已核对风险，不等于唯一根因已证实。Renderer 数量不增加，但三角数略增，不能据此承诺性能提升 |

七束火床保持 224 三角形和既有燃烧控制；125 条 CPU 流路、固定 288 标记池、每路径最多 12 样本、原生命周期、隧道裁剪、蓝色扫描和金色完成反馈均保留。软粒子测试覆盖贴图边缘、混合状态、圆形 UV 范围、生命周期 alpha/emission 同步归零与重用恢复；这是资源和逻辑检查，不是原生透明排序验收。

`BuildAndroid()` 在生成后执行 `SwanJourneyFeedbackChecks.Run()`，分别重开派生场景运行 visibility、prepared-cards、native-camera、TMP、hidden-material-updates 和 flow-lifecycle，再恢复主场景进入构建；不保存测试修改、不进入 Play/P2D。生成阶段另执行 FireChecks、SoftParticleChecks 和台面移除断言。首次卡片生命周期检查失败已记录；修正后的最终轮已通过全部六组检查并成功打包。**此前候选产物的包内检查失败，后来最终APK通过包内检查并安装；但0.23.8原生验收仍失败，暴露节点错位、卡片正文/FPS缺失和驾驶载入问题。**

**包内门禁发现并修正的发光导出遗漏：**此前一次 0.23.8 APK 虽然 Android 构建 Succeeded/errors=0，32 份 Flow 调色板资源仍缺失 emissiveTexture/emissiveFactor，因此没有按合格交付包处理。名称“alpha 0”是第0档、实际 alpha=.04，不是完全隐藏材质，不能豁免。`Effect()` 原先只启用 `_EMISSION`，但保留默认 `EmissiveIsBlack` 标志；本地 Unity `StandardShaderGUI.SetMaterialKeywords` 会据该标志清除关键字，SDK 导出器又按关键字决定是否导出发光图。已使生成效果材质与主转换路径一致，显式设置 `RealtimeEmissive`，并增加 SaveAssets→同步强制 reimport→再次保存→重新加载及读取实际 `.mat` YAML 的关键字/标志校验。本修正前文件另存于备份目录的 `before-exportfix/`。该修正已纳入最终APK并重新通过包内检查，前一候选包的哈希不能作为本次交付身份。

发光非零断言按用途区分：32份Flow与动态Steam/Mist/Furnace/Beam/Halo要求非零因子；原Emission为黑、运行时不更新emission的静态Particle允许glTF默认黑因子，但仍要求BLEND与遮罩。`Completion glints` 属于后者，Swan的native完成回调还会绕过原CreateSparks；不能为满足过宽断言而给原本不发光的粒子添加亮度。

本轮报告已核对：visibility 27 项、说明卡 68 项、相机路径 10 项、UI 130 项、隐藏更新 72 项、流路 22 项通过；生成阶段火床 15 项、软粒子 254 项通过。软粒子检查覆盖 54 份材质的同步重新导入与持久化、9 张纹理、5,084 个边框像素；台面断言为移除 2 个 Renderer、2 个 MeshFilter、1 个 Collider，四组支撑边界不变；卡片报告为一个 Canvas 中14个装饰Image转换为14个普通网格表面。`feedback-checks.json` 的 `passed=true`，但 `nativeRenderingValidated=false`、`devicePerformanceMeasured=false`。这些通过结果只能证明本轮编辑器和资源条件，不证明真机画面或性能。

**最终构建与包内验收已完成。** APK为 **1,139,973,075 bytes**，SHA-256 `45FE93FD2763547A72045C4D8C64F9CC7F173A92FB3A13CBFE2030436D7C4430`；Android构建 `Succeeded/errors=0`，v2签名通过，aapt确认 `com.picovibe.steamatelier.v23`、versionName `0.23.8`、versionCode `8`。`feedback-export-checks.json` 共238项通过，包括32份Flow独立材质资源及4份仅注册软效果资源的分用途发光校验。完成包内验证的是实际GLB内容，不仅是Unity材质配置；仍不证明设备上的透明排序、文字位置或帧时表现。

最终APK与报告归档于 `Builds/SwanJourney/Releases/0.23.8/`，安装件是 `SteamAtelier-V23-Swan-0.23.8.apk`。该包已安装并启动，归档 `release-validation.json` 记录包内扩展复验239项通过、`nativeAcceptance.passed=false`。**佩戴画面只确认灰黑大台面移除；大棕块、正文/FPS及峡谷尺寸/载入仍有问题。不能把资源门禁通过说成完整体验可交付。**软粒子的透明排序与长期性能没有获得完整通过证据。

| 本轮证据/入口 | 用途和限制 |
| --- | --- |
| `Builds/Evidence/session-output-20261029-v238/v237-cube-worn.png`、`v237-baseline-app.log` | 0.23.7 反馈基线；不是 0.23.8 通过证据 |
| 同目录 `v238-first-view.png`、`v238-second-view.png`、`v238-worn.log`、`v238-drive-report.log` | 0.23.8 已安装佩戴证据；确认台面移除，同时记录棕块、文字/FPS和驾驶问题，不能当0.23.9验收 |
| `desktop-worktop-removal.json`、`card-native-mesh-export.json`（均在 `Builds/SwanJourney/`） | 派生台面/卡片转换对象及支撑边界；须对应当前生成轮次 |
| `soft-particle-checks.json`、`fire-checks.json` | 生成纹理、材质、网格及原控制引用检查；nativeRenderingValidated=false |
| `feedback-checks.json` 及各分项报告 | 本轮聚合回归门禁，失败即停止正式打包；旧文件存在不能代替本轮成功 |
| `Tools/Inspect-JourneyFeedbackExports.py` | 完成后直接核对最终 APK 的 Unlit FPS、软粒子 BLEND/发光遮罩、卡片装饰与两份场景；包内正确仍需真机画面复验 |
| `Tools/Build-SwanJourney.ps1 -RenderPathChecksOnly` | 独立执行实际初始化/更新的相机门控回归，不代表设备渲染耗时测试 |

## 0.23.7 蒸汽机车应用图标（历史交付记录）

用户要求替换 Unity 默认 APP 图标。本轮使用内置 image_gen 生成蓝黑背景、黄铜机身与红色车轮的蒸汽机车图，并分别配置原图、Round 留边和 Adaptive 更大安全留边三种构图，以适应 launcher 遮罩。最终构建已使用以下独立资源绑定。

| 用途 | 最终资源与层 |
| --- | --- |
| Unity Default / Android Legacy | `Assets/SwanJourney/Branding/SteamAtelierIcon.png` |
| Android Round | `Assets/SwanJourney/Branding/SteamAtelierIconRound.png`，保留圆形遮罩留边 |
| Android Adaptive background，layer 0 | `Assets/SwanJourney/Branding/SteamAtelierIconAdaptive.png`，采用更大的安全留边 |
| Android Adaptive foreground，layer 1 | `Assets/SwanJourney/Branding/SteamAtelierIconTransparent.asset`，由配置器程序生成的 4×4 全透明 Texture2D |

透明前景层避免把同一幅机车重复放进两层；完整构图位于背景层。`SwanJourneyIcon.Configure()` 设置 Unity 默认 1 个槽及 Android Legacy/Round/Adaptive 共 18 个尺寸槽，并输出 `Builds/SwanJourney/icon-config.json`，记录资源路径、槽位尺寸与层数。Builder 每次 `Configure()` 都调用它，防止后续生成/构建恢复默认图标；不要仅手动修改 PlayerSettings。图标 PNG 按方形、sRGB、无 mipmap、无压缩导入，透明层作为独立 Texture2D asset 保存。

普通 Unity/Android 构建路径、`Tools/Build-SwanJourney.ps1`、V23 包名及场景入口保持原值；版本增至 0.23.7/7。本轮不修改游戏流程，也未重复跑 0.23.6 的 72 项 HiddenChecks 或 22 项 FlowChecks。配置报告只证明编辑器图标槽设置；本轮另完成包内图像、XML 层顺序与签名验证，结果见下文。图标交付当轮完成本版头显安装、未启动应用；后续 0.23.7 使用反馈见上方 0.23.8 修复记录。

最终 Android 构建为 **Succeeded / errors=0**，APK **1,138,757,816 bytes**，SHA-256 `7A5AC446044477B119EBF520C109123E32DA758B83CE8D81F7103DDBB2DC2F68`。aapt 确认包名 `com.picovibe.steamatelier.v23`、versionName `0.23.7`、versionCode `7`；两份场景 GLB 检查通过，几何数量与 0.23.6 一致。这是本轮产物与场景完整性证据，不是原生启动、手势或性能验收。

**v2 签名与包内图标验证已通过。** APK 包含 24 张多密度 PNG；抽取的默认、圆形和 Adaptive 背景图可视确认为机车，432×432 Adaptive 前景逐像素 alpha=0，manifest 及两份 Adaptive XML 的资源映射正确。结果记录在 `Builds/SwanJourney/icon-export-checks.json`；发布记录已更新，0.23.7 APK 与报告归档于 `Builds/SwanJourney/Releases/0.23.7/`，归档哈希与最终文件一致。这些是包内资源验证，真实头显启动器遮罩和显示仍未验收。

**2026-10-29 已安装到 Swan，未启动。** 使用已核对大小与哈希的归档 `Builds/SwanJourney/Releases/0.23.7/SteamAtelier-V23-Swan-0.23.7.apk` 更新设备 `PB3A10MGL9240036B`，安装返回 `Performing Streamed Install / Success`、exit 0。随后 app info 确认 `com.picovibe.steamatelier.v23`、versionName `0.23.7`、versionCode `7`、`running=false`。旧应用 `com.picovibe.steamatelier` 0.5.6/24 与 `com.picovibe.steamworkshop` 0.1.2/3 均保留。证据为 `Builds/Evidence/v237-install.log` 与 `Builds/Evidence/v237-installed-app.json`。按用户要求，本轮只安装供其回家查看；没有启动或新增真机画面、手势、稳定性和性能验收。

## 0.23.5 两场景边界与载入保护

`SwanJourneySceneSplit` 从派生版中拆出当前 `Mesa original canyon · miniature railway V12` 子树，形成独立峡谷场景。启动场景继续持有完整工坊、18 组零件与预览、共用机车、三节车厢、驾驶控制器/控制台、天空与日月逻辑；它保留原轨道模拟和共享状态机，只推迟大峡谷/铁路线环境的几何载入。不是把完整沙盘玩法或第二套列车复制到另一个场景。

`scene-split.json` 本轮记录启动场景 4,338 个 MeshFilter / 3,655,454 三角形，峡谷场景 1,385 个 MeshFilter / 4,720,438 三角形。生成阶段检查两场景之间没有序列化对象交叉引用；启动场景改持有空的 `Deferred canyon placement anchor`，原桌面扫描的 958 条 contour 资源引用仅在派生版确认 native reveal 绑定后清空。**SDK 全局材质注册保持原样**；这不是所有纹理的延后载入，也不能由几何比例推导设备内存收益。

`SwanJourneySceneLoader` 在 18 组完成之后通过 `SwanModeRequest` 拦截出发，先进入 Loading；原 `SetMode` 的清理/重置逻辑尚未执行，因此等待或失败不会清空装配进度。Loading/Unloading 期间，`SwanSceneLoading` 阻断教学动作、重置及连续控制，并显示载入提示。Unity 场景完成并绑定 payload 后才继续原模式切换；如果 Unity 载入或绑定失败，进入 Failed，保留工坊进度、显示失败提示并允许再次出发。返回工坊时先解除峡谷材质/renderer 引用并恢复核心支撑集合，让共用列车回到工坊后卸载独立场景；应用暂停时延后继续进入沙盘。

峡谷 payload 始终是自身场景中的根，不能 parent 到启动场景；`SwanJourneyEnvironment.Follow` 将它对齐到主场景 anchor 的世界位置、旋转和均匀正缩放。`SwanJourneyPresentation` 执行顺序为 **2150**，早于 `JourneySceneArrival` 的 **2200**：`ApplyModeNow` 先切换 0.12/0.035 内容比例，再通过实际回调同步峡谷，`RefreshFootprint` 在桌面高度校正后再次同步，避免扫描范围使用上一帧姿态。Attach/Detach 合并或恢复支撑面集合，驾驶控制台与共用对象不随峡谷卸载。

本轮 `-SceneOnly` 已通过；`-DeferredChecksOnly` 修正 EditMode `SendMessage` 断言后重跑 **78 项 PASS**，实际打开/关闭峡谷场景两轮，确认 **459 个峡谷独占 mesh 资产均不在启动场景依赖中**。检查包括 18 组状态与对象归属、veto/忙状态输入保护、重复 Attach/Detach、支持面去重、实际 `ApplyModeNow` 和 `RefreshFootprint` 回调同步。检查不进入 Play/P2D、不保存测试场景；`nativeRenderingValidated=false`、`asyncLifecycleValidated=false`、`memoryReleaseValidated=false`。

Unity 的异步完成和等待帧数不是 native 场景可见的完成信号；`Resources.UnloadUnusedAssets` 结束也不是 native 缓冲已释放的证明。原生载入、返回卸载、反复出发、暂停恢复与实际内存收益仍待运行证据。0.23.5 当轮 UiChecks 124 项已通过（编辑器），APK 构建、签名和包内检查通过；该版未安装。0.23.7 图标交付时已安装但未启动；后续佩戴反馈已记录在 0.23.8 修复节，真实完整验收仍未通过。

### 0.23.5 历史构建与包内检查

Android 构建为 Succeeded / errors=0；APK **1,138,164,359 bytes**，SHA-256 `15101E1F727672E23AD4BF7DCDB064CC704D07913076B03187B3C51CDE251161`，v2 签名验证通过。aapt 核实 `com.picovibe.steamatelier.v23`、0.23.5/5、应用名「蒸汽工坊·V23」、SpatialAdapterPlayerActivity 与 arm64-v8a。版本安装包及本轮报告归档于 `Builds/SwanJourney/Releases/0.23.5/`。

`Tools/Inspect-JourneySceneExports.py` 直接读取 APK：仅有两个 `.unity.glb`，启动场景 **291,457,808 bytes**，峡谷场景 **385,188,688 bytes**；启动 GLB 没有 canyon root。对比 0.23.4 启动 GLB 的 **676,656,864 bytes**，启动场景导出文件减少 **56.93%**。峡谷含 4,720,438 个引用三角形、349,144,712 bytes 独立几何缓冲。这是打包资源分区证据，不是实测启动内存、VRAM、FPS 或运行时 native 释放收益；所有关卡仍保存在 APK 内，因此总体安装包不会随启动场景同比缩小。

0.23.5 当轮未安装和运行该版，当时最后确认的设备安装为 0.23.4；当前已安装并启动版本为上文的 0.23.9，待有效佩戴验收。原生两场景往返、FPS/文字/透明火焰和持续性能尚未完整通过验收。

## 0.23.6 隐藏更新优化候选

这轮调整针对源码中重复执行的显示工作，未改变原热力、气动、阀门/轮系、教学验证、驾驶或存档的顺序。作者脚本新增门控仅在 `SwanInputActive && !inSandbox` 生效，桌面分支保留原行为。尚无帧时证据证明这些更新是主要瓶颈；不能由调用次数减少推导 FPS、GPU 或内存收益。

| 路径 | 当前候选实现 | 必须保留的恢复与清理 |
| --- | --- | --- |
| `MesaEnhanceV15` / `MesaAtmosV16` | 工坊跳过隐藏云姿态/属性块和 42 槽烟团遍历；时钟、Evolution 仍推进。已初始化、启用且属于同 journey/sky 的 V16 接管前三片云时，V15 跳过重复写；缺失/禁用时回退 | 冷隐藏/退出只清理一次 dust/plume/puffs、beam/halo/flare；恢复原工坊 smoke。可见首帧继续当前时钟，不能冻结或重置教学状态 |
| `SteamJourney` / `StudioSpatialRefineV19` | 工坊跳过驾驶显示、仪表文本、车窗与控制台显示变换；可见文字只在显示值变化时写入，缓存键与输出共用同一量化值 | 冷隐藏/退出一次关闭驾驶灯、cabLight、窗口发光、返回盖与车厢火花；重新显示首帧用最新汽门、制动、方向、圈数、压力、速度和昼夜值 |
| 可见灯泡与车窗的 MPB | **工坊仍可见的机车灯泡每帧写黑；沙盘可见车窗每帧写当前 emission**，不做一次性或值去重提交 | 原生桥消费后会清空块；可见灯泡若只写一次黑，下一帧会回到材质默认强发光。真正隐藏的灯泡可以跳过后续写入 |
| `SwanJourneyVisualBridge` | 在 `GetPropertyBlock`/清空前跳过 disabled 或祖先 inactive 的 renderer，保留最后 pending 值；显示首帧强制写；输出颜色/emission 未变时不重复写自有 Material；keyword 在实例创建时设置一次 | 每帧仍从 definition/originalEmission 和本帧 MPB 计算，不累计混合缓存颜色；不依赖 Unity `isVisible`，不改扫描/金色临时 sharedMaterials，取消、禁用和卸载仍执行原清理 |

`SwanJourneyHiddenUpdateChecks` 汇总三个临时 EditMode fixture：云烟的真实 Awake/LateUpdate、驾驶显示的连续 24 次隐藏更新/退出/重入/桌面分支，以及桥的 disabled/祖先 inactive、pending 最新参数、可见恢复、稳定输出不写材质和 arrival/gold 材质归还。可见窗口与灯泡测试模拟 MPB 已被桥清空的下一帧。本轮重跑 **72 项 PASS**，日志没有 Assertion/异常。首轮仅因 fixture 对 MPB 浮点往返使用严格 `==5` 失败，实际读回为 5.00000048；修正为 `1e-4` 容差并保留实际值断言信息后通过，不能将这个测试容差问题写成运行时代码缺陷。

桥的 `LastAnimatedVisited`、`LastHiddenSkipped`、`LastPropertyBlockReads`、`LastMaterialWrites`、`LastPropertyBlocksConsumed` 记录源码工作次数。本轮报告中 `stableHiddenMaterialWrites=0`、`stableHiddenPropertyBlockReads=0`、`stableVisibleUnchangedMaterialWrites=0`，对应测试帧的实际源码调用；它们不测量设备 FPS、native 提交成本、GPU、启动内存或异步资源释放。原生视觉、亮灭恢复和真实往返流程继续保留 pending；本轮没有要求用户回来戴机。

### 0.23.6 历史构建记录

0.23.6 当轮 HiddenChecks 72 项、CPU FlowChecks 22 项及生成时火床 15 项通过；网格导出检查通过。Android 构建为 Succeeded / errors=0，APK **1,138,168,607 bytes**，SHA-256 `F487603BC15BAB9D48F3FBEAF8423DC701B9F884B0D88A6EC68697414E29303D`，v2 签名通过。aapt 核实 `com.picovibe.steamatelier.v23`、0.23.6/6、SpatialAdapterPlayerActivity、arm64-v8a；包内只含两个派生 scene GLB，启动 291,457,808 bytes、峡谷 385,188,688 bytes，与 0.23.5 的资源规模相同。这些检查与哈希不能作为 0.23.7 的本轮结果。

交付件和当轮报告归档在 `Builds/SwanJourney/Releases/0.23.6/`；0.23.5 保留用于后续对比。修改前脚本备份在 `Builds/Backups/0.23.5-before-hidden-updates/`。0.23.6 当轮没有安装或操作头显，当时最后确认安装为 0.23.4；0.23.7图标交付时已安装但未启动，后续反馈见0.23.8修复节。实际 FPS、工坊/驾驶/返回的原生视觉、真实手势和持续性能仍待佩戴验收，不能由编辑器零次冗余写入推算设备提升比例。

## 0.23.3 LOW_MEMORY 失败与 0.23.4 候选

0.23.3 的历史 APK 为 **1,572,448,046 bytes**，SHA-256 `C566BAAA657655318442F0D049B1006291FC5D88DBDEA0FDF4D116DFB8278A2E`，构建 Succeeded/errors=0、签名和安装已确认。其进程 PID 20085 在 08:36:01.943 启动；08:36:10.183 报告 TMP world=97、active=26、invalidMeshes=0；08:36:38.372 的系统退出记录为 `reason=3 (LOW_MEMORY)`、RSS **2.4GB**、importance=100。系统日志同窗记录低内存 nowOomLevel 从 100 到 0，并在 08:36:38.340 记录前台 TOP 进程死亡。0.23.2 PID 13739 也于 08:21:45.358 因 LOW_MEMORY 退出，RSS **2.2GB**。这些是两次运行的圆整 RSS 记录，不是泄漏增长曲线。

直接终止机制已由系统事件确认，但具体分配来源和 LMK 阈值未确定。退出后 meminfo 中的空闲 RAM、Spatial/OpenXR RSS、graphics/DMA-BUF 等全系统值不能归属到该应用，也不能还原被杀时刻；当前没有 trace、分配栈、GC/STW 或 GPU 执行数据，不能宣称纹理/GPU/GC 已被证明为根因。0.23.4 将列车主色上限降至 1024，其余法线/数据/发光/环境纹理上限为 512；资源估算从 **754,996,428** 降至 **226,514,124 bytes**，约降 70%，这不是实测 VRAM。只有相同场景下的持续存活、退出原因与实测内存比较才能验证该实验。

本次只分析现有日志，未采集新 trace。Memory 主题、完整主题路由记录、根因分类、输入副本与哈希位于 `Builds/Evidence/session-output-20261029-084000/`；可阅读 [HTML 报告](../Builds/Evidence/session-output-20261029-084000/report.html) 和 [分析汇总](../Builds/Evidence/session-output-20261029-084000/final-summary.md)。PerformanceKB 后端不可用，未用一般 SDK 知识服务替代；报告仅依据实际日志。0.23.3 发布记录继续绑定其历史 APK，后续产物不能复用该哈希。

## 0.23.3 已实施修复与复验边界

0.23.3 保留 0.23.2 引入的 TMP、空缓冲、软火床与共同移动把手改动，并补充真实说明卡启动路径的材质绑定与检查。

| 历史反馈或证据 | 当前代码改动 | 尚需确认 |
| --- | --- | --- |
| 0.23.2 `StudioPartCards.Build()` / `Start()` 抛出 `ArgumentNullException`，参数 `shader`，未完成世界空间说明卡配置 | Builder 为 `StudioPartCards.spatialLeaderMaterial` 序列化绑定已生成的 Standard 材质；Swan 分支克隆该材质创建引线，不再在设备上查找 `Universal Render Pipeline/Unlit`。颜色修改作用于自有克隆，销毁时清理，原桌面分支保留 | 新 APK 启动无此异常，说明卡完成世界锚定，标题/说明与引线正常；不能仅凭编辑器有该 shader 就宣称设备已修复 |
| 文字、说明卡和面板错位 | `SwanJourneyNativeUi` 不再创建 `SpatialAdapterNativeText` 副本，保留原 TMP 中文字体、层级、局部位置、尺寸和显隐状态；世界 Canvas/Image 与卡片背景网格保留 | 两眼、近远观察、移动/旋转/缩放后的文字对齐和可读性 |
| 设备持续出现 `Need Positions` / `Empty Indices` | TMP 上传前补齐空缓冲：合法空文字使用微小全透明三角形，实际 `.text` 不变；内容变化才发 dirty 信号，停用或稳定空文字不持续排队 | 新版运行日志的错误是否停止，动态中文/空白/重新显示是否正常；不能仅凭守卫代码声称修复完成 |
| 原生火焰需要修正 | 仅派生版本关闭已被原 `flameVolume` 替代的旧 fire 发射器和其 renderer，保留引用；生成七束软透明火舌，共 224 三角形，仍由原燃烧强度和时间驱动 | 火床轮廓、透明排序、强度和完整教学过程，非原体积 shader 等价移植 |
| 单手移动入口找不到，且原按钮随工坊隐藏 | 新建共同 stage 根下的“捏住移动整个场景”把手，亮铜色，基础宽 0.48 米、高 0.09 米；`SwanJourneyPresentation` 根据当前支撑范围将其放到前左侧，两模式均保留，继续走既有桌面约束和输入分派 | 工坊/沙盘两模式中的可见性、注视高光、真实单手捏住拖动与桌面吸附 |
| FPS 为方框、视觉上不跟头 | 保留与已验证旧入口相同的 `SwanFrameRateHud` / `SwanFpsMeshText` 路径；两版 APK 字形顶点/索引一致，Unity 日志显示头部与 HUD 位置变化 | **尚未证明修复。**需在 native 资源错误处理后复测真实画面；Unity 姿态变化和 tracker valid 均不等于原生节点已正确显示 |

0.23.1 原始设备反馈对应 `Builds/Evidence/v23-worn-input.log` 与 `v23-live-resource-errors.log`。后者记录 APP FPS 从 21.7 降至 8.7，同时存在大量 mesh 资源错误；这是待调查的运行证据，不能据此直接断言 FPS 方框或跟随失败的唯一原因，也不能归咎于单条启动时 null 警告。0.23.2 的独立失败证据是 `Builds/Evidence/v232-startup.log:441`：`ArgumentNullException` 的参数为 `shader`，堆栈明确经过 `StudioPartCards.Build()` 和 `Start()`；其后 UI health 显示 `invalidMeshes=0` 也不能抵消说明卡初始化失败。

`-UiChecksOnly` 在 0.23.3 增加对真实 `StudioPartCards.Start()` 的执行检查，不再只依靠合成文字样例。该版 **124 项通过**，范围限于编辑器中的启动、布局和上传缓冲逻辑；设备启动捕获未重现该 shader 异常，但随后 LOW_MEMORY 退出。0.23.1、0.23.2、0.23.3 均没有完整原生验收通过，后续版本也不能沿用该编辑器结果作为设备通过证据。

## 原场景与派生场景的边界

原保存场景、模型、材质、音频及其 `.meta`/GUID 保留。生成器打开该场景后，在内存中制作 Swan 版本并另存到 `Assets/SwanJourney/Scenes`。不要直接维护生成场景，也不要运行伙伴全量重建脚本恢复其已隐藏的旧环境。

原脚本已有必要的坐标转换、旧输入门控和可选原生效果回调补丁。这些是有意保留的兼容改动，不能重新复制伙伴脚本将其覆盖。`spatialRoot == null`、原生回调未注册时，应维持桌面默认行为；派生材质转换和资源清理不能回写原材质或源场景。

| 责任 | 代码位置与约束 |
| --- | --- |
| 组装、状态、进度、热过程、驾驶 | `Assets/AtelierJourney/Scripts/SteamJourney.cs` 及原 partial；`LocomotiveController` 保留机械姿态求解 |
| 原生动作桥 | `Assets/AtelierJourney/Scripts/JourneySwanInputBridge.cs`；通过原状态机安装、拆除、验证和驾驶，不另建一套模拟 |
| 输入仲裁 | `Assets/SwanJourney/Scripts/SwanJourneyInteraction.cs`；原生 `SpatialInputDevice` primary/secondary，以 deviceId + interactionId 区分触点 |
| 全局坐标 | `JourneySpatialRevision.cs` 的 `spatialRoot` 和 Scene/World 转换；原场景数值保持在共同内容根的局部坐标中 |
| 模式与支撑 | `SwanJourneyPresentation`、`SwanJourneyScenePreparation`，复用 `SwanTabletopPlacement` |
| TMP 上传与原生效果 | `SwanJourneyNativeUi` 保留原 TMP 并守护上传缓冲；`SwanJourneyVisualBridge`、`SwanJourneyMaterials` 负责效果 |
| 派生清理与静态合批 | `SwanJourneyScenePreparation`、`SwanJourneyStaticBatching`；保留当前几何和运行时引用 |
| 峡谷延后载入 | `SwanJourneySceneSplit` 生成边界，`SwanJourneySceneLoader` 管理忙状态与绑定/卸载，`SwanJourneyEnvironment` 保持独立场景归属并同步姿态 |

共同操作根为 `蒸汽工坊·V23 · Swan workbench`；其下 `V23 partner composition` 初始朝向 Y=180°，工坊局部尺度为 0.12，沙盘为 0.035。整个展台再由双手统一缩放。太阳/月亮轨道、烟云、扫描、灯束、水反馈和隧道空间场通过公共根转换；不要用世界坐标常量替代原局部数值。当前转换约定为均匀、正缩放。

## 保留的 Swan 操作

用户再次明确要求保留**注视命中高光**和**双击捏合归位**；两者是后续修改必须维持的功能，真实设备上的命中与手势体验仍单独验收。

| 操作 | 当前实现与使用边界 |
| --- | --- |
| 注视命中高光 | 零件预览、已安装零件、控制器、日月挂公开 `SpatialAdapterHoverEffect` 和碰撞体；使用平台 hover 命中反馈，不能把头部指向说成已验证的眼动追踪 |
| 捏合拿取 / 松手安装 | 使用原吸附判定与教学先后顺序；拿取已安装零件并移动可拆下，保持原流程失效/恢复规则 |
| 双击捏合快速归位 | 对同一个已暂放或拆下的零件连续短捏合；目前单次 ≤0.28 秒、两次间隔 ≤0.40 秒、移动 ≤2.5 厘米；约 0.22 秒飞回，仍受模块顺序限制 |
| 一键拆开 | 工坊新增“一键拆开”按钮；已安装的组按原拆除逻辑展开，可逐件拿取、按顺序归位 |
| 双手移动 / 旋转 / 缩放 | 任一触点命中展台后，两手捏合接管公共根；平移取双手中点，旋转为水平转动，比例相对初始展台为 0.26–1.5；接管、取消、失焦会释放原操作，primary/secondary 不代表固定左右手 |
| 单手搬动展台 | 自 0.23.2 引入并在 0.23.3 保留的“捏住移动整个场景”把手是 stage 根直属子级，工坊与沙盘均保留；按模式支撑边界放到前左侧，移动同一个公共根并经过桌面约束；待真机复验 |
| 桌面吸附 | 沿用 PICO 空间网格感知与支撑采样：接近桌面约 6.5 厘米可吸附，留 4 毫米间隙，上提约 5.5 厘米脱离；范围不足时限制移动/缩放并提示，不依赖虚拟房间地板 |
| 独立 FPS | 沿用 `SwanFrameRateHud` + `SwanFpsMeshText` 网格字形，位于展台根之外，代码按 `XRNode.Head` 更新；显示 **APP FPS**，统计 Unity 应用更新频率；0.23.1 视觉跟随验收失败，0.23.8 已调整 Unlit 材质与近视域偏移，待本轮真机复验 |

桌面权限为 `com.picovr.permission.SPATIAL_DATA`，传感器等待 `SwanSpatialInputMode.Ready`。拒绝权限时仍可手动摆放，不反复弹窗。双手变换不会同时变换 FPS；不得将 FPS 改回 NativeText 或将 Unity XR 头部姿态再次套原生导出坐标逆变换。

四个支撑 Transform 的路径如下，启动场景路径相对于伙伴内容根；0.23.5 的峡谷托盘改归独立场景 root。生成器可将独立控制台的底面对齐同模式底座，轨道、地形与列车模拟坐标保持原值。模式切换后刷新 footprint；若已吸附且未被拿起，重新约束展台高度。

- `原理装配工坊/Railway teaching plinth`
- `原理装配工坊/A · 精密仪器浅台`
- `SteamJourneyCanyon.unity`：`Mesa original canyon · miniature railway V12/Flush walnut exhibition tray V22`（Attach 时加入 sandboxSupports，Detach 时移除）
- `山谷微缩铁路/Spatial driving console V20`

## 18 组装配映射

顺序以原场景 `modules[7]` 与 `finishing[11]` 为准，不合并零件组或改写其对象归属。所有组保留 `objects`、`preview`、`socket`、`previewCentre` 以及说明文字、安装/验证状态。

| 全局序号 | 数组索引 | 原组名 | 对应教学或外观 |
| --- | --- | --- | --- |
| 1 | modules[0] | 01 燃烧与进风 | 炉门、炉排、进风与火床建立 |
| 2 | modules[1] | 02 锅炉换热 | 烟管、水体、水温与蒸汽压力 |
| 3 | modules[2] | 03 烟气与抽风 | 烟气路线、烟囱和抽风 |
| 4 | modules[3] | 04 汽水分离与蒸汽输送 | 蒸汽分离、供汽管和阀门 |
| 5 | modules[4] | 05 配汽与往复 | 汽缸进排汽、活塞与往复运动 |
| 6 | modules[5] | 06 联动与传动 | 连杆、轮系和相位联动 |
| 7 | modules[6] | 07 空压机与空气制动 | 空压机、风缸、制动管和闸瓦 |
| 8 | finishing[0] | 走行平台 | 双侧走行平台 |
| 9 | finishing[1] | 双侧供水管路 | 左右供水管 |
| 10 | finishing[2] | 驾驶室 | 驾驶室主体 |
| 11 | finishing[3] | 驾驶室顶盖 | 顶盖 |
| 12 | finishing[4] | 双侧水箱总成 | 左右水箱 |
| 13 | finishing[5] | 双侧登车踏步 | 左右登车踏步 |
| 14 | finishing[6] | 煤仓 | 煤仓 |
| 15 | finishing[7] | 前缓冲梁 | 前部缓冲总成 |
| 16 | finishing[8] | 后缓冲梁 | 后部缓冲总成 |
| 17 | finishing[9] | 右前照灯 | 右侧前灯 |
| 18 | finishing[10] | 左前照灯 | 左侧前灯 |

## 原版流程与功能对应

| 原版功能 | Swan 接入方式与保留内容 |
| --- | --- |
| 拿取、吸附、安装、拆件和暂放 | `JourneySwanInputBridge` 回到原 `BeginGrabModule` / `MoveGrab` / `EndGrab` / `DetachModule`；工坊和零件一同变换 |
| 原理验证和因果链 | 原 FireLevel → WaterHeat → SteamPressure → 供汽/配汽 → 连杆运动逻辑继续驱动；缺件仍限制下游效果和观察进度；数值是教学模型，不是工业设备仿真 |
| 暂停和慢放 | 工坊新增“暂停 / 继续”“慢放 / 常速”原生操作面，分别调用原 Pause、Slow 动作 |
| 原理说明、选件、介绍开关、计数 | V18 工坊实体台与 `StudioPartCards` / `StudioSpatialConsoleV18` 保留文字和状态；0.23.2 保留原 TMP 的布局与更新，组件卡背景与引线继续跟随空间对象 |
| 实体 / 透视 / 剖面 | 同一 `Cutaway` 动作循环三态，保留实体截面、汽缸、锅炉水体、烟管和流向表现 |
| 手动 / 一键完成 | 原 18 组完成条件、剩余件依次飞入、完成声音、输入锁与恢复；原生金色扫光由原完成生命周期驱动 |
| 重置和返回保护盖 | 原 reset guard、workshop return guard 继续开合；按钮帽按压回弹、状态镜片和消息同步 |
| 出发与回工坊 | 0.23.5 完整装配后先附加载入峡谷环境，再执行原模式切换与蓝色升起扫描；等待/Unity 载入失败保留进度，返回解除环境引用并卸载，装配状态仍由原状态机持有 |
| 沙盘驾驶 | 当前 V12 峡谷、V22 平底展盘、V23 表面修补、机车和三节客车；原路线姿态和轮系联动 |
| 汽门、制动、换向、鸣笛 | 捏合滑动连续调汽门/制动/换向；换向继续要求 speed <0.03；刹车压力渐进建立，鸣笛保留声音和按钮反馈 |
| 仪表与空气制动 | 原压力、速度、圈数、制动缸及空压机逻辑、连杆/闸瓦动画继续驱动，不将“制动”改为瞬间速度归零 |
| 日月、云层与昼夜 | `MesaDayNightV13` 与原烟云生命周期保留；捏合拖日月沿原轨道改变时间，松开后恢复此前 automatic 设置 |
| 灯光与 V23.1 车窗修复 | 沙盘新增“车灯 开 / 关”操作面；机车前灯、灯束、驾驶室灯和三车共 30 扇窗保留；`StudioSpatialRefineV19` 的 WindowGlow 通过原生 Standard emission 更新 |
| 制动火花、烟汽、水反馈 | 原运动/制动/燃烧条件继续控制发射与寿命；车厢火花仍随车轮触轨位置，水冲击由原事件触发原生波纹 |
| 音频与 BGM | 原装配/按钮/拨动/汽笛/排汽/制动/燃烧资源和触发链保留；BGM 跨模式连续、首尾交叉淡化、操作 duck |
| 状态保存 | `SteamJourney.Progress.v1` 保存 stage、finishIndex、installed、verified、finished；约 10 秒和暂停/退出保存；暂放位置及驾驶位置不是持久化承诺 |

音频资源包括 `ProcessPolishV21/Audio/Assembly-01～03.mp3`、Button/Switch、`Audio/Two tone steam whistle.wav`、`Assets/Locomotive/V4/Audio/Chuff.wav` 和 `FlatTrayV22/Audio/Puzzle Masters.mp3`。主动轨道“双敲”在伙伴版已移除，不应因为包内有 `Rail joint double knock.wav` 就恢复播放。现有 BGM 是 2D 音频；没有声称完成头显空间声场设计。

输入方式的变化：使用者通过自然移动头部和变换展台观察，原 View 动作自身为空实现，不另造桌面相机切换。桌面 T/N 是调试快捷键，无需在头显复制键位；原生通过捏合拖动太阳/月亮调节昼夜，既有 automatic 状态继续保留。工坊 Pause/Slow 和沙盘 Lights 已有独立操作面，仍调用 `SwanAct`，不直接翻字段绕过状态机。

## 原生画面的适配范围

伙伴 URP 自定义 HLSL 并不能直接等价地成为 SpatialAdapter 原生材质。生成器创建独立 Standard PBR 材质与网格，原材质不改写；原模拟参数和事件仍是动态效果的来源。

| 伙伴效果 | 当前原生表示 | 差异与验收重点 |
| --- | --- | --- |
| 金属、木材、岩石等表面 | Standard 的底色、纹理、法线、金属度、粗糙度、AO、emission | 自定义各向异性、程序噪声和细节层非逐项 shader 移植 |
| 多层地面 | 生成的地面颜色贴图 | 不是实时多层地形 shader |
| 体积云、白汽、烟雾和火床 | 云为低面团状网格；烟、白汽、火床为软透明网格，随原强度、寿命和昼夜更新 | 近似体积外观；原 raymarch 自阴影、密度积分和深度融合不等价 |
| 锅炉水体、水套和水面 | 对应透明几何，随热状态变色并响应水冲击圆环 | 原屏幕空间折射、厚度吸收、动态波纹曲面为近似 |
| 前灯光束、光晕 | 透明灯束片和发光网格 | 深度衰减、镜头星芒非原 shader 精确重现 |
| 安装幽灵和轮廓 | 半透明 PBR ghost 与亮度变化 | 原程序扫描轮廓有视觉差异 |
| 蓝色到场扫描 / 金色完成 | 分网格渐进换材质、扫描环与金色反馈 | 受网格分块粒度影响，不是连续逐像素裁切 |
| 世界文字、说明卡 | 原 TMP 内容、中文字体、层级与世界 Canvas 保留，通过 SDK 的 TMP 路径上传；0.23.8 预导出完整卡片并把14个装饰 Image 路径替换为普通网格，空文字缓冲有透明占位保护 | 0.23.1 的 NativeText 副本已移除；新版字体排版、富文本和透明叠放待真机复验；桌面屏幕 Canvas 仍隐藏 |

125 条原理流动路径使用 `SwanJourneyFlowParticles` 读取原 `GetParticles` 状态，以固定 288 个原生网格槽位稀疏呈现。它不重写热力仿真或推进生命周期，每路径最多 12 个样本，共享预导出颜色/透明度材质。SDK 仅转换发射器参数，不能把原 `SetParticles` 数组直接认作原生支持；此桥保留原路径、出现顺序和传递前沿。透明排序及观感仍需真机验证。HDRP/URP 后处理、桌面景深、Bloom 和烘焙 GI 不作为保真承诺。当前生成配置取消 URP asset、关闭 Unity 阴影，使用原生支持的渲染路径；保留昼夜参数、独立反射纹理和发光，不代表桌面投影效果已复现。

`SwanJourneyTextureBudget` 为派生材质生成真实缩小的 PNG 副本。0.23.4 候选预算为列车主色最大 1024，其余法线/数据/发光/环境纹理最大 512；此前 0.23.3 使用 1024/2048 预算并出现 LOW_MEMORY，二者并不能单独证明纹理因果。法线先从 Unity GPU 编码解码为标准 RGB，不修改搭档源贴图或 importer。不能只设置 importer maxSize，因为本地 GLTF 导出器可能直接读取源 PNG。`texture-budget.json` 的字节数字是资源估算，最终 APK/GLB 体积、进程内存和实际 graphics/VRAM 须分开测量。

`SwanJourneyMeshAudit` 将 26 份 Lines 轮廓转换为细三角管，保留轮廓状态；移除零几何峡谷 MeshFilter，保留被引用的节点/renderer。镜像车轮使用与原边界一致的正缩放碰撞子代理。生成时必须通过网格/碰撞检查。Unity 在导出异常后仍可能报告 Succeeded，因此正式构建还必须满足 `totalErrors == 0`。

## 导出负担与依赖控制

`Prepare()` 只清理派生场景中明确退役且 inactive 的旧峡谷、旧控制台和桌面背景；三份同名 `Previous canyon scene · preserved` 需全部枚举。当前可切换的 sandbox 虽初始隐藏仍必须保留。停用的 LODGroup 和 inactive LOD1/2/3 副本可移除，保留全细节 LOD0；保留对象的序列化引用会保护候选。静态合批只在明确静态的当前峡谷范围执行，报告记录三角形数与边界，不合并零件、车窗、控制器或动态效果。

必须清除派生场景继承的 `Lightmapping.lightingDataAsset`、lightmaps 以及 renderer 光照贴图索引。原 `LightingData.asset` 引用完整源 `SteamJourney.unity`，SpatialAdapter 的递归 `GetDependencies` 会把它及所有旧材质再次导出；仅转换可见 renderer 并不能切断这条链。当前派生场景无 `CanyonLightmapsV11` 自动重新加载烘焙贴图。

`BuildAndroid()` 在生成后清理明确的 `SpatialAdapterExport` 生成缓存，再正式构建。SDK 会将导出目录整体打包，旧缓存不会因为本轮不再引用就自动消失。不得在仍运行的 Play-to-PICO/构建导出期间手动删除缓存，尤其不能提前删除 SDK 恢复用的 `SpatialAdapterExportBackup`。不要通过删除原模型、Resources、源场景或修改 SDK 规避依赖问题。

## 复现命令与证据

在本工程目录执行；同一 Unity 工程不要同时运行第二个编辑器进程。脚本默认使用上表 Unity 安装路径，也可通过 `-EditorPath` 指定同版本编辑器；Android SDK 来自 `../toolchain/android-sdk`，JDK 来自该 Unity 的 AndroidPlayer/OpenJDK。

```powershell
Set-Location 'D:\Projects\PICOVibe\unity\SteamAtelierSwan'

# 读取伙伴保存场景的 inventory，不执行伙伴重建。
.\Tools\Build-SwanJourney.ps1 -InspectOnly

# 生成 Swan 派生场景及材质、支撑和静态合批资源。
.\Tools\Build-SwanJourney.ps1 -SceneOnly

# 独立编辑器 CPU 流动桥检查，不进入 Play/P2D，不验证原生渲染。
.\Tools\Build-SwanJourney.ps1 -FlowChecksOnly

# 对已经生成的场景检查原 TMP 布局、空文字缓冲和更新，不进入 Play/P2D。
.\Tools\Build-SwanJourney.ps1 -UiChecksOnly

# 对两份已生成场景检查依赖隔离、两轮打开/关闭、支持面、姿态和进度保护。
# 只在 EditMode 执行，不进入 Play/P2D、不保存测试场景。
.\Tools\Build-SwanJourney.ps1 -DeferredChecksOnly

# 临时 EditMode fixture：隐藏云烟/驾驶显示/材质桥与恢复逻辑。
# 只检查真实方法与源码调用计数，不进入 Play/P2D，不测 FPS/native 性能。
.\Tools\Build-SwanJourney.ps1 -HiddenChecksOnly

# 重新生成场景、清导出缓存并运行检查；会走 SDK Play-to-PICO。
.\Tools\Build-SwanJourney.ps1 -ChecksOnly

# 重新生成、清理本项目生成缓存并构建 Android APK。
.\Tools\Build-SwanJourney.ps1
```

`-ChecksOnly` 当前要求按 SDK 支持流程连接目标 Swan，并运行官方 Play-to-PICO server。没有该连接时的连接错误应如实记录；不得为了获得绿色检查改 SDK、冒充设备连接或把桌面模式当原生运行。检查中的手势样本由确定性注入驱动，不能证明真实捏合输入命中正确。

`-FlowChecksOnly` 执行 `SwanJourney.Editor.SwanJourneyFlowChecks.Run`，在 Edit Mode 读取真实原粒子场的 `Advance` / `GetParticles` 并检查网格桥，不启动 Play/P2D、不保存测试时改变的场景。0.23.1 阶段 `flow-checks.json` 的 **22 项检查通过**：125 条路径绑定、固定 288 槽位、真实源粒子到稀疏网格的映射、每路径最多 12 样本、移动/旋转/正缩放一致性、剖面/沙盘/禁用时的显隐，以及槽位不增长。样本场产生 96 个源粒子并显示 12 个网格标记；报告明确为 `nativeRenderingValidated=false`，不覆盖全部教学闭环、真实手势或头显画面。

`-UiChecksOnly` 执行 `SwanJourney.Editor.SwanJourneyUiChecks.Run`，读取已生成的派生场景，检查原 TMP 层级与姿态、无 NativeText 副本、中文字形更新、空白/停用/重新显示时的有效网格、稳定文字不重复请求上传，以及变换后的卡片局部布局。0.23.3 还建立临时真实 `StudioPartCards` 并调用其 `Start()`，检查序列化 Standard 材质、世界 Canvas/锚点、真实所选标题与说明，以及引线材质克隆不改写源材质。此检查不自动重新生成场景、不启动 Play/P2D，执行前应先运行 `-SceneOnly`；结果写入 `ui-checks.json`，即使通过也不代表原生显示或 Android shader 保留情况已经验收。

`-DeferredChecksOnly` 执行 `SwanJourney.Editor.SwanJourneyDeferredChecks.Run`，执行前需生成当前两份场景并保存或关闭脏场景。它在 EditMode 打开主场景，再两轮附加打开/关闭峡谷，最终丢弃测试修改并恢复原编辑器场景设置。报告为 `Builds/SwanJourney/deferred-checks.json`，本轮 78 项通过；日志为 `Builds/Evidence/unity-v23-deferred-checks.log`。反射直接调用实际 loader `Awake` 绑定缓存/回调，检查不启动运行时异步协程；异步生命周期和 native 释放仍未验证。

`-HiddenChecksOnly` 执行 `SwanJourney.Editor.SwanJourneyHiddenUpdateChecks.Run`，依次汇总 `SwanJourneyCloudChecks`、`SwanJourneyDrivingDisplayChecks` 与原生桥用例。它要求 EditMode 和干净场景，创建临时 fixture、通过反射直接调用实际方法，最后清理并恢复原场景设置；不进入 Play/P2D、不保存测试场景。结果写入 `Builds/SwanJourney/hidden-update-checks.json`，日志为 `Builds/Evidence/unity-v23-hidden-checks.log`。0.23.6 本轮重跑 72 项通过；`nativeRenderingValidated=false`、`devicePerformanceMeasured=false`，测试中的 24 次隐藏调用与计数不能当作设备帧时测量。

0.23.2 的 `-SceneOnly` 已完成编译、生成和网格审计；生成器调用的 `SwanJourneyFireChecks` **15 项通过**，记录 224 三角形火舌、有效 UV、透明边缘、原 fire 引用保留但发射关闭，以及视觉材质表排除 SDF 字体材质。`fire-checks.json` 明确 `nativeRenderingValidated=false`；这一结果不覆盖原生火焰观感或 FPS。

0.23.3 的 `-SceneOnly`、网格审计和 15 项火床检查已通过；`ui-checks.json` 为 `passed=true`、124 项，包括实际 `StudioPartCards.Start()` 回归检查。该版构建 Succeeded/errors=0，08:35:47 安装成功；08:36 启动捕获没有此前 shader 异常，但 **08:36:38 LOW_MEMORY 退出**。系统截图的前摄像头遮挡提示及 UI 就绪日志都不是应用画面或持续运行通过证据。以上记录仅属于 0.23.3，后续版本须取得各自当轮结果。

| 输出 | 用途 |
| --- | --- |
| `Builds/Evidence/unity-v23-inspect.log`、`source-inventory.txt` | 源对象、脚本和资源清点 |
| `Builds/Evidence/unity-v23-build.log`、`Builds/SwanJourney/scene-report.json` | 生成器、18 组、hover、renderer 与支撑信息 |
| `Builds/SwanJourney/icon-config.json` | 0.23.7 Unity 默认及 Android 图标槽、三份独立 PNG 与 Adaptive 层配置；最终构建已完成，配置报告本身非 APK 图像验证或头显 launcher 验收 |
| `Builds/SwanJourney/icon-export-checks.json` | 0.23.7 包内 24 张多密度 PNG、机车图像、全透明 Adaptive 前景与 manifest/XML 资源映射验证通过；非头显 launcher 验收 |
| `Builds/Evidence/v237-install.log`、`v237-installed-app.json` | 0.23.7 安装返回 Success，Swan app info 确认 0.23.7/7、running=false；本轮未启动，非画面或手势验收 |
| `Builds/SwanJourney/static-batching.json` | 静态合批的范围、三角形数与边界证据 |
| `Builds/SwanJourney/scene-split.json` | 0.23.5 两场景几何边界、移除的旧 contour 引用与无序列化交叉引用 |
| `Builds/Evidence/unity-v23-deferred-checks.log`、`Builds/SwanJourney/deferred-checks.json` | 0.23.5 重跑 78 项通过、两轮实际 EditMode 场景开关、459 个独占 mesh 的依赖隔离；非运行时异步/native/内存释放验收 |
| `Builds/Evidence/unity-v23-hidden-checks.log`、`Builds/SwanJourney/hidden-update-checks.json` | 0.23.6 重跑 72 项通过；隐藏/恢复、MPB 和材质调用计数的 EditMode 用例，非 FPS/native 性能测量 |
| `Builds/Evidence/unity-v23-flow-checks.log`、`Builds/SwanJourney/flow-checks.json` | 独立编辑器 CPU 流动桥的 22 项检查；与 Play/P2D、原生验收分开 |
| `Builds/Evidence/unity-v23-ui-checks.log`、`Builds/SwanJourney/ui-checks.json` | 0.23.3 的 124 项编辑器检查通过，含 TMP 布局、空缓冲、更新及实际说明卡 `Start()`；非原生验收 |
| `Builds/SwanJourney/fire-checks.json` | 0.23.3 生成阶段 15 项火床检查通过，`nativeRenderingValidated=false` |
| `Builds/Evidence/unity-v23-checks.log`、`Builds/SwanJourney/playmode-checks.json` | 编辑器流程/注入手势结果、错误与失败项 |
| `Builds/SwanJourney/workshop-*.png`、`sandbox-*.png` | Unity 预览相机截图，不是头显截图 |
| `Builds/SwanJourney/build-result.json`、APK | Android 构建结果与交付件；不能由文件存在推断本轮成功 |
| `Builds/Evidence/v23-worn-input.log`、`v23-live-resource-errors.log` | 0.23.1 真机反馈期间的输入、HUD 姿态与持续 native mesh 错误证据 |
| `Builds/Evidence/v232-startup.log` | 0.23.2 的 `StudioPartCards.Build/Start` shader 空值异常；保留为失败证据 |
| `Builds/Evidence/v233-exit-info.log`、`v233-system.log`、`v233-startup.log`、`v233-system-memory.log` | 0.23.3 LOW_MEMORY 退出、同窗系统压力、启动局部修复与退出后快照；不可混成同刻内存归因 |
| `Builds/Evidence/session-output-20261029-084000/` | 分阶段输入副本/哈希、Memory topic、summary、路由、LMK 分类与 HTML 报告；无 trace/GC/GPU 根因证明 |

检查脚本覆盖 18 组、输入门控、预览/插槽、hover 挂载、全装配、自动装配、观察模式、热过程、三节车厢、驾驶、制动、换向和返回；`SwanJourneyGestureChecks` 覆盖输入仲裁、双击/双手等逻辑。每次交付都应绑定当前构建产物和最新日志，不沿用伙伴 Windows 的历史结论。

## 已有证据与待验证清单

| 状态 | 验收项目 | 必须取得的证据 |
| --- | --- | --- |
| 历史：已构建/安装 | 0.23.1 独立 V23 APK，标准 Gradle 重建及 `pm` 确认 | 仅属于上文 0.23.1 长度/哈希，不可作为后续版本构建、安装或验收证据 |
| 未通过 | 0.23.1 用户真机验收 | 文字/面板错位、FPS 方框/空间固定、单手移动入口找不到；缩放可用是有限反馈 |
| 未通过 | 0.23.2 运行时说明卡初始化 | `v232-startup.log` 的 `shader` 空值异常中断实际 `StudioPartCards.Start`；不是原生成功验收 |
| 历史：已通过（编辑器） | 0.23.2 编译、场景生成、网格审计与火床检查 | `-SceneOnly` 完成；`fire-checks.json` 15 项通过、`nativeRenderingValidated=false`；未覆盖后续暴露的设备启动异常 |
| 已通过（编辑器） | 0.23.3 派生场景生成、网格审计与火床检查 | 本轮 `-SceneOnly`、mesh audit、fire 15 项通过；不能代替 APK 构建和原生运行 |
| 已通过 | 0.23.3 构建、签名和安装 | Succeeded/errors=0；1,572,448,046 bytes；SHA-256 C566BAAA657655318442F0D049B1006291FC5D88DBDEA0FDF4D116DFB8278A2E；Swan pm 确认 0.23.3/3，旧两应用更新时间不变 |
| 未通过 | 0.23.2 / 0.23.3 持续运行 | 系统记录两版 LOW_MEMORY；0.23.3 前台 PID 20085 约 36.4 秒后退出，RSS 2.4GB；分配来源未确定 |
| pending | 0.23.4 内存缓解候选 | 构建/签名/安装已确认；仍需持续存活及相同流程内存/退出记录，资源估算降低或短时存活不等于修复通过 |
| 已通过（编辑器） | 0.23.5 场景生成与延后载入检查 | 本轮 SceneOnly 通过；DeferredChecks 重跑 78 项 PASS、两轮实际场景打开/关闭、459 个峡谷独占 mesh 不进入 startup dependencies |
| 已通过（编辑器与产物） | 0.23.5 UiChecks / APK | UiChecks 124 项；Android Succeeded/errors=0；v2 签名通过；aapt 核实 0.23.5/5、独立包名及 SpatialAdapter 入口；本轮未安装 |
| pending | 0.23.5 原生载入与释放 | 完整装配出发后的可见画面/时延，失败重试与忙状态，返回后 native 缓冲释放，重复出发/暂停恢复的内存和退出记录；编辑器 78 项不覆盖这些项目 |
| 历史：已通过（编辑器与产物） | 0.23.6 隐藏更新回归与 APK | 当轮 HiddenChecksOnly 重跑 72 项通过，稳定隐藏 Material writes/MPB reads 和稳定可见未变 Material writes 均为 0；FlowChecks 22 项通过，APK 构建/签名/包内检查通过；使用上文 0.23.6 哈希，该版未安装；0.23.7 未重复这些游戏检查 |
| pending | 0.23.6 原生恢复与实际性能 | 云烟恢复、机车灯泡熄灭、30 扇窗昼夜、仪表读数和扫描/金色结束后的真机画面；同流程帧时/内存测量，源码调用减少不是性能通过结论 |
| 已通过（构建、签名与包内产物） | 0.23.7 最终应用图标配置与 APK | 三份 PNG 已独立绑定；Succeeded/errors=0，1,138,757,816 bytes，SHA-256 7A5AC446044477B119EBF520C109123E32DA758B83CE8D81F7103DDBB2DC2F68；aapt 确认同包名 0.23.7/7，两份 GLB 检查通过且几何数量与 0.23.6 一致；v2 签名、24 张图标 PNG、透明前景及 manifest/XML 映射验证通过，归档哈希一致 |
| 已通过（安装） | 0.23.7 Swan 安装与版本确认 | 安装返回 Success/exit 0；app info 确认同包名 0.23.7/7、running=false；旧两应用仍保留，本轮未启动 |
| pending | 0.23.7 launcher 显示 | 已安装供用户回家查看；设备应用列表中的遮罩、尺寸与实际图标显示尚未验收 |
| 历史：22 项通过 | 0.23.1 阶段编辑器 CPU 粒子与网格流动桥 | `flow-checks.json`，`nativeRenderingValidated=false`；非完整原生检查结果 |
| 124 项通过（编辑器） | 0.23.3 TMP 与实际说明卡启动检查 | 本轮 `ui-checks.json` 与 `unity-v23-ui-checks.log`，含实际 `Start()` 回归；仍须与真机显示验收分开 |
| pending | 原生启动画面、冷启动、休眠/恢复与多次重启 | 设备型号/系统版本、启动日志和设备画面 |
| 已通过（产物） | 0.23.5 原生场景导出隔离 | APK 仅有 SteamJourneySwan / SteamJourneyCanyon 两个 scene GLB；主 GLB 无 canyon root，完整原源场景未导出；不证明设备加载顺序 |
| pending | 高光与真实捏合命中 | 实机逐件/逐控件操作，不将挂载组件等同眼动验收 |
| pending | 拿取、暂放、吸附、拆除、双击归位、一键拆开 | 18 组与顺序限制、取消、快速交替操作录像 |
| pending | 双手移动/旋转/缩放及模式切换 | 边界缩放、丢手/恢复、取消、不误装件、FPS 不随展台变化 |
| pending | 桌面权限、吸附、边缘支撑、拿起脱离 | 真实桌面/空间网格，权限允许与拒绝、工坊/沙盘两模式 |
| pending | 完整教学与驾驶闭环 | 验证、透视/剖面、完成扫描、驾驶、制动/换向、返回与存档 |
| pending | 原生文字、材质与透明效果 | 双眼/近远观察、夜间 30 扇窗、烟云/水/光束/粒子和扫描；与视频逐项对照 |
| pending | 0.23.8 反馈修复整体、说明卡、空中棕块与软粒子复验 | 无 shader 空值异常且持续存活；说明卡世界锚定/标题/正文/引线正常，原 TMP 对齐和背景保留；新日志无持续零缓冲错误，软火舌与燃烧时序正确 |
| pending | 音效与 BGM | 装配、按钮、汽笛、排汽、制动、跨模式持续与混音 |
| pending | APP FPS 与稳定性 | 设备上的 Unity 更新率、头部跟随、帧时/内存/启动时间；不得称为合成器 FPS |
| pending | 当前版本单手移动把手 | 工坊/沙盘前左侧均可见、可高光与捏住移动，经过桌面约束且不误触其他控件 |
| pending | 新增辅助操作面与日月操作 | 工坊 Pause/Slow、沙盘 Lights、日月拖动及松开后 automatic 状态恢复 |

编辑器日志里的 APP FPS、PC 预览图、APK 构建成功，以及伙伴交接中的 V23/V23.1 历史记录，都不能替代上表的真机证据。

### 0.23.4 启动观察（未戴机验收）

0.23.4/4 APK 构建 Succeeded/errors=0，长度 1,153,484,126 bytes，SHA-256 `E698FF3FCF192459B47DE8CC31BE94900753D9DE74961B56A9E2915699F5B350`，签名通过；08:47:06 安装后 PID 22854 在至少两分钟启动观察中保持存活，退出历史没有新增该 PID 的 LOW_MEMORY。12 份 meminfo 快照后段 PSS 约 3,824,582 KB、RSS 3,866,519 KB；保留了较高的资源占用，不能据此宣称性能问题彻底解决。

观察结束时设备为 Asleep，截图为黑屏；未取得实际佩戴画面和交互验收。日志 world=97/invalidMeshes=0，捕获片段未出现此前的说明卡 shader 异常或 Empty Indices/Need Positions；APP 更新率约 42–44，只属于静止姿态采样，不是屏幕/合成器 FPS 或游玩性能。仍有 SDK ControllerTrackingProvider 启动失败等提示，尚未证明与用户问题的关系。0.23.4 历史证据归档在 `Builds/SwanJourney/Releases/0.23.4/release-validation.json` 和 `Builds/Evidence/v234-*`；`Docs/V23-Swan-Release.json` 以其中实际版本与哈希为准，不能混用不同版本证据。



## 0.23.12 · 2026-10-30 夜间性能与加载迭代

已构建、包内验证并安装；Swan 休眠，未做佩戴验收。用户确认上一版0.23.11在静止和双手操作时约27–28 FPS，说明持续1 FPS问题缓解，不能当成本版达到目标的证据。完整清单见 V23-Swan-Release.json。

- 刚性同父级装饰1407个Renderer合为105个，保留3668257个主场景三角形。被脚本引用的组件、动画父级、原18组装配与控制器不合并。合并命中框的实际精度待验。主GLB增加约31MB，是减少提交次数的取舍。
- 静止运动部件不再重复设置相同局部姿态。Swan专属Start将目标帧率设为90；目标值并非实测值。
- 峡谷的1155个隐藏旧网格无运行控制/外部引用，剥离后GLB约281MB降至99MB，保留约53.3万三角；没有以降低当前可见地形精度换取该减重。
- 增加11节点轻量启动场景，唯一Stage和XR初始化归它所有。工坊后载入；峡谷仍在完成装配点击出发时载入。两处均展示真实Unity资源进度，然后单列“准备空间显示”；无原生进度API，不显示假100%。用户视野下的加载文字和时长仍待验。
- 峡谷独立根显式注册、稳态减少Transform写入，并增加相对UID诊断。尺寸候选修正尚无真机证明；不改SDK或手工补UID/缩放常数。
- 9组回归、51项性能/加载、80项场景边界、263项包内检查通过。最终APK：790500485字节，SHA256 1DEC84823D51F2F8D17A7FCBAE8564D7DE41125D46EFBB0E0A3C5857C0EEC877。两个旧包仍在，更新未清数据。

下一次最小佩戴验收：冷启动观察加载提示与模型；静止及双手操作各约10秒记录FPS；出发查看山谷尺寸和实际切换时间。随后检查合并后的零件高光、双击归位与桌面吸附。未完成这些检查前，不宣称90FPS、启动偶发故障或山谷尺寸已经解决。


## 0.23.16 · 2026-10-30 山谷真机验收

用户确认完整山谷、轨道和列车可整体移动并统一缩放；按需 Resources 预制体方案通过这项真机验收。保留该加载与共同父级架构。用户同时报告帧率仍低，性能仍未通过；返回重进的原生资源释放及冷启动稳定性也未据此验证。

## 0.23.13 · 2026-10-30 反馈修正

0.23.12 实测：工坊静止约37–40 FPS，双手约20 FPS；加载字竖排过大、山谷尺度仍异常、按钮重叠和铁轨不可见。因此不能把12版作为视觉或性能验收通过。

13版修正加载字形为一行并检查实际米制bounds；工具按钮移到仪器前方独立一排，搬台把手再置于最前方并随stage自然缩放。单手搬台按头部距离平滑采用1–3倍位移，缓慢朝向玩家，桌面粘滞仍用真实手部位移判定。

山谷按原设计 .035比例、微缩位置和active根导出，不再以原始大尺寸/隐藏根等待首次同步；单独场景中的展盘碰撞体纳入双手目标。派生场景清除静态标记，保留原始伙伴资源和全部工坊几何。

Unity实验：改变主stage缩放会标脏9141/9177节点，峡谷3974/3974。新增应用侧TransformSyncBudget：在应用动画之后、适配器之前，只有局部TRS/父级/激活状态均未改变且已预热的子节点才清除冗余hasChanged；真实动画、重挂接、首次和激活更新仍同步。不写SDK内部UID或改SDK。

9组回归、63项性能/加载检查、263项APK检查、3场景分包与v2签名通过。13版已安装；新帧率、铁轨显示和山谷实际尺度须以真机证据为准。详见 Docs/V23-Swan-Release.json。


2026-10-31 0.23.20复测：753个纯双手帧的过滤均值 2.690 ms（19版单窗口3.793 ms），Late均值 28.829 ms。用户静止约41 FPS、双手约30 FPS，无明显总体提升。保留有效微优化，但下一轮同时处理工坊和山谷的共用场景负担；夜间无佩戴测试，不宣称90FPS。


## 2026-10-31 夜间工坊与山谷共享预算迭代

0.23.20 的真实反馈仍为静止约41、双手约30 FPS；过滤均值下降至2.690 ms，并未改善总帧率。0.23.19工坊两个稳态窗口的Late记录为30.671/30.418 ms、工程已测回调合计3.187/2.268 ms。工坊和山谷都需要处理共同负担，未测余量不能直接认定为SDK独占耗时。

1. 清点保留场景发现1416个只有Transform和MeshCollider的旧桌面命中叶，最大网格426294三角、没有可见Renderer。新生成器只移除无子对象、无序列化引用、无触发/物理材质/其他行为，且同一LocomotivePart有当前可见原生命中面与hover的旧叶。实际删除1134个、1514522碰撞三角；282个不满足条件继续保留。可见网格、材质和零件状态不删除。
2. 刚性合批只穿过无引用的纯Transform祖先，到脚本、引用、激活或反射边界停止。机械联动根、独立零件、预览、动态材质与控制器引用保护。较20额外减少77个Renderer；全部合批逐组校验三角数量和变换后的包围盒。
3. 0.23.21 Development诊断包通过9组回归、110项性能保护和263项包内检查，单独归档，未安装。主GLB节点7680→6469，meshNodes3155→3078，三角总数3668257保持；山谷仍333节点、532970三角。21 APK为758921598字节。包内Unity数据减少133282769字节，原生导出增加8841500字节（合批减少实例复用），不等于真实内存或FPS收益。
4. 0.23.22候选继续同一视觉/交互内容，默认使用非Development和IL2CPP Master/OptimizeSpeed；Build-SwanJourney.ps1 -DevelopmentBuild仍可生成可调试包。此前已是Release编译器+OptimizeSpeed，并非没有优化；本轮进一步关闭调试版引擎与提升编译优化。低频计时日志和FPS保留，Release缺失的ProfilerRecorder数据标为unavailable。

用户已经休息，本轮不重新做需要佩戴的采样。工坊/山谷实际FPS、命中体验与冷启动耗时留待真机复测；不能将这些静态计数或编译通过等同于90FPS达标。


0.23.22最终交付：9组回归、110项性能保护、263项包内检查及v2签名通过。三个GLB与21逐字节相同。APK739039156字节（比20减少46168363字节，约44MiB），SHA256 `b5ecb6b8b2952071bfabdde127ddbfa9e75574e485c36320a4c4b6de7ee1dbda`。已安装Success、启动进程PID8224，存档未清理、两个旧版本仍在。设备检查为Asleep/供电/电量100%，暂无Unity场景初始化日志，不能确认佩戴加载和性能。原APK和代码快照在Releases/0.23.20、0.23.21、0.23.22；改动前备份在Builds/Backups/0.23.20-before-shared-scene-budget/source.zip。当前使用本地原签名以保留升级和存档，未改为商店发布签名。
