# 发布 Playbook：私有开发仓库 → 公开 GitHub 镜像

> 通用流程说明，供其它项目复用。本项目（RizEditorMod）按此执行，细节见 `docs/GITHUB.md`。
> 占位符：`<DEV>` 开发仓库、`<PUBLIC>` 公开镜像工作区、`<REMOTE_URL>` GitHub 仓库地址。

## 0. 原则

1. **开发仓库不动**：不为了公开而删文件、改写路径或塞入发布专用文件。
2. **公开仓库是镜像**：内容完全可由脚本从开发仓库重建，重复执行结果一致（幂等）。
3. **白名单 + 排除表**：只同步「git 跟踪的文件」，再按排除表挡掉不该公开的东西。
4. **发布专用文件只存在于 `<PUBLIC>`**：`README.md`、`LICENSE` 等手写文件由同步脚本保留、不被覆盖。
5. **永不发布**：二进制/资源/生成物/私人笔记/日志/凭据。

## 1. 目录布局

```text
<ROOT>/
├─ <DEV>/        开发仓库（有自己的 .git 与完整历史）
├─ <PUBLIC>/     公开镜像工作区（自己的 .git，origin=<REMOTE_URL>）
└─ 其它/         游戏本体、构建产物、日常运行副本、临时验证副本……
```

## 2. 一次性准备

```powershell
# GitHub 上新建「空仓库」（不要勾选自动生成 README/LICENSE）
mkdir <PUBLIC>; cd <PUBLIC>
git init -b main
git config user.name  "<你的名字>"
git config user.email "<ID>+<用户名>@users.noreply.github.com"   # 隐私邮箱，仍能关联账号
git config core.quotepath false
git config core.autocrlf false                                   # 仓库内统一 LF，消除行尾警告
git remote add origin <REMOTE_URL>
```

然后在 `<PUBLIC>` 手写 `README.md`（项目定位、功能、安装、构建前提、兼容表、免责声明）与
`LICENSE`（只覆盖你自己的代码；明确声明不含第三方/生成物/资源）。

## 3. 同步脚本（核心，建议 `tools/publish.ps1`）

必须满足：

| 要求 | 做法 |
| --- | --- |
| 白名单来源 | `git -C <DEV> -c core.quotepath=false ls-files -z`，用 **NUL 分隔 + 显式 UTF-8 解码**（否则非 ASCII 路径被转义/乱码） |
| 排除表 | 精确文件名（私人笔记、`CONTRIBUTING.md`）+ 前缀（`.github/`） |
| 路径规范化 | 把本地绝对路径（`D:\...\<DEV>`）替换为相对路径后再写入 |
| 镜像 + 清理 | 复制白名单文件到 `<PUBLIC>`；删除不在白名单里的旧文件，但**保留** `.git/`、`.github/`、`README.md`、`LICENSE` |
| 幂等 | 第二次运行应 `nothing to commit, working tree clean` |
| 可选开关 | `-Commit`、`-Push`、`-Message`、`-Target` |
| 编码 | 脚本本身存 **UTF-8 with BOM**（PowerShell 5.1 否则按 GBK 读中文 → 语法错误） |
| git 调用 | 一律 `2>&1 \| Out-String \| Out-Host`（git 进度写 stderr，PowerShell 会当错误中断） |

关键片段（PowerShell 5.1）：

```powershell
# 1) 取跟踪文件（UTF-8、NUL 分隔）
$psi = New-Object Diagnostics.ProcessStartInfo
$psi.FileName = 'git'; $psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.StandardOutputEncoding = [Text.Encoding]::UTF8
$psi.Arguments = "-C `"$dev`" -c core.quotepath=false ls-files -z"
$p = [Diagnostics.Process]::Start($psi); $raw = $p.StandardOutput.ReadToEnd(); $p.WaitForExit()
$files = $raw -split "`0" | Where-Object { $_ -and ($excluded -notcontains $_) }

# 2) 复制 + 路径改写
$text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($src))
foreach ($pair in $rewrites.GetEnumerator()) { $text = $text.Replace($pair.Key, $pair.Value) }
[IO.File]::WriteAllText($dst, $text, (New-Object Text.UTF8Encoding($false)))

# 3) 删除不在白名单的旧文件（保留 .git/.github/README/LICENSE）
# 4) git -C <PUBLIC> add -A; commit -m $msg; push -u origin main
```

## 4. 日常发布（三步）

```powershell
cd <DEV>
# ① 开发：测试 + 提交
<测试命令>
git add -A; git commit -m "feat: ..."

# ② 同步到公开工作区（不提交）
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\publish.ps1

# ③ 提交并推送
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\publish.ps1 -Commit -Push -Message "sync: ..."
```

推送时若弹出 GitHub 登录窗口，按提示在浏览器授权即可（凭据由系统凭据管理器保存，之后免输入）。

## 5. 发新版本

1. 改**唯一的版本来源**（例如 `Plugin.Version` / `package.json` / `Cargo.toml`）。
2. 本地导出发行包（**不要上传**，尤其含第三方或生成物时）。
3. 在干净副本上验证安装/更新/卸载流程；确认核心文件哈希未变。
4. 使用者/自测验收。
5. 更新 `CHANGELOG`、`STATUS`、版本与兼容信息。
6. `publish.ps1 -Commit -Push -Message "release: x.y.z sources"`
7. 打标签并推送：

```powershell
cd <PUBLIC>
git tag -a vX.Y.Z -m "<项目> X.Y.Z sources"
git push origin vX.Y.Z
```

8. 可选：GitHub Release **只写说明与校验和**；若附二进制，仅限你自建、不含第三方/生成物的产物，并注明运行前提。

## 6. 认证与凭据

- 首次推送弹出 **Git Credential Manager** 浏览器登录；凭据存系统凭据管理器。
- **绝不**把 token/密码写进仓库、脚本或聊天记录。
- 换机器：随便执行一次推送 → 浏览器登录；或 `git credential-manager github login`。
- 想全命令行：`winget install --id GitHub.cli` → `gh auth login` / `gh repo create` / `gh release create`。

## 7. 已知坑（均为实际踩过）

1. **非 ASCII 路径**：`git ls-files` 默认转义加引号，PowerShell 5.1 又按控制台代码页解码 → 路径乱码。
   → `-c core.quotepath=false ls-files -z` + 显式 UTF-8 解码。
2. **含中文的 `.ps1`**：必须 UTF-8 **with BOM**，否则按 GBK 读 → 乱码与语法错误。
3. **`git push` 的退出码**：进度写 stderr，PowerShell 报错并返回 1（假失败）。
   → 看输出里的 `main -> main` / `[new branch]`，或事后 `git ls-remote origin` 核对。
4. **行尾**：仓库内统一 LF；检出时 CRLF 警告正常；公开仓库设 `core.autocrlf=false` 可静音。
5. **同步会删文件**：`README.md`/`LICENSE`/`.github/` 必须列入保留表，否则会被清掉。
6. **amend 之后**：`git push --force-with-lease`（仅限自己新建、无人克隆的仓库）。
7. **镜像会补齐所有已提交改动**：包括你在其它会话/分支提交的内容 → 想"暂不发布"，把对应文件或目录加进排除表。
8. **手写文件不要进白名单**：`README.md`/`LICENSE` 只存在于 `<PUBLIC>`，否则会被开发仓库里的版本覆盖。
9. **中文/emoji 文件名**在 Windows 上还要注意大小写与 NFC/NFD 差异（跨平台协作时优先用 ASCII 文件名）。

## 8. PR 处理（镜像架构特有）

公开仓库是只读镜像 → **不要直接 merge PR**（下次同步会被覆盖）。正确流程：

```powershell
cd <DEV>
git remote add contributor https://github.com/<作者>/<仓库>.git   # 一次性
git fetch contributor
git cherry-pick <提交哈希>            # 审查后摘入；冲突在此解决
<测试命令>
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\publish.ps1 -Commit -Push -Message "feat: 采纳 PR #<编号>（@<作者>）"
```

随后在 PR 下留言「已 cherry-pick 到开发线（`<哈希>`），随下次同步发布」并关闭。

评审清单：① 测试结果与基线 ② 环境（版本 + 关键二进制哈希）③ 范围（一次一个任务）
④ 合规（无第三方二进制/生成物/私有资源）⑤ 文档更新 ⑥ 回归（原有路径行为不变）。

> 若 `CONTRIBUTING.md` 与 `.github/` 模板留在开发仓库不发布，则公开仓库的 PR 页面**不会自动套模板**，
> 需人工按清单核对。

## 9. 发布前检查清单

- [ ] 开发仓库 `git status` 干净（除刻意保留的未跟踪文件）
- [ ] 同步第二次运行 → `nothing to commit, working tree clean`（幂等）
- [ ] 公开仓库文件清单核对：无二进制/资源/音频/生成物/私人笔记/日志/凭据
      （`git -C <PUBLIC> ls-tree -r --name-only HEAD`）
- [ ] 本地 = 远程（`git rev-parse HEAD` vs `origin/main`）
- [ ] 标签已推送（`git ls-remote --tags origin`）
- [ ] `README.md` 的版本号/兼容表/构建前提是最新的

## 10. 给另一个项目的 AI 的指令模板

```text
我要把一个私有开发仓库发布到 GitHub 公开仓库，请按下面的 Playbook 执行（细节见我附的文件）：

架构：<DEV> 开发仓库保持不动；<PUBLIC> 是公开镜像工作区（自己的 git 仓库，origin=<REMOTE_URL>）；
      用 tools/publish.ps1 从 <DEV> 的「git 跟踪文件」白名单同步，排除私人笔记等，并把本地绝对路径规范化为相对路径。

硬性要求：
1) 白名单用 `git -C <DEV> -c core.quotepath=false ls-files -z` 并以 UTF-8 解码（防非 ASCII 路径乱码）
2) 同步是镜像：删除不在白名单的旧文件，但保留 .git/、.github/、README.md、LICENSE
3) 幂等：第二次运行必须 nothing to commit
4) 脚本存 UTF-8 with BOM；所有 git 调用 2>&1 | Out-String（git 进度走 stderr，避免 PowerShell 假失败）
5) 永不发布：二进制/资源/生成物/私人笔记/日志/凭据；README 明确声明不含第三方/资源
6) 推送使用系统凭据管理器浏览器授权；绝不把 token 写进仓库/脚本/聊天
7) 发版：改唯一版本来源 → 本地导出（不上传）→ 干净副本验证 → 更新 CHANGELOG/STATUS → 同步推送 → 打 tag 推送
8) PR 不直接 merge 到镜像：cherry-pick 进 <DEV> → 测试 → 同步推送 → 关闭 PR

请先给出：
- 需要新建/修改哪些文件（脚本、README、LICENSE、排除表）
- 排除清单与保留清单的具体内容
- 首次发布与后续发布的命令序列
- 你打算怎么验证幂等与「无敏感文件」
等我确认后再动手。
```

## 11. 本项目（RizEditorMod）的对应实现

| Playbook 项 | 本项目实现 |
| --- | --- |
| 开发仓库 | `RhythmEditor_working` |
| 公开镜像 | `RizEditorMod`（<https://github.com/1ZFC1/RizEditorMod>） |
| 同步脚本 | `tools/Publish-GitHub.ps1`（排除：私人笔记、`CONTRIBUTING.md`、`.github/`） |
| 发布教程 | `docs/GITHUB.md` |
| 发行流程 | `docs/DISTRIBUTION.md`（改版本 → 导出 → 干净副本验证 → modded 更新 → 记录哈希） |
| 校验 | 同步幂等、公开仓库清单核对敏感文件、本地=远程、标签、干净副本 58/58 文件哈希 |
