# GitHub 仓库维护指南

公开仓库：<https://github.com/1ZFC1/RizEditorMod>（Public，MIT 覆盖插件自身源码）
本地公开工作区：`RZEMOD\RizEditorMod\`（与 `RhythmEditor_working` / `RhythmEditor_modded` / `RhythmEditor_original` 并列）
同步脚本：`RhythmEditor_working\tools\Publish-GitHub.ps1`

## 1. 为什么要单独一个工作区

`RhythmEditor_working` 是**开发仓库**（80+ 提交、含内部调查过程与私人笔记）；公开仓库是它的
**发布镜像**，只有 1 条起始提交：

- 发布不会改动开发仓库（不删文件、不重写路径）✓
- 公开历史干净，随时可以「重新同步」覆盖 ✓
- 私人笔记、游戏文件、interop/metadata、发布 ZIP 永远不会被带上去 ✓

## 2. 目录对应关系

```text
RhythmEditor_original\   只读基线（绝不发布）
RhythmEditor_working\    开发仓库（改代码、跑测试、提交都在这）
RhythmEditor_modded\     日常使用副本（绝不发布）
RizEditorMod\            ★ 公开仓库工作区（推送这个）
   ├─ .git\              origin = https://github.com/1ZFC1/RizEditorMod.git
   ├─ README.md          手写，同步时**保留**，不会被覆盖
   ├─ LICENSE            手写，同步时**保留**
   └─ src\ tests\ tools\ installer\ baseline\ research\ docs\ AGENTS.md\ .gitignore
                         ← 由脚本从 working 的 git 跟踪文件镜像而来
```

## 3. 日常：把改动发布到 GitHub

```powershell
# 1) 在开发仓库里正常开发、测试、提交
cd RZEMOD\RhythmEditor_working
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Install-RhythmEditorMod.ps1
git add -A; git commit -m "feat: ..."

# 2) 同步到公开工作区（只同步，不提交）
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-GitHub.ps1

# 3) 看一眼再推：提交 + 推送
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-GitHub.ps1 -Commit -Push -Message "sync: 0.4.x 更新"
```

脚本行为：

- 只取 `git ls-files` 的**跟踪文件**（未跟踪的日志/缓存/私人笔记自然被排除）
- **排除**：`docs/待实现功能.txt`、`docs/问题BUG.txt`（私人笔记；要改排除清单就编辑脚本顶部的 `$excluded`）
- **保留**：`.git/`、`.github/`、`README.md`、`LICENSE`（不会被镜像覆盖或删除）
- **路径规范化**：把 `RZEMOD\...` 之类的本地绝对路径改成相对路径后再写入
- 会删除公开工作区里「已不在发布集合」的多余文件，所以重复执行是安全的
- 常用参数：`-Target <目录>`（换工作区）、`-Commit`、`-Push`、`-Message`、`-RemoteUrl`

## 4. 永远不要上传的东西

| 内容 | 原因 |
| --- | --- |
| `Rizline Editor.exe`、`Rizline Editor Manual.exe`、`Rizline Editor_Data/` | 游戏本体与资源 |
| `GameAssembly.dll`、`UnityPlayer.dll`、`baselib.dll`、`winhttp.dll` | 游戏/运行库二进制 |
| `BepInEx/`（尤其 `interop/`、`metadata/`） | 由游戏生成、不可再分发 |
| `artifacts/`（发布 ZIP） | 内含 BepInEx 运行时 + interop/metadata |
| `#Samples/`、任何音频（`*.ogg`） | 游戏/样例音频 |
| `docs/待实现功能.txt`、`docs/问题BUG.txt` | 私人笔记（脚本已排除） |
| `BepInEx/LogOutput.log`、崩溃转储、缓存 | 无意义且可能含路径信息 |

`.gitignore` 已经覆盖以上绝大部分；**不要**用 `git add -f` 强行绕过。

## 5. 发新版本（以 0.4.x 为例）

```powershell
# 1) 改版本号并导出安装包（本地生成，不发布）
#    src\RhythmEditorMod\Plugin.cs -> Version = "0.4.1"
cd RZEMOD\RhythmEditor_working
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Export-RhythmEditorModPackage.ps1 -Version 0.4.1
#    → artifacts\releases\RhythmEditorMod-0.4.1-win-x64.zip (+ .sha256)

# 2) 按 docs\DISTRIBUTION.md 在干净副本与 modded 上验证安装流程，并由使用者验收

# 3) 更新文档里的版本与哈希（CHANGELOG / STATUS / DISTRIBUTION），提交开发仓库

# 4) 同步并推送源码
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-GitHub.ps1 -Commit -Push -Message "release: 0.4.1 sources"

# 5) 打标签并推送标签
cd RZEMOD\RizEditorMod
git tag -a v0.4.1 -m "RizEditorMod 0.4.1 sources"
git push origin v0.4.1
```

在 GitHub 上还可以（可选）为标签建一个 Release：**只写说明与 ZIP 的 SHA-256，不要上传 ZIP**
（ZIP 内含游戏派生的 interop/metadata）。若你只想提供二进制，最多上传自己构建的
`RhythmEditorMod.dll` + `RhythmEditorMod.Core.dll`，并在说明里写清「缺少 interop 无法单独运行」。

## 6. 认证与凭据

- 本机使用 **Git Credential Manager**（`credential.helper = manager`）。首次推送会弹出浏览器登录；
  凭据由 Windows 凭据管理器保存，之后推送不再询问 ✓
- **绝不要把 token/密码写进仓库、脚本或聊天记录** ✓
- 换电脑或凭据失效时：执行任意推送命令 → 按提示在浏览器登录即可；也可用
  `git credential-manager github login`
- 想用命令行建仓库/发 Release，可以装 GitHub CLI：`winget install --id GitHub.cli`，然后
  `gh auth login`、`gh repo create`、`gh release create`（凭据同样由系统保管）

## 7. 常见坑（本项目已踩过）

1. **中文文件名**：`git ls-files` 默认转义非 ASCII 路径并加引号，PowerShell 5.1 又会用控制台代码页解码
   原生命令输出 → 路径变乱码。脚本改用 `-c core.quotepath=false ls-files -z` + 显式 UTF-8 解码 ✓
2. **PowerShell 脚本编码**：含中文的 `.ps1` 若保存为「无 BOM 的 UTF-8」，PowerShell 5.1 会按 GBK 读，
   中文变乱码并导致语法错误。本项目脚本统一 **UTF-8 with BOM** ✓
3. **推送的退出码**：`git push` 把进度写到 stderr，PowerShell 会把它显示成错误、退出码为 1；
   判断是否成功要看输出里有没有 `* [new branch]` / `main -> main`，或事后 `git ls-remote origin` ✓
4. **行尾**：仓库里是 LF，Windows 检出时会提示「LF will be replaced by CRLF」，属正常 ✓

## 8. Issue 口径

- 本项目为**个人向**，不保证支持与合并；Issue 建议附：编辑器版本、`GameAssembly.dll` 哈希、
  `BepInEx\LogOutput.log` 相关片段、复现步骤
- 涉及游戏本体、破解、绕过版本校验、分发游戏资源的请求一律不接受
- 编辑器版本升级后插件会**拒绝加载**（哈希不匹配），这不是 bug，需要重新调查 interop/metadata

## 9. 收到 PR 时怎么处理

公开仓库是**只读镜像**（由开发仓库同步而来）。因此**不要直接 merge**：那样改动会在下一次同步时被覆盖。
正确做法是先把贡献者提交**摘进开发仓库**，再按常规流程发布。

```powershell
cd RZEMOD\RhythmEditor_working

# 1) 抓取贡献者的 fork（每个贡献者只需添加一次 remote）
git remote add contributor https://github.com/<贡献者>/RizEditorMod.git
git fetch contributor

# 2) 审查后把他的提交摘进开发仓库（保留原作者署名）
git cherry-pick <提交哈希>            # 多个提交依次进行；冲突在开发仓库里解决
#    也可以：git checkout -b pr-<编号> contributor/main，改好后再合回 main

# 3) 验证 + 同步发布
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Build-Install-RhythmEditorMod.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Publish-GitHub.ps1 -Commit -Push -Message "feat: 采纳 PR #<编号>（@<作者>）"
```

### 评审清单（逐项确认后再 cherry-pick）

1. **测试**：贡献者是否跑了纯数据测试并给出结果（当前基线 **51/51**）；插件与 Core 是否 0 警告 0 错误
2. **环境**：是否写明编辑器版本与 `GameAssembly.dll` 哈希（`F6CCB822…D683876`）——版本不一致的改动本机无法验证
3. **范围**：是否「一次一个明确任务」，没有顺手重构/格式化/升级依赖
4. **合规**：没有游戏本体、`GameAssembly.dll`、`BepInEx/interop|metadata`、音频、发布 ZIP、日志缓存；
   没有搬运反编译得到的游戏源码；引用的第三方实现已注明来源与许可
5. **文档**：用户可见改动更新了 `CHANGELOG.md`，设计取舍进 `DECISIONS.md`，定位变化进 `CODEMAP.md`
6. **回归**：手动复现一次改动前后行为，确认不影响原版路径（不按 `Shift`、不缩放时的行为必须与改造前一致）

### 处理完成后

在 GitHub 上给该 PR 留言并关闭（例如）：「已 cherry-pick 到开发线（提交 `<哈希>`），随下次同步发布，感谢贡献！」
若改动需要调整，直接在 PR 里留言说明，让作者更新分支后再摘取。

> 注意：`CONTRIBUTING.md` 与 `.github/` 模板**只存在于开发仓库**，不随同步发布，因此公开仓库的
> PR/Issue 页面**不会自动套用模板**；评审时请按上面的清单人工核对。
