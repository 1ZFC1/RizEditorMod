# RhythmEditorMod 安装包

## 使用方法

1. 退出 Rizline Editor。
2. 将完整的 `RhythmEditorMod` 文件夹放入编辑器根目录；它应当与 `Rizline Editor.exe` 并列。
3. 双击 `RhythmEditorMod/RhythmEditorMod.Manager.exe`。
4. 选择“安装或更新”。
5. 安装完成后正常启动编辑器。

管理器只支持清单中记录的 `GameAssembly.dll` 版本。版本或文件哈希不匹配时会在写入任何文件前停止。

## 更新

退出编辑器，用新安装包中的 `RhythmEditorMod` 文件夹替换旧安装包文件夹，再运行管理器并选择“安装或更新”。普通功能更新只会替换插件文件；运行环境未变化时不会重复写入。

## 禁用和卸载

- “禁用插件”保留全部文件，只让 BepInEx 不再加载主插件。
- “卸载插件”删除本插件，保留 BepInEx，方便以后重新安装。
- “完整移除”只删除管理器创建且哈希未改变的运行文件；检测到其他插件或已修改文件时会停止或保留文件。

安装记录和更新备份位于编辑器目录的 `.rhythm-editor-mod/`。不要手工编辑其中的 `install.json`。

## 日志

运行问题请查看编辑器目录中的 `BepInEx/LogOutput.log`。安装问题请保留管理器窗口中的完整文字。
