# 编辑器内切换工程（工程工具）

状态：**已实现并装入 `RhythmEditor_working`，待使用者验收**。文件生命周期已由运行探针确认（第 2、5 节）；参数由使用者决定（第 3 节）。
相关：`CODEMAP.md` 的「JSON 谱面读取与保存」、D-024、`DECISIONS.md` D-022（插件工具栏入口）。

## 1. 需求

在编辑器内直接切换谱面工程，不必退出重进：

1. 记住**最近三个工程**（谱面 + 音乐），点击即刻跳转；
2. 另有「选择文件」入口，走原版的两步选择（先谱面、再音乐）；
3. 切换前后必须处理好相关文件，不能丢改动。

## 2. 原编辑器的工程生命周期（已知部分，证据见 CODEMAP）

| 环节 | 事实 | 证据 |
| --- | --- | --- |
| 打开（两步） | `FileControl.OpenProject()` 调用**两次** `DataManagerController.SelectOpenPath`（谱面、音乐），读取文本并反序列化到 `ProjectInformation.chart` | CODEMAP「已确认的打开主链」 |
| 打开（快捷通道） | `FileControl.OpenLastProject()` 从 `PlayerPrefs` 取回路径，再走同一套读取、音乐加载、基线序列化与 `CheckIfLoadCompleted` | 同上 |
| 保存 | `FileControl.Save()` →（无路径时转 `SaveAs`）；调用 `WorkspaceDB.GetAll` 填 `Chart.__db` → 序列化 → `File.WriteAllText` 覆写**所选谱面文件**；随后清空 `__db` | CODEMAP「已确认的保存主链」 |
| 自动保存 | `FileControl.AutoSave()` 只在路径非空且文件存在时执行；与 `FileControl.chartJson` 比较后，写入谱面同目录的 `Rizline Editor Auto-Save/` | CODEMAP「已确认的自动保存主链」 |
| 记忆路径 | `PlayerPrefs` 的 `lastFilePath`（谱面）与 `lastSongPath`（音乐） | `CODEMAP.md` PlayerPrefs 表 |
| 相关字段 | `ProjectInformation.filePath` / `songCachePath` / `songCacheExtension` / `notSaved` / `ChartIsDirty`；`FileControl.songPath` / `chartJson` / `pendingChart` / `pendingClose` | 类型转储 |

**文件生命周期（2026-09-23 运行探针确认，`ProjectProbe`）**：

| 项 | 实测 |
| --- | --- |
| 谱面工作副本 | **在内存**（`ProjectInformation.chart`），磁盘上没有「临时谱面文件」 |
| 音乐临时副本 | `ProjectInformation.songCachePath = %TEMP%\tmpXXXX.tmp`（`songCacheExtension = .ogg`，大小与 `music.ogg` 完全一致）。`%TEMP%` 下的 `tmp*.tmp` 全是音频（`OggS`/`RIFF`/`ID3`），**没有任何 JSON** |
| 临时副本会残留 | 每次加载都生成**新**文件，旧的不删（实测 `tmp16e80c68` → `tmp6f1813b4` → `tmp6469a1a3`） |
| 谱面目录构成 | `<谱面>.json`、`music.ogg`、`illustration.jpg`、`Rizline Editor Auto-Save/` |
| `PlayerPrefs` | 确认就是 `lastFilePath` / `lastSongPath` |
| 未保存标记 | 编辑后 **`notSaved=True`**（`ChartIsDirty` 仍为 False）→ 切换前的保存判断用它 |
| 加载完成标志 | `loadOver`（`ProjectInformation`）与 `_songLoadOver`（`FileControl`） |

## 3. 方案（参数已定）

### 3.1 记忆槽

- 存储：`BepInEx/config/RhythmEditorMod/projects.json`，明文、可查看可手改、可备份；**不动**编辑器的 `PlayerPrefs` 作为长期存储。
- 结构：最多 3 条，每条 `{ chartPath, songPath, lastOpenedUtc }`，按最近使用排序，去重按「谱面路径」。
- 采集：插件轮询 `projectInformation.filePath` + `fileControl.songPath`，两者都非空且与当前队首不同时，推入队首（这样无论用户是用原版两步对话框还是快捷通道打开的工程都会被记住）。

### 3.2 切换流程（点击记忆槽 / 选择完文件之后）

```text
1. 若当前工程未保存（ProjectInformation.notSaved / ChartIsDirty）：
     - 当前 filePath 非空 → 调用 FileControl.Save()（同步写主谱面 + 自动保存）
     - filePath 为空（从未保存过）→ 拒绝切换并提示，不改任何文件、不切工程
2. 记录目标 (chartPath, songPath) 到 PlayerPrefs 的 lastFilePath / lastSongPath
3. 调用 FileControl.OpenLastProject()：复用原版完整加载链（读取 → 反序列化 → 音乐加载协程
   → chartJson 基线重建 → CheckIfLoadCompleted）
4. 等待加载完成（ProjectInformation.loadOver / FileControl._songLoadOver），期间面板显示「切换中」
   并忽略重复点击；完成后把该工程推入记忆槽队首
```

要点：

- **不复制原版加载逻辑**。第 3 步直接复用编辑器自己的快捷通道入口，避免重现泛型反序列化、音乐加载与基线重建这些细节。
- **选择文件**用原版 `FileControl.OpenProject()`（两步对话框，标题与筛选同原版），选完由第 1 步之后的同一条链路接管；即「选择文件」= 原版入口 + 插件的保存保护与记忆。
- **保存策略**（使用者已定）：切走前自动保存；从未保存过的工程拒绝切换而不是弹「另存为」。

### 3.3 面板

复用「插件」工具栏入口（D-022）的面板：

- 顶部：当前工程（谱面名 / 音乐名 / 未保存标记）。
- 三个记忆槽按钮：`1. <谱面文件名>`、`2. …`、`3. …`（含音乐名与时间），点击即切换；空槽显示「（空）」。
- 「选择文件…」按钮：调用原版两步对话框。
- 状态行：保存/切换进度与失败原因。

## 4. 风险与保护

| 风险 | 处理 |
| --- | --- |
| 切换丢失未保存改动 | 切换前强制保存；无路径则拒绝（3.2 第 1 步） |
| 保存与自动保存的时序 | `FileControl.Save()` 内部本就会先 `AutoSave`，顺序由原版保证，插件不自己拼 |
| 切换是异步的，期间再点会叠加 | 面板置「切换中」状态并在加载完成前忽略输入 |
| 旧工程的自动保存文件留在其目录 | **不删除**任何文件；原版保存链与自动保存轮换已覆盖各自目录 |
| PlayerPrefs 被改写 | 只改 `lastFilePath` / `lastSongPath`，且写入的值就是即将加载的工程，语义与原版一致 |
| 缓存的音乐/谱面副本（待确认） | 探针确认 `songCachePath` 后决定：若按工程生成则由原版流程覆盖；若需要清理，则只在「确认原版会重建」的前提下清理 |
| 版本保护 | 不改 `GameAssembly.dll`、不改清单哈希、不动原版 EXE |

## 5. 核心假设的验证（`ProjectSwitchProbe`，2026-09-23）

| 试验 | 结果 |
| --- | --- |
| **F9**：把 `PlayerPrefs` 指回当前工程再调用 `OpenLastProject()` | ✓ 同一次运行内重载成功（`filePath` 不变，音乐缓存换成新的临时文件） |
| **F12**：切到另一份样例工程 | ✓ `filePath` 变为 `#Samples\Power Attack\Chart_IN.json`，`songPath` 同步；**内存谱面确实换了**（`chart.lines` 230 → 537） |
| 音乐缓存 | 每次加载生成新的 `%TEMP%\tmpXXXX.tmp`，**旧文件残留** → 切换成功后由插件清理旧的那一个 |
| 加载完成判定 | 切换后 `loadOver` / `_songLoadOver` 为 True；插件用「目标路径已生效 + 两个标志」判定完成，避免误判 |

## 5.1 实现落点

| 文件 | 职责 |
| --- | --- |
| `src/RhythmEditorMod.Core/ProjectSlotList.cs` | 「最近三个工程」的纯逻辑：去重（按谱面路径、Windows 大小写不敏感）、置顶、容量 3、空路径不入列表 |
| `src/RhythmEditorMod/ProjectSlots.cs` | 持久化到 `BepInEx/config/RhythmEditorMod/projects.json`（明文 JSON，可查看可手改） |
| `src/RhythmEditorMod/ProjectSwitcher.cs` | 保存保护、切换（写 `PlayerPrefs` + `OpenLastProject`）、选择文件（`OpenProject`）、完成判定、清理旧音乐缓存、轮询记录当前工程并更新状态行 |
| `src/RhythmEditorMod/NativeProjectPanel.cs` | 「项目」工具的 **原生 body**：克隆真实工具 body 与原生按钮搭建，标题「项目」 |
| `src/RhythmEditorMod/PluginLayout.cs` | body 的布局参数（`BepInEx/config/RhythmEditorMod/layout.json`） |

自动测试：`tests/RhythmEditorMod.Core.Tests` 新增 4 条（置顶顺序、按路径去重与保留音乐、容量裁剪、空路径忽略），合计 37/37。

## 5.1 状态行独立（2026-10-15）

项目 body 的**标题与状态行由 `NativeProjectPanel` 自己持有**（`_status` 字段 + 每帧 `Refresh()` 重设），项目消息（切换成功/失败、槽位为空、选择文件…）直连 `NativeProjectPanel.SetStatusText`。

**不再**与插件的公共状态出口 `SpeedEasePanel.ShowStatus` 互通：那一个承载所有功能的消息（画布流速缓动、剪贴板、整体缩放…），此前会无条件写进项目状态行，导致「设定缓动后项目面板显示缓动文字」。现在项目工具与其它功能互不影响（D-032）。

同一条规则也用于日志：文本只在**真的变化**时才记录一行，避免每帧重写同一内容时刷屏。
## 5.2 UI：原生 body（2026-09-23 由使用者验收）

工具栏入口与 body 标题都叫**「项目」**（`CustomToolbarTool.ButtonTitle`）。面板不是 IMGUI，而是用**克隆的原生控件**搭出来的：

- **容器**：克隆 `Canvas/Toolbar/Tools/Edit Canvas/Body`（最紧凑的真实 body：`Body > Title{BG}, Lock, Canvas Title, AddCanvas, DeleteCanvas`），销毁源工具自己的控件，保留原生标题栏与 `Lock`，并把标题框的子物体拉伸填满（否则文字会按源 body 的大框偏移、落到内容区里）。
- **行控件**：三个槽位 + 「选择文件…」克隆自同一 body 的 `DeleteCanvas`（`Graphic + Button + Text`），状态行克隆自 `Canvas Title`；文字统一**黑色**（源按钮是删除用的红字），标题栏**白色**，并开启自动缩放 + 按 `MaxLabelChars`/`StatusMaxChars` 截断加 `…`，避免文字顶出框。
- **标题框**：应用配置的尺寸/留白（发布标题栏的 `sizeDelta.x` 是 0，靠锚点拉伸，不能直接照抄），并把标题 `SetAsLastSibling()`，保证覆盖在内容之上而不是被内容压住。
- **面板命名 `PluginBody`**、显隐规则是「**选中该工具即显示**」，**不跟随鼠标**。
- 克隆体上原有的 `ToolbarBodyHolder` 会被移除（它们会把自己注册到源按钮上）。

`layout.json`（首次启动自动生成）可调：面板尺寸/锚点/轴心/偏移、首行位置、行高、左右边距、状态行额外高度、标题框尺寸与留白、标签与状态字数上限。读取时键名大小写不敏感，并会对**无法识别的键**告警，避免「改了没反应」查不出原因。

**回退记录（保留）**：期间试过两条更"原生"的路线，均按使用者要求撤回——

1. 复刻工具栏的 hover/lock 规则（悬停显示、离开淡出、再点锁定）：实测原版是**靠 `CanvasGroup.alpha` 淡入淡出**（body 对象始终 active），悬停→`alpha=1`、离开→`alpha=0`、再点已选中工具→`locked` 翻转。但插件淡出时同步设了 `blocksRaycasts=false`，导致「鼠标一离开按钮就消失、且再也进不去面板」，槽位根本点不到。
2. 把 body 直接交给原版托管：改名为 `Body` 并注册 `ToolbarBodyHolder`（`RegisterBodyHolder`）。仍然无法稳定复现「鼠标可进入面板」的使用体验，遂一并撤回。

最终选择「选中即显示」这一版：**不跟随鼠标**，因此不存在上述死循环。

## 6. 验收计划

1. 在隔离样例工程上：打开 A → 编辑 → 点记忆槽 B → 确认 A 被保存（A 的 JSON 字节变化、A 目录新增自动保存）、编辑器切到 B、可正常播放与编辑。
2. 反向切回 A，确认 A 的内容完整（含刚才的编辑）。
3. 未保存过的新工程（`NewProject`）直接点切换 → 应被拒绝并提示，且不产生任何文件。
4. 连续快速点击同一槽位 → 只执行一次切换。
5. 关闭编辑器重开 → 记忆槽仍在（`projects.json` 存在且内容正确）。
