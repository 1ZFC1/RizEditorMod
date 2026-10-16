# 项目状态

最后更新：2026-09-23

## 当前阶段

长期迭代中。阶段 0 至阶段 6 的核心目标已完成。当前通过可禁用的 BepInEx 插件增强编辑体验；原始 EXE、`GameAssembly.dll` 和 Unity 资源未被修改。插件版本 **0.3.0**，已按 `docs/DISTRIBUTION.md` 导出正式包、在干净副本完成管理器全流程验证、更新 `RhythmEditor_modded`，并由使用者完成管理器界面与编辑器运行验收。

使用者已确认当前面板分工、`Ctrl+D` 删除行为与颜色编辑剪贴板可用。**发布统计**在 0.3.0 中因工具识别假设错误（假定发布按钮有 `toolObject`）而从未显示，并会把「发布」误判成「光标」；2026-09-23 按按钮结构改为「无 `toolObject` 且有 `Body` 子物体 = 发布」后修复，已由使用者在 working 副本验收（D-021）。同日完成「插件」工具栏入口的机制验证（D-022）。

**本轮已完成并验收**（2026-09-24/10-15，均已装入 `RhythmEditor_working`，自动测试 **51/51**）：

0. **公开仓库已建立**（2026-10-16）：GitHub 仓库 <https://github.com/1ZFC1/RizEditorMod>（Public，MIT 覆盖插件自身源码）。发布采用**独立工作区**方案：`RZEMOD\RizEditorMod\` 是公开镜像（自己的 git 历史），由 `tools/Publish-GitHub.ps1` 从开发仓库的**跟踪文件**同步（排除私人笔记、规范化本地绝对路径、保留手写的 README/LICENSE），`RhythmEditor_working` / `modded` / `original` 均不受影响。首次发布：2 个提交、103 文件、375 KiB，含 `README.md`、`LICENSE`(MIT)、`docs/GITHUB.md`（维护教程）与 `tools/Publish-GitHub.ps1`；标签 `v0.4.0`。仓库**不含**游戏本体、`GameAssembly.dll`、`BepInEx/`（interop/metadata）、`#Samples/`、音频与发布 ZIP。
0. **发布 0.4.0**（2026-10-16，已完成自动化验证与人工验收）：`Plugin.Version` = `0.4.0`；包 `artifacts/releases/RhythmEditorMod-0.4.0-win-x64.zip`，**ZIP SHA-256 `5E460EE6D164FE2D9F14B2BC61704C21DC78B2DE55B5DC869023B84675015C56`**，payload 387 文件 / 139,509,396 字节；插件测试 51/51，插件+管理器+原生启动器 0 警告 0 错误；干净副本（由 `original` 新复制、58/58 一致）依次通过安装/重复安装/禁用启用/仅卸载重装/完整移除重装，且 58 文件始终与 baseline 一致；`RhythmEditor_modded` 已由 0.3.0 升到 0.4.0（备份 `20261016-004012`），安装后 DLL 与构建产物逐字节一致；使用者确认可用。细节见 `DISTRIBUTION.md`。提交 `0e3bf00`。
0. **修复「项目」工具提示文本串台 + 日志规范化**（D-032）：项目 body 的状态行与标题改为**自持**（每帧重设），项目消息直连自身、不再与公共状态出口互通 → 在画布流速里设定缓动不会再改写项目面板文字；日志改为「信息尽量全、只在动作/状态变化时输出」，并修掉上一版每帧重写标题造成的刷屏（一轮会话 1052 行中 718 行），补全缩放/项目 body/游玩区域边框/预览蒙版/跨画布剪贴板等日志上下文。已由使用者验收（「没什么问题」）；更细的字段取舍记为低优先（`BACKLOG.md`）。
0. **整体缩放改为 40%–200%、每格 10 个百分点**（D-031）：`Ctrl+Shift+滚轮` 现在可**放大**（原先被 `scale ≥ 0.999` 挡住只能缩小），步进由「乘 1.12」改为**加 10 个百分点**（17 档），按 10% 栅格取整不漂移；放大/缩小共用「突破缩放极限」门闩，关掉复位 100%。纯逻辑在 `Core/ZoomStepping.cs` 并有自动测试，插件测试数 48 → **51**。已由使用者验收。
0. **跨画布线的复制与粘贴**（D-030）：线编辑里 `Ctrl+C` 后**按住 Shift 拖框**即可整条选中跨画布线（命中只看当前画布上被完全框住的节点），拖动中**实时高亮**；复制进插件剪贴板（逐节点保留 `canvasIndex`），`Ctrl+V` 按鼠标对齐并**整体偏移节点画布**（Hold 结束画布同步、越界拒绝），`Ctrl+Shift+V` 以鼠标为对称轴镜像；写入包在 `Stepping` 内（单步 Undo），刷新改为 0.5 秒后只读复核。x 吸附 = 实测基础格距 ÷ 水平细分，换算用**实测渲染映射**（斜率 5.625；旧常数 5.5625 偏 1.1% 已弃用）。剪贴板归属 = 最后复制者拥有；不按 Shift 时对原版零干预。细节与失败记录见 `docs/features/CROSS_CANVAS_LINES.md`。
0. **游玩区域边框**（D-029）：`预览蒙版：关` 且带预览播放时（或整体缩放 ≠ 100% 时）用 **2px** 边框标出原本未被遮住的游玩区域。几何全部取**世界固定值**（左右 = 侧板外侧边 ∓2.813、下 = `BottomSideForward` 上沿 −2.9、上 = `TopSide` 外侧边 +4.1）再经相机投影 → 不随播放移动、随整体缩放与窗口变化；颜色 = `themes[0/1].colorsList[2]`（**0–255 ÷255**，随 Riztime 变色）；线宽恒定 2px。**硬约束**：`GUI.DrawTexture` 在本构建被 IL2CPP 裁剪（`Method unstripping failed`），故用自建 1×1 纹理 + 纯色 `GUIStyle` 配 `GUI.Box`，失败自动回退默认 `GUI.Box`。细节与三次候选迭代见 `docs/features/PLAY_AREA_FRAME.md`。
0.2. **缩放改为等比（整体缩放）**（D-028）：`Ctrl+Shift+滚轮` 横竖同比例缩放（原实现只拉伸水平），范围仍 40%–100%；**删除 D-017 的 editorMask 自动隐藏**（实测无用，预览时原版自己会关）；读数改「缩放 N%」、面板提示改「Ctrl+Shift+滚轮整体缩放，Ctrl+M 复位。」。已知仍存在：缩放后 UI 点击命中错位（自定义投影须把 UI 画布搬 Overlay），属另立任务。
0.5. **三个 bug 修复**（使用者逐项验收）：
   - **流速同拍重复不再阻断操作**（D-025）：校验改为「不得倒序、同拍允许」，复制/粘贴按「后一项优先」折叠同拍项，并堵住两处重复来源（缓动插入采样的相邻 float 塌缩、粘贴覆盖容差 `1e-6` 与插入容差 `1e-5` 不一致）；已有重复只报告不自动改写。相关用例 4 条。
   - **「编辑线」`Ctrl+D` 不再丢颜色**（D-026）：线剪贴板补齐 `judgeRingColor`/`lineColor` 两个数组（`Line` 的全部四个字段），文本格式同步扩展且向后兼容。相关用例 1 条。
   - **数值框里输入 `9` 不再跳到「项目」**：`9` 由插件处理，现按项目既有做法统一判断 `ProjectInformation.isInput`，输入框聚焦时忽略并记日志。

1. **额外缓动曲线（D-023）**：`GetEase` 的缓动表固定 19 条、`easeType ≥ 19` 会抛异常，因此新曲线只能**烘焙**成 `easeType = 0` 的普通节点。**编辑画布**与**编辑摄像机·横向移动**的缓动下拉框末尾追加三条标准曲线 `In Elastic` / `Out Elastic` / `InOut Elastic`，节点按曲线弯曲程度**自适应**（四分点弦误差细分，默认容差 0.01），两者共用拟合核心。画布走已验证的 `OnEaseTypeChange` 拦截；摄像机因接线不同，改在 `Dropdown.set_value` 处事件式拦截（拟合后把值换成 Linear 再交回原版）。**摄像机缩放与编辑线经使用者决定取消**：缩放曾在选择后触发 `IndexOutOfRangeException`（`19×101` 表越界），现只提示不支持、不写任何字段；编辑线因「拖动创建线、`Ctrl`+拖动改属性」的交互与下拉框驱动割裂而整体移除。另外新增 `EaseTypeGuard`：任何越界 `easeType` 都按 Linear 求值并告警，编辑器不再因坏数据崩溃。
2. **编辑器内切换工程（D-024，「项目」工具）**：记住最近三个工程（`BepInEx/config/RhythmEditorMod/projects.json`），点击即切换；「选择文件…」调用原版两步对话框；切换前自动保存（`notSaved` 时走原版 `FileControl.Save`，无路径则拒绝），加载复用原版 `OpenLastProject()`，完成后清理上一次遗留的 `%TEMP%\tmp*.tmp` 音乐缓存。
3. **「项目」工具的 UI 改为原生 body**：工具栏入口与 body 标题均为「项目」，面板由**克隆的原生控件**搭建（容器克隆 `Edit Canvas/Body`，行控件克隆其 `DeleteCanvas`），布局参数放在 `BepInEx/config/RhythmEditorMod/layout.json`，可手改后重启生效。**显隐规则是「选中即显示」，不跟随鼠标**——悬停淡出与交给原版托管两条路线都已撤回（原因见 `docs/features/PROJECT_SWITCH.md` 5.2）。

详见 `docs/features/EXTRA_EASING.md`、`docs/features/PROJECT_SWITCH.md`、`docs/features/PREVIEW_MASK.md` 与 `docs/features/PLAY_AREA_FRAME.md`。最终干净构建装入 `RhythmEditor_working`：`RhythmEditorMod.dll` `F0C4705D…` / `RhythmEditorMod.Core.dll` `3763D0DB…`（DLL 内已无任何探针类），自动测试 51/51。调查用的临时探针均已改名为 `.cs.disabled`（脱离编译范围、不进 Git）：`ToolbarProbe`、`EaseProbe`、`EaseFlowProbe`、`NativeUiProbe`、`BodyUiProbe`、`ToolbarBehaviorProbe`、`ProjectProbe`、`ProjectSwitchProbe`、`PreviewMaskProbe`、`CrossCanvasLineProbe`。

## 最近验证与 checkpoint（2026-09-22）

已把长期累积在 working 工作区的 9 月改动固化为 Git checkpoint。此前最后提交为 `006b11e`，其后的 18 个改动文件加若干新增文件一直没有回滚点，现已分为 4 个提交：

| 提交 | 内容 |
| --- | --- |
| `7f072ff` | `chore: ignore local runtime, build outputs and release artifacts` |
| `47ed392` | `feat: add the September editor-enhancement iteration`（插件源码 + 测试） |
| `30ecfb5` | `docs: record the September feature round` |
| `ea5815d` | `tools: add metadata scan scripts, type probe and segment order fix` |

同一轮实际执行的验证：

- `dotnet run --project tests/RhythmEditorMod.Core.Tests -c Release`：**25/25 通过**（2026-09-23 再次实际运行；计数随颜色剪贴板两项用例增加）。
- 重跑 `tools/Decrypt-RizlineMetadata.py`：解密明文与重建元数据均与既有产物逐字节一致，`C358B6FA…` 可复现；重建产物的类型、字段和方法 token 与 `docs/CODEMAP.md` 一致。详见 `research/STAGE5_IL2CPP_METADATA.md`。
- 修正 `BODY_PAIR_ORDER` 第 7、12 号区段顺序。旧顺序把 `fieldDefaultValues` 与 `genericParameters` 放反，`fieldIndex` 可达 `1572864`（字段总数仅 40185）；修正后 `fieldIndex` 落在 `53`–`40184`，且 11654/13251 条相邻记录呈"同 `typeIndex`、`fieldIndex` 递增 1"的结构化形态。旧产物保留为 `research/generated/verify-standard.dat`。
- 复查 `RhythmEditor_working` 与 `RhythmEditor_modded` 的 58 个原程序文件，SHA-256 全部与 `RhythmEditor_original` 一致。

此后另有 `60ea2f9`（复现并校验元数据重建）、`16c6586`（搬迁后新路径运行验证）、`4d48687`（基础设置面板移到光标工具，D-018）、`baa9e55`（颜色编辑剪贴板重做，D-019 / D-020）、`2a550e2`（发布统计工具识别修复，D-021）、`60ee9b3`（插件自己的工具栏入口，D-022）、`ea94e55` / `2e0fc72`（画布弹性缓动、编辑器内切换工程、弹性缓动扩展到摄像机横向移动；D-023 / D-024）、`71eba47` / `1b31092` / `e3faab6`（三项 bug 修复：输入框内的 `9` 快捷键、流速同拍重复、线剪贴板丢颜色；D-025 / D-026）、`5e44b08` / `87708ff` / `6cc881c`（预览蒙版开关、白名单化并补齐底部两块、修复子物体不复位；D-027）以及 `3d1a370` / `17734f6`（等比整体缩放 + 游玩区域边框；D-028 / D-029）、跨画布线复制粘贴（D-030）与整体缩放新范围（`57ad643` / `04b28ac`；D-031）；功能明细见「已完成（近期迭代）」。

## 搬迁后新路径运行验证（2026-09-22）

工作区从 `D:\RZEMOD` 搬到 `RZEMOD` 后，已在新路径完成一次完整运行，确认搬迁不影响 BepInEx 与插件。

证据来自编辑器自己写出的 `BepInEx/LogOutput.log`（写入时间 `2026-09-22 16:43:41`，共 294 行）：

```text
[Debug  :   BepInEx] Game executable path: RhythmEditor_working\Rizline Editor.exe
[Debug  :   BepInEx] Interop assembly directory: RhythmEditor_working\BepInEx\interop
[Debug  :   BepInEx] BepInEx root path: RhythmEditor_working\BepInEx
[Debug  :InteropManager] Preloaded 86 interop assemblies in 152ms
[Info   :   BepInEx] 1 plugin to load
[Info   :   BepInEx] Loading [Rhythm Editor Canvas Editing 0.2.0]
[Info   :Rhythm Editor Canvas Editing] 音符流速下限补丁已就绪。打开「突破缩放极限」后才会放宽到 -0.5。
[Info   :Il2CppInterop] Registered mono type RhythmEditorMod.SpeedEasePanel in il2cpp domain
[Info   :Rhythm Editor Canvas Editing] 流速缓动面板初始化完成。
[Info   :Rhythm Editor Canvas Editing] Loaded. Press F8 to show or hide the canvas-editing panel.
[Message:   BepInEx] Chainloader startup complete
```

- 全部 294 行里只有 2 行非 Info：`Class::Init signatures have been exhausted, using a substitute!`（Il2CppInterop 内部回退）和 `Assembly RhythmEditorMod.dll is not registered in il2cpp`（普通 mono 插件程序集的常规提示，紧随其后即成功注册 mono 类型）。两者在搬迁前的日志中同样存在，**没有 Harmony 补丁失败、异常或崩溃**；`ErrorLog.log` 只含两行 Steam 初始化文本。
- 日志中还有约 40 行真实功能操作记录（`复制判定环` / `剪切判定环` / `粘贴判定环`，含"已复制线 1 的 1 个判定环事件""已向线 2 粘贴 1 个判定环事件"等），说明插件在新路径不只是加载成功，而是被实际使用并通过。
- 当时加载的插件文件 `BepInEx/plugins/RhythmEditorMod/RhythmEditorMod.dll` 修改时间为 `2026-09-22 01:42:09`，SHA-256 与 `src/RhythmEditorMod/bin/Release/net6.0/` 的构建产物一致，即**已提交的源码版本**。
- 由于每次启动都会覆盖 `LogOutput.log`，本次会话日志已另存为 `baseline/runtime-artifacts/2026-09-22-relocation-verify/LogOutput.newpath.log`，`ErrorLog.newpath.log` 同目录保存。搬迁前指向 `D:\RZEMOD` 的那份日志**已被本次运行覆盖、无法找回**；更早指向 `D:\WorkShop\RZEMOD` 的 `BepInEx/LogOutput.1.log` 仍在，并已复制为该目录下的 `LogOutput.oldpath-D-WorkShop-RZEMOD.log`。

### 该次运行在工作区外的写入

编辑器会按 `PlayerPrefs` 的 `lastFilePath` 自动打开上次的谱面，该路径在工作区之外：

```text
D:\steam\steamapps\common\Rizline\Editor\谱面\BATTLE NO1\Chart_NO1.json
```

该次运行产生的实际写入（均为原编辑器正常行为，非插件引入）：

| 位置 | 变化 |
| --- | --- |
| 上述谱面目录下的 `Rizline Editor Auto-Save\` | 新增/更新 4 个自动保存文件，时间 `16:31:05`、`16:38:40`、`16:38:56`、`16:39:40` |
| `Chart_NO1.json` 本体 | **未修改**（仍为 `2026-09-18 10:47:49`） |
| `LocalLow\PigeonGames\Rizline Editor\` | 未写入（`Player.log` 仍为 `2026-04-24`） |
| `HKCU\Software\PigeonGames\Rizline Editor` | 会话计数与窗口状态更新，属正常 |

结论：自动打开上次谱面时，自动保存会写入该谱面所在目录。今后在做隔离实验前，必须先清空或改写 `lastFilePath`，否则会把自动保存写到游戏安装目录。

## 正在进行

- 无阻塞中的实现任务。下一轮从 `docs/BACKLOG.md` / `docs/待实现功能.txt` 选取边界明确的单项。

## 当前插件能力摘要

| 区域 | 内容 |
| --- | --- |
| 音符编辑 | 无插件面板 |
| 光标 | 基础设置：「突破缩放极限」、预览细线、面板宽度/透明度滑条 |
| 发布 | 仅谱面统计（画布/线/Tap/Hold/Drag） |
| 画布流速 | 缓动采样；框选复制/剪切/`Ctrl+D` 删除；系统剪切板可选导出 |
| 画布/摄像机 | 框选剪贴板与 `Ctrl+D` |
| 颜色编辑 | 框选按大地 x=0 左右路由（左=线色覆盖／右=判定环／跨过=两者）；粘贴跟随上次复制并覆盖同拍；`Ctrl+D` |
| 画线 | 一键入调色板；原版复制剪切；可选系统导出；`Ctrl+D` 删线不进剪贴板 |
| 主题 | 一键入调色板 |
| 项目 | 面板正式用途：**编辑期内切换工程**。显示最近三个工程（谱面+音乐），点击即切换；另有「选择文件…」走原版两步对话框。切换前自动保存，未保存过的新工程拒绝切换（D-024） |
| 弹性缓动（拟合） | **画布**与**摄像机·横向移动**的缓动下拉框末尾追加 `In Elastic` / `Out Elastic` / `InOut Elastic`：把弹性曲线烘焙成 `easeType = 0` 的普通节点，位置按曲线自适应；不可逆（一次 `Ctrl+Z` 可整体撤销）。**摄像机缩放与编辑线不支持**（原因见 D-023） |

横向缩放：突破极限开启后 `Ctrl+Shift+滚轮` 40%–100%；`Ctrl+M` 复位；小于 100% 时自动隐藏 `editorMask`。纯数据测试 **25/25**。

## 已完成（近期迭代）

- 插件获得自己的工具栏入口（D-022）：运行时克隆「光标」按钮追加到工具栏末尾，标题「插件」，面板按对象身份路由；本次是「自定义工具栏工具」的机制验证，面板内容为占位。过程中确认两条重要事实：工具栏按钮没有 `UnityEngine.UI.Button`（鼠标点选由 `ToolbarToolButton.Update()` 处理，克隆体天然继承），且原工具栏的快捷键映射覆盖不到晚创建的克隆体（按 `9` 改由插件自己选择）。另发现 `GetComponentInChildren<T>(bool)` 在本构建必抛 `VerificationException`，组件查找改为「遍历子树 + `GetComponents<Component>()` + `TryCast<T>()`」，发布那条 `PublishControl` 附加确认随之从「永远为空」变为可用（日志显示 `PublishControl=Control`）。已由使用者在 working 副本验收（标题、鼠标点击、快捷键 `9`、与其它工具互斥）；最终干净构建为 `RhythmEditorMod.dll` `0C5821A8…`（临时探针未参与编译）。
- 颜色编辑剪贴板重做（D-019、D-020）：框选横向按大地 x=0 路由到线色覆盖／判定环／两者；`Ctrl+V` 与 `Ctrl+Shift+V` 锚点不变但目标数组跟随上次复制；粘贴覆盖同拍并把同拍重复塌缩成一条；粘贴块尾部 `endColor` 重定向，不再带进选区之外的过渡。修复两个缺陷：颜色写入被钳成纯白（`Color32` vs `Color`）、`editingLineColor` 读取抛 `NullReferenceException` 导致复制整体失败。已由使用者在 working 副本验收。
- 把「突破缩放极限」「预览细线」及面板宽度/透明度滑条从**音符编辑**移到**光标**的「基础设置」面板（D-018，取代 D-015 的前两条）；面板内容与行为不变，音符编辑不再显示插件面板。已由使用者在 working 副本验收（`RhythmEditorMod.dll` `547C0CF1…`）。光标识别按「工具按钮没有 `toolObject`」判定，并在每次切换工具时记录按钮名与 toolObject 名。
- 修复发布统计永不显示的缺陷（D-021）：工具栏探针证实**发布按钮的 `toolObject` 也是 `null`**，原先「发布只认 `toolObject` 上的 `PublishControl`」的判定恒为假，导致选「发布」时既不显示统计、又被误判成光标弹出「基础设置」。现按按钮结构区分：`toolObject == null` 且有 `Body` 子物体 = 发布，没有 `Body` = 光标；发布另以该按钮子树内的 `PublishControl` 作附加确认。工具切换日志扩展为按钮名、toolObject、`Body`、`PublishControl` 与最终面板。自动测试 25/25，已装入 `RhythmEditor_working`（`RhythmEditorMod.dll` `D2E44F45…`，2026-09-23）；已由使用者在 working 副本验收，`RhythmEditor_modded` 仍为 0.3.0、不含此修复。验收后把临时探针改名为 `ToolbarProbe.cs.disabled`（脱离编译范围）又装了一次，工具路由代码未变；连续两次构建哈希一致。
- 面板按工具切换：光标承载「基础设置」（缩放相关控件与面板外观）；音符编辑不显示插件面板；发布仅统计。识别不再依赖 `toolObject` 判发布（发布的 `toolObject` 为 `null`，见 D-021），也避免常驻 `PublishControl` 误判。
- `Ctrl+D`：复用框选剪切路径删除，删除后恢复删除前的软件剪贴板（Il2Cpp 回调用堆对象传 `deleteOnly`；线剪贴板深拷贝再还原）。
- 流速缓动插入过滤 float 塌缩到端点的样本，避免首位拍双写速度事件。
- 「关闭边缘遮罩」按钮已取消；横向缩小自动藏 `editorMask`。
- 调色板写入 `ProjectInformation.colorPanels`；画线/颜色/主题均可一键导入。
- 预览细线约 75% 线宽；系统剪切板空格/制表符互通；摄像机移动框选等同画布移动。
- 修复画线工具双挂 `OnEnable`（共享 RVA `0x2CF1D0`）导致的卡死。
- 正式包 `RhythmEditorMod-0.2.0-win-x64.zip` 曾装入 `RhythmEditor_modded` 并通过安装/启停/卸载流程；日常开发以 `RhythmEditor_working` 为准。

## 已完成（阶段回顾）

- 建立 `RhythmEditor_original/` / `RhythmEditor_working/` / `RhythmEditor_modded/` 与 SHA-256 baseline。
- 确认 Unity 2022.3.62f2 Windows x64 IL2CPP；无 Unity 工程源码。
- 建立文档体系、`AGENTS.md`、Git（working）、插件构建安装脚本与管理器。
- 画布流速缓动离散采样与流速/画布事件剪贴板接入编辑器，Undo/Redo 走原 `EditControl.Stepping`。
- 谱面格式与颜色/Hold/`floorPosition` 等大量字段已在 `CHART_FORMAT.md` 中确认。

## 已知限制

- 每次启动后，首次框选前需先进入一次「线编辑」标签，以初始化原框选组件。
- 横向缩小时优先保证界面可见，部分按钮可能点不到。
- Note 界面框选删除、跨画布复制线、时间点列表、额外缓动曲线等仍见 `待实现功能.txt`。
- 正式对外发版需另打版本号与 ZIP；`0.2.0` 与 `0.3.0` 的目录包与 ZIP 并存，勿覆盖已发布的旧版本。
- `RhythmEditor_modded` 已于 2026-09-22 更新到 **0.3.0**（`install.json` 的 `InstalledAt` 为 `2026-09-22T20:06:06+08:00`，更新备份在 `.rhythm-editor-mod/backups/20260922-200217`），管理器界面与编辑器运行均已验收通过。验收时再次执行安装/更新未写入任何文件，确认重复更新是幂等的。
- 工作区目录曾被搬迁：`BepInEx/LogOutput.1.log` 记录 `D:\WorkShop\RZEMOD`；随后的 `D:\RZEMOD` 只在本轮调查初期被读到，该日志已被后一次启动覆盖；现为 `RZEMOD`，并已在新路径完成运行验证。`doorstop_config.ini`、BepInEx 路径和 `install.json` 都不含绝对路径，搬迁本身不影响运行。
- 编辑器按 `PlayerPrefs` 的 `lastFilePath` 自动打开上次谱面，该路径可能位于游戏安装目录；此时自动保存会写入该目录。做隔离实验前需先清空或改写 `lastFilePath`。
- `BepInEx/plugins/RhythmEditorMod/` 下留有 4 个编辑器运行时构建产生的 `.locked-*` 残留文件，已由 `.gitignore` 排除，不影响运行。
- `src/RhythmEditorMod/ToolbarProbe.cs.disabled` 等 8 个 `.cs.disabled` 文件是调查用的临时探针（工具栏结构、缓动求值、缓动流程、原生 UI 清单、工具 body 结构、工具栏行为、工程文件生命周期、切换验证）：都已从 `Plugin.cs` 摘除注册并改成 `.disabled` 扩展名，脱离 `*.cs` 编译范围，因此不会被编进任何构建或发布包，也不进 Git。结论均已写入 `docs/DECISIONS.md` 与 `docs/features/`，需要复用某个探针时改回 `.cs` 并重新注册即可。
- 「项目」工具的 body **不跟随鼠标**：切到别的工具才隐藏。工具栏原生的 hover 淡出/再点锁定行为已实测（`CanvasGroup.alpha` 0/1 + `locked` 翻转），但复刻与托管两条路线都会让面板在被淡出后 `blocksRaycasts=false`、鼠标无法进入，故按使用者要求保持「选中即显示」。
- body 的位置/尺寸/标题框/字数上限都在 `BepInEx\config\RhythmEditorMod\layout.json` 里，改动需**重启编辑器**生效；未知键会在日志中告警。

## 下一步建议

1. 验收两个待验收功能：① 画布弹性（`In/Out/InOut Elastic` 的形状与节点密度、`Ctrl+Z` 一次回退）；② 工程切换（保存保护、三个槽位、选择文件、记忆是否落盘）。确认后把 D-023 / D-024 收尾并提交。
2. 从 `待实现功能.txt` 选取下一项（例如 Note 框选删除、跨画布线、时间点列表）。
3. 做迁移或隔离实验前，先改写或清空 `PlayerPrefs` 的 `lastFilePath`，避免自动保存写入游戏安装目录。
