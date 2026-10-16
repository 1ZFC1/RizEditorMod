# 阶段 5：IL2CPP 元数据恢复与文件入口定位

调查日期：2026-08-29

状态：静态、只读调查已完成元数据恢复、方法体映射和核心代码地图；未修改任何编辑器发布文件。

## 目标

- 判断 `global-metadata.dat` 的保护方式是否可恢复。
- 恢复足够的类型、字段和方法签名，以定位谱面打开、保存、另存和自动保存入口。
- 为后续方法体地址映射建立可重复工具链。

## 输入版本

```text
F6CCB82225839ABC8155C2A1241B4ACAF22E4A35BDCDF49B64B2AA550D683876  GameAssembly.dll
FEB10B4F20A1AB5858072585E1AFB55047440596A1D4FAA02A37E681014F6562  Rizline Editor_Data/il2cpp_data/Metadata/global-metadata.dat
```

以下所有 RVA、结构排列和结论只对这组哈希确认。

## 保护层

状态：`confirmed`

`global-metadata.dat` 原文件：

- 文件头不是标准 IL2CPP 魔数，前 4 字节为 `00 C0 00 C0`，即小端 `0xC000C000`。
- 第 4 至第 7 字节声明受处理载荷长度 `7,793,508`。
- 文件总长 `7,793,616`；程序只处理外层头之后声明长度的载荷，尾部另有 100 字节不进入该加载路径。
- 原文件 Shannon 熵约为 `7.999979 bit/byte`，且没有 `Assembly-CSharp`、`ProjectControl` 等明文字符串。

`GameAssembly.dll` 中的加载路径：

- `global-metadata.dat` 字符串 RVA：`0x1A6B4C8`。
- 对该字符串的加载器引用：RVA `0x1916F6`。
- 加载器在 RVA `0x191722` 检查标准魔数 `0xFAB11BAF`。
- 非标准文件从偏移 `+8` 复制声明长度的数据，调用 RVA `0x194720` 就地处理，再调用 RVA `0x18E2A0` 重建元数据头映射。

RVA `0x194720` 的行为与实际认证结果共同确认：

- AES-256-GCM；
- 12 字节 nonce；
- 16 字节认证标签；
- 无附加认证数据；
- AES-256 的 15 组轮密钥以转置形式存放在 `GameAssembly.dll` RVA `0x1A6B230`，脚本从匹配 DLL 动态恢复原始密钥，不在仓库中硬编码密钥。

认证解密成功后，明文不是压缩数据，而是：

- 开头 31 个小端 `uint32` 区段长度，共 124 字节；
- 后接按自定义顺序排列的 31 个 IL2CPP 元数据区段；
- 31 个长度之和为 `7,793,356`，与明文体长度精确一致。

RVA `0x18E2A0` 对 31 个长度和区段顺序的映射已完整转写进脚本。重建后的标准文件：

```text
C358B6FA326B4B91117364B23464CF396BD5559A017B23D701B66B179FFC1FAC  research/generated/global-metadata.standard.dat
```

其文件头为标准 IL2CPP 魔数 `AF 1B B1 FA`、版本 `31`；31 个区段均位于文件范围内，从 `0x100` 起无空隙、无重叠，最后一个区段恰好结束于文件末尾。

同一份文件同时用于 `BepInEx/metadata/global-metadata.standard.dat`、`RhythmEditor_modded` 和 `RhythmEditorMod-0.2.0-win-x64` 安装包 payload；四处哈希一致。

### 区段顺序修正（2026-09-22）

本文件早期版本记录的哈希为 `461BB20F22AB8755952DC9534028623F2FB9D90037B7AE8BC5062CD18DFF8644`。该值对应脚本中 `BODY_PAIR_ORDER` 仍把第 12 号区段排在第 7 号区段之前的版本，属于**错误映射**，已在脚本中修正为第 7 号在前。旧输出保留在 `research/generated/verify-standard.dat` 作为对照证据。

两者只有 256 字节标准头的第 7、12 号区段偏移不同，正文区完全相同，因此仅靠哈希或"能否解析出类型名"无法区分。判定依据是这两个区段的语义：

- 第 7 号区段按惯例为 `fieldDefaultValues`，记录长度 `FieldDefaultValue` 12 字节；第 12 号区段为 `genericParameters`，记录长度 16 字节。两版对这两个区段记录的大小分配相同（`159024` 与 `35264`），差别只在于正文里哪一段归谁。
- 当前版本把 `159024` 字节解释为 13252 条 `FieldDefaultValue`，其 `fieldIndex` 全部落在 `[0, 40185)` 内，最小值 `53`、最大值 `40184`，恰好等于字段总数减一；其中 **11654 / 13251** 条相邻记录满足"同一 `typeIndex` 且 `fieldIndex` 递增 1"，符合数组或结构体初始化的形态。
- 错误版本把同一段按 12 字节解释时 `fieldIndex` 达到 `1572864`，远超字段总数；相邻结构化记录数为 **0 / 13251**。

因此修正后的顺序可由数据本身证明正确，而不是仅由哈希自洽推断。

## 复现验证（2026-09-22）

在 `RhythmEditor_working/` 根目录重跑，输出到独立路径以免覆盖既有产物：

```powershell
python tools\Decrypt-RizlineMetadata.py GameAssembly.dll `
  "Rizline Editor_Data/il2cpp_data/Metadata/global-metadata.dat" `
  research/generated/repro-payload.dat `
  --standard-output research/generated/repro-standard.dat
```

观察结果与本文记录一致：

```text
wrapper_marker=0xC000C000
declared_payload_bytes=7793508
ignored_trailing_bytes=100
plaintext_bytes=7793480
plaintext_head=2049000050b60300207c000060730000
standard_bytes=7793612
standard_head=af1bb1fa1f000000c4ac0000807a0100
```

重跑产物哈希与既有产物逐字节一致：

```text
17DAB7652B36E893E82363B32982EF6F1E97BCF1BF9BAFB8D938957DD184004E  repro-payload.dat  == global-metadata.decrypted-payload.dat == verify-payload.dat
C358B6FA326B4B91117364B23464CF396BD5559A017B23D701B66B179FFC1FAC  repro-standard.dat == global-metadata.standard.dat
```

再用重建产物复查元数据语义，类型、字段、方法 token 与 `docs/CODEMAP.md` 记录一致：

```text
FileControl  type index 8099，8 个字段（jsonSettings / projectInformation / _songLoadOver / chartJson / db / pendingChart / pendingClose / songPath）
             Save=0x0600005D  AutoSave=0x0600005E  SaveAs=0x06000062  OpenProject=0x06000063
             OpenLastProject=0x06000064  NewProject=0x06000065  SaveCache=0x0600006E
Chart        10 个字段，含 bPM 与 __db
LinePoint    6 个字段（time / xPosition / color / easeType / canvasIndex / floorPosition）
```

同时确认全局计数：`type_definitions` 9103 项、`method_definitions` 62189 项、字段 40185 项、字符串堆 `1197988` 字节。

## 可重放工具

- `tools/Find-PeRipReferences.py`：定位 PE 中对指定 ASCII 字符串的常见 x86-64 RIP 相对引用候选。
- `tools/Decrypt-RizlineMetadata.py`：认证解密受保护载荷，并可选择重建标准 IL2CPP v31 元数据。
- `tools/Inspect-Il2CppMetadata.py`：读取标准元数据，按类型列出字段、嵌套类型、方法和参数签名。
- `tools/Inspect-Il2CppCodeGenModule.py`：通过 IL2CPP code-generation module 的方法指针表，把 MethodDef token 映射到当前 DLL 的 RVA。
- `tools/Analyze-Il2CppCalls.py`：在指定方法范围内列出直接 `E8/E9` 分支候选，并以恢复的各程序集方法表标注精确命中的托管方法地址；重要结果仍需反汇编核对。

生成物写入 `.gitignore` 已覆盖的 `research/generated/`，不会进入普通 Git 历史。脚本依赖当前 Python 环境中的 `pefile` 与 `cryptography`。

典型命令：

```powershell
python tools/Decrypt-RizlineMetadata.py `
  GameAssembly.dll `
  'Rizline Editor_Data/il2cpp_data/Metadata/global-metadata.dat' `
  research/generated/global-metadata.decrypted-payload.dat `
  --standard-output research/generated/global-metadata.standard.dat

python tools/Inspect-Il2CppMetadata.py `
  research/generated/global-metadata.standard.dat `
  --type FileControl `
  --type DataManagerController `
  --type ProjectInformation

python tools/Inspect-Il2CppCodeGenModule.py `
  GameAssembly.dll Assembly-CSharp.dll `
  0x0600005D 0x0600005E 0x06000062 0x06000063 0x0600006E

python tools/Analyze-Il2CppCalls.py `
  GameAssembly.dll `
  research/generated/global-metadata.standard.dat `
  Assembly-CSharp.dll 0x0600005D
```

## 已定位的原源码路径字符串

状态：`confirmed`（字符串存在）；源码文件本身不存在。

- `Assets/Scripts/MainScripts/DataManagerController.cs`
- `Assets/Scripts/MainScripts/FileControl.cs`
- `Assets/Scripts/MainScripts/ProjectControl.cs`
- `Assets/Scripts/MainScripts/ProjectInformation.cs`
- `Assets/Scripts/MainScripts/WorkspaceDB.cs`

## 文件与谱面入口

状态：类型归属和签名 `confirmed`；方法体行为除运行实验已证实部分外仍为 `unknown`。

### `DataManagerController`

- `SelectOpenPath(title, formatName, format)`
- `SelectSavePath(title, formatName, format)`
- `GetOpenFileName(ofn)`
- `GetSaveFileName(ofn)`

### `FileControl`

关键字段：

- `jsonSettings`
- `projectInformation`
- `chartJson`
- `db`
- `pendingChart`
- `pendingClose`
- `songPath`

关键方法：

- `Save()`
- `AutoSave()`
- `Start()`
- `AutoSaveCountdown()`
- `OnApplicationQuit()`
- `SaveAs()`
- `OpenProject()`
- `OpenLastProject()`
- `NewProject()`
- `CheckIfLoadCompleted()`
- `SetBpm()`
- `LoadMusic()`
- `LoadOGG(filePath)`
- `LoadMP3(filePath)`
- `OnChartDirty()`
- `OpenSampleFolder()`
- `OpenManual()`
- `SaveCache(filePath, chart)`

嵌套类型 `<AutoSaveCountdown>d__11` 确认 `AutoSaveCountdown` 是编译器生成状态机所对应的迭代器/协程方法。

### `ProjectInformation`

关键字段包括 `fileControl`、`chart`、`notSaved`、`<ChartIsDirty>k__BackingField`、`filePath`、`songCachePath`。关键状态方法包括 `MarkChartDirty()`、`ClearChartDirty()`、`SyncWindowTitle()`。

### `WorkspaceDB`

字段为 `writer`、`db`、`services`；方法为 `Register(service)`、`Get(key)`、`Init(records)`、`GetAll()`。它是持久化候选，但当前没有证据证明它直接写谱面 JSON。

## 已修正的旧判断

- `NewProject`、`OpenProject`、`OpenLastProject` 不属于 `ProjectControl`，而属于 `FileControl`。
- `ProjectControl` 当前只有 `Start`、`OnApplicationQuit` 和构造函数三个确认方法，主要字段是启动界面的谱面名、歌曲名和路径。
- `global-metadata.dat` 不是无法继续处理的随机加密文件；保护和区段排列均可从匹配版本的 `GameAssembly.dll` 静态恢复。

## 代码注册表与方法体映射

状态：`confirmed`

标准元数据的 image-definition 区段包含 54 个程序集；`Assembly-CSharp.dll` 为 image 13，覆盖 type definition 8072 起的 195 个类型。`GameAssembly.dll` 中相应 code-generation module 位于 RVA `0x1791860`，声明 1,029 个方法指针，方法指针表位于 RVA `0x1C7AFF0`。

当前版本的关键 `FileControl` 方法：

| 方法 | token | RVA |
| --- | --- | ---: |
| `Save` | `0x0600005D` | `0x2A9440` |
| `AutoSave` | `0x0600005E` | `0x2A7610` |
| `SaveAs` | `0x06000062` | `0x2A8E60` |
| `OpenProject` | `0x06000063` | `0x2A8770` |
| `OpenLastProject` | `0x06000064` | `0x2A81C0` |
| `NewProject` | `0x06000065` | `0x2A7EB0` |
| `SaveCache` | `0x0600006E` | `0x2A91E0` |

这些 RVA 已同时通过方法指针表、PE exception/runtime-function 范围和 `objdump` 反汇编核对。带异常处理的方法可能由多个 runtime-function/funclet 组成，因此研究工具把下一个不同方法入口作为扫描上界，不能仅把第一条 exception entry 的结束地址当成完整 C# 方法结束。

## 文件读写调用图

状态：下列直接调用和寄存器数据流 `confirmed`；泛型/间接调用的完整签名仍有边界

### 保存与另存

- `Save` 验证当前项目路径；不可用时尾跳转到 `SaveAs`。
- `SaveAs` 调用 `DataManagerController.SelectSavePath` 获取路径。
- 两者都会写入两个 `PlayerPrefs` 字符串并先调用 `AutoSave`。
- 随后调用 `WorkspaceDB.GetAll`，把结果临时写入当前 `Chart.__db`；以 `JsonConvert.SerializeObject(value, Formatting, settings)` 序列化 `Chart`，再调用 `File.WriteAllText` 写入当前路径。
- 写入后清空 `Chart.__db`。`SaveAs` 还调用 `ProjectInformation.SyncWindowTitle`，并进入 `ProjectInformation.set_NotSaved`。

这修正了此前“WorkspaceDB 是否参与谱面保存未知”的判断：它不直接执行文件写入，但 `GetAll()` 的结果会在序列化前临时挂到 `Chart.__db`。

### 自动保存

- `AutoSave` 要求当前路径非空且文件存在。
- 它先以 `JsonConvert.SerializeObject(value, settings)` 序列化当前 `Chart`，与 `FileControl.chartJson` 比较；相同则返回，不同则更新比较基线。
- 它从主谱面路径组合自动保存目录和文件名前缀，枚举已有文件、解析编号，并包含轮换删除逻辑。
- 最终同样把 `WorkspaceDB.GetAll()` 临时写入 `Chart.__db`，生成编号路径，序列化、`File.WriteAllText`，再清空 `Chart.__db`。

该调用图解释了运行实验中“未变化时不会产生新自动保存”和“保存文件包含工作区数据准备步骤”的可能实现基础；自动保存保留数量、文件名常量与轮换阈值尚未从元数据 usage 映射中完全恢复。

### 打开

- `OpenProject` 两次调用 `SelectOpenPath` 并验证路径，随后 `File.ReadAllText` 读取谱面文本。
- RVA `0x436E40` 的泛型实例负责把文本转换为 `Chart`，结果立即写入 `ProjectInformation.chart`。它的调用形态与泛型 JSON 反序列化一致，但共享泛型注册尚未恢复出完整显示名，因此具体重载标为 `likely`。
- 方法更新项目/歌曲路径与 `PlayerPrefs`，调用 `LoadMusic`，启动音乐加载协程。
- 当前 `Chart` 随后经 `JsonConvert.SerializeObject(value, settings)` 保存到 `FileControl.chartJson` 作为变化比较基线，再启动 `CheckIfLoadCompleted` 协程。
- `OpenLastProject` 从 `PlayerPrefs.GetString` 取回两个路径，然后执行同类读取、反序列化、音乐加载和比较基线建立流程。

### 缓存

`SaveCache(filePath, chart)` 创建缓存目录，至少两次序列化并写入 JSON，同时遍历待处理谱面。对整个 DLL 的 `E8` 目标扫描没有找到直接调用其入口 RVA `0x2A91E0`；这不排除委托、接口、协程或其他间接调用，也可能表示当前版本没有使用该入口。

## 核心编辑模块

状态：类型、字段、方法签名、RVA 和列出的直接调用 `confirmed`

- `EditControl.Stepping`（RVA `0x2A55C0`）两次调用 `Chart.Clone`，调用 `MarkChartDirty` 并管理历史记录；`Undo`（`0x2A5810`）和 `Redo`（`0x2A3B00`）都会标记 dirty 并进入 `OnChartUndoRedo`。
- `LineEditor` 明确包含 `CopyLines`、`CutLines`、`SelectLines`、`Paste` 和 `ToolInput`；`Paste` 与 `ToolInput` 的编辑分支调用 `EditControl.Stepping`。
- `NoteEditor.ToolInput`（RVA `0x2D6E70`）的新增/修改分支调用 `EditControl.Stepping`，Hold 预览调用 `ProgressControl.ChartTimeRageToRealTime`。
- `InputControl` 提供鼠标位置、按钮、最近线和最近线位置计算入口。
- `CanvasEditor` 持有完整拖动起始状态，并提供移动关键点、增删画布和设置关键点入口。
- `ShortcutKeyControl` 统一暴露 Ctrl/Alt/Shift 状态和快捷键注册；`ShortcutKeysControl.Update`（RVA `0x2C3590`）按修饰键分派输入。
- `ProgressControl.PlayOrPause`（RVA `0x2BA1F0`）的暂停分支调用 `AudioSource.Pause`，播放分支设置 pitch/time 后调用 `AudioSource.Play`。

## 谱面数据模型交叉验证

状态：字段声明 `confirmed`

- `Chart` 声明 `fileVersion`、`chartDelayMs`、`themes`、`challengeTimes`、`bPM`、`bpmShifts`、`lines`、`canvasMoves`、`cameraMove` 和运行期字段 `__db`。
- `Line` 恰好声明 `linePoints`、`notes`、`judgeRingColor`、`lineColor` 四个数据字段。
- `LinePoint`、`Note`、`ChallengeTime`、`CanvasMove`、`CameraMove`、`ColorKeyPoint` 和 `KeyPoint` 的字段均与 `CHART_FORMAT.md` 记录的 JSON 结构逐项对应。
- `ColorKeyPoint` 自带 `CanMerge`、`ColorsAreEqual`、`CompareTo`；`KeyPoint` 自带 `CompareTo`。这些能力的存在不等于保存或渲染一定调用排序，运行实验已经证明颜色事件数组不会被自动排序。

## 当前边界

- 当前恢复的是托管元数据和原生方法映射，不是可直接重新编译的 C# 源码。
- 直接调用可以静态确认；虚调用、接口调用、委托、共享泛型实例和元数据 usage 字符串仍需进一步解析。
- `SaveCache` 的调用者、缓存文件精确命名、自动保存轮换阈值和泛型反序列化实例的完整签名尚未确认。
- 解密和头部映射常量均与当前 DLL 哈希绑定。换版本后必须重新定位并验证，认证失败时不得强行输出。
- 本调查仅服务于个人本地研究；生成的解密元数据和原软件二进制不应公开分发。
