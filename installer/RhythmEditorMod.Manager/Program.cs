using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RhythmEditorMod.Manager;

internal static class Program
{
    internal const string EditorExecutable = "Rizline Editor.exe";
    internal const string GameAssembly = "GameAssembly.dll";
    private const string StateDirectoryName = ".rhythm-editor-mod";
    private const string StateFileName = "install.json";
    internal const string PluginDll = "BepInEx/plugins/RhythmEditorMod/RhythmEditorMod.dll";
    internal const string DisabledPluginDll = "BepInEx/plugins/RhythmEditorMod/RhythmEditorMod.dll.disabled";
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            var managedBase = Path.GetFullPath(AppContext.BaseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var packageRoot = Path.GetFileName(managedBase).Equals(
                "manager-runtime",
                StringComparison.OrdinalIgnoreCase)
                    ? Directory.GetParent(managedBase)?.FullName
                        ?? throw new InvalidOperationException("无法从私有运行环境确定安装包目录。" )
                    : managedBase;
            var manifest = LoadManifest(packageRoot);
            var options = CommandOptions.Parse(args, packageRoot);
            var manager = new InstallationManager(packageRoot, options.TargetRoot, manifest);

            if (options.Command != null)
                return ExecuteCommand(manager, options.Command, options.AssumeYes);

            RunInteractive(manager);
            return 0;
        }
        catch (Exception error)
        {
            WriteColored(ConsoleColor.Red, $"错误：{error.Message}");
            if (args.Contains("--debug", StringComparer.OrdinalIgnoreCase))
                Console.Error.WriteLine(error);
            if (args.Length == 0)
            {
                Console.WriteLine("按 Enter 退出。");
                Console.ReadLine();
            }
            return 1;
        }
    }

    private static void RunInteractive(InstallationManager manager)
    {
        while (true)
        {
            Console.Clear();
            Console.WriteLine("RhythmEditorMod 管理器");
            Console.WriteLine(new string('=', 56));
            manager.PrintStatus();
            Console.WriteLine();
            Console.WriteLine("1. 安装或更新");
            Console.WriteLine("2. 启用插件");
            Console.WriteLine("3. 禁用插件");
            Console.WriteLine("4. 卸载插件（保留 BepInEx）");
            Console.WriteLine("5. 完整移除管理器安装的运行环境");
            Console.WriteLine("0. 退出");
            Console.Write("请选择：");
            var choice = Console.ReadLine()?.Trim();
            if (choice == "0")
                return;

            try
            {
                switch (choice)
                {
                    case "1":
                        manager.InstallOrUpdate();
                        break;
                    case "2":
                        manager.Enable();
                        break;
                    case "3":
                        manager.Disable();
                        break;
                    case "4" when Confirm("确认卸载插件？BepInEx 将保留。"):
                        manager.UninstallPlugin();
                        break;
                    case "5" when Confirm("确认完整移除？检测到未知或已修改文件时不会删除它们。"):
                        manager.RemoveManagedRuntime();
                        break;
                    default:
                        Console.WriteLine("未执行操作。");
                        break;
                }
            }
            catch (Exception error)
            {
                WriteColored(ConsoleColor.Red, $"操作失败：{error.Message}");
            }
            Console.WriteLine("按 Enter 返回主菜单。");
            Console.ReadLine();
        }
    }

    private static int ExecuteCommand(InstallationManager manager, string command, bool assumeYes)
    {
        switch (command.ToLowerInvariant())
        {
            case "status":
                manager.PrintStatus();
                return 0;
            case "install":
            case "update":
                manager.InstallOrUpdate();
                return 0;
            case "enable":
                manager.Enable();
                return 0;
            case "disable":
                manager.Disable();
                return 0;
            case "uninstall" when assumeYes || Confirm("确认卸载插件？"):
                manager.UninstallPlugin();
                return 0;
            case "remove-runtime" when assumeYes || Confirm("确认完整移除运行环境？"):
                manager.RemoveManagedRuntime();
                return 0;
            default:
                throw new ArgumentException($"未知命令或操作未确认：{command}");
        }
    }

    private static PackageManifest LoadManifest(string packageRoot)
    {
        var path = Path.Combine(packageRoot, "manifest.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("安装包缺少 manifest.json。", path);
        return JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("manifest.json 内容无效。");
    }

    private static bool Confirm(string message)
    {
        Console.Write($"{message} 输入 YES 继续：");
        return string.Equals(Console.ReadLine()?.Trim(), "YES", StringComparison.Ordinal);
    }

    internal static void WriteColored(ConsoleColor color, string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}

internal sealed class InstallationManager
{
    private readonly string _packageRoot;
    private readonly string _targetRoot;
    private readonly string _payloadRoot;
    private readonly string _stateDirectory;
    private readonly string _statePath;
    private readonly PackageManifest _manifest;

    public InstallationManager(string packageRoot, string targetRoot, PackageManifest manifest)
    {
        _packageRoot = Path.GetFullPath(packageRoot);
        _targetRoot = Path.GetFullPath(targetRoot);
        _payloadRoot = Path.Combine(_packageRoot, "payload");
        _stateDirectory = Path.Combine(_targetRoot, ".rhythm-editor-mod");
        _statePath = Path.Combine(_stateDirectory, "install.json");
        _manifest = manifest;
    }

    public void PrintStatus()
    {
        Console.WriteLine($"安装包版本：{_manifest.PackageVersion}");
        Console.WriteLine($"目标目录：{_targetRoot}");
        var validation = ValidateTarget(requireSupportedHash: false);
        Console.WriteLine($"编辑器：{(validation.ExecutableFound ? "已找到" : "未找到")}");
        Console.WriteLine($"GameAssembly：{(validation.HashMatches ? "受支持" : validation.GameHash ?? "未找到")}");
        var state = LoadState();
        Console.WriteLine($"已安装版本：{state?.PackageVersion ?? "未安装"}");
        Console.WriteLine($"插件状态：{GetPluginStatus()}");
    }

    public void InstallOrUpdate()
    {
        EnsureEditorStopped();
        ValidateTarget(requireSupportedHash: true);
        ValidateManifestAndPayload();

        var previous = LoadState();
        var disabled = File.Exists(TargetPath(Program.DisabledPluginDll))
            && !File.Exists(TargetPath(Program.PluginDll));
        var planned = BuildInstallPlan(previous, disabled);
        var backupRoot = Path.Combine(
            _stateDirectory,
            "backups",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        var completed = new List<InstallPlanItem>();

        try
        {
            foreach (var item in planned)
            {
                if (item.PreserveExisting || item.SkipCopy)
                    continue;
                Directory.CreateDirectory(Path.GetDirectoryName(item.Destination)!);
                if (File.Exists(item.Destination))
                {
                    var backup = Path.Combine(backupRoot, item.RelativeDestination.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(item.Destination, backup, overwrite: true);
                    item.BackupPath = backup;
                }

                var temporary = item.Destination + ".rhythmeditor.tmp";
                File.Copy(item.Source, temporary, overwrite: true);
                File.Move(temporary, item.Destination, overwrite: true);
                completed.Add(item);
            }

            var installedFiles = new List<InstalledFile>();
            foreach (var item in planned)
            {
                if ((item.PreserveExisting || item.SkipCopy) && item.PreviousEntry != null)
                {
                    installedFiles.Add(item.PreviousEntry);
                    continue;
                }
                installedFiles.Add(new InstalledFile
                {
                    Path = item.RelativeDestination,
                    Sha256 = item.ExpectedHash,
                    Role = item.Role,
                    CreatedByManager = item.PreviousEntry?.CreatedByManager ?? !item.ExistedBefore,
                });
            }

            Directory.CreateDirectory(_stateDirectory);
            SaveState(new InstallState
            {
                SchemaVersion = 1,
                PackageVersion = _manifest.PackageVersion,
                BepInExVersion = _manifest.BepInExVersion,
                ExpectedGameAssemblySha256 = _manifest.ExpectedGameAssemblySha256,
                InstalledAt = DateTimeOffset.Now,
                Files = installedFiles,
            });
            Program.WriteColored(ConsoleColor.Green, $"安装/更新完成：RhythmEditorMod {_manifest.PackageVersion}");
            Console.WriteLine($"插件状态：{GetPluginStatus()}");
        }
        catch
        {
            foreach (var item in completed.AsEnumerable().Reverse())
            {
                if (item.BackupPath != null && File.Exists(item.BackupPath))
                    File.Copy(item.BackupPath, item.Destination, overwrite: true);
                else if (!item.ExistedBefore && File.Exists(item.Destination))
                    File.Delete(item.Destination);
            }
            throw;
        }
    }

    public void Enable()
    {
        EnsureEditorStopped();
        var enabled = TargetPath(Program.PluginDll);
        var disabled = TargetPath(Program.DisabledPluginDll);
        if (File.Exists(enabled))
        {
            Console.WriteLine("插件已经启用。");
            return;
        }
        if (!File.Exists(disabled))
            throw new FileNotFoundException("没有找到已安装或已禁用的插件，请先安装。", disabled);
        File.Move(disabled, enabled);
        RenameStateEntry(Program.DisabledPluginDll, Program.PluginDll);
        Program.WriteColored(ConsoleColor.Green, "插件已启用。");
    }

    public void Disable()
    {
        EnsureEditorStopped();
        var enabled = TargetPath(Program.PluginDll);
        var disabled = TargetPath(Program.DisabledPluginDll);
        if (File.Exists(disabled))
        {
            Console.WriteLine("插件已经禁用。");
            return;
        }
        if (!File.Exists(enabled))
            throw new FileNotFoundException("没有找到已安装的插件。", enabled);
        File.Move(enabled, disabled);
        RenameStateEntry(Program.PluginDll, Program.DisabledPluginDll);
        Program.WriteColored(ConsoleColor.Green, "插件已禁用。");
    }

    public void UninstallPlugin()
    {
        EnsureEditorStopped();
        var state = LoadState() ?? throw new InvalidOperationException("没有找到管理器安装记录。");
        var pluginFiles = state.Files.Where(file => file.Role == "plugin").ToArray();
        foreach (var file in pluginFiles)
            DeleteManagedFile(file, leaveModified: false);
        state.Files.RemoveAll(file => file.Role == "plugin");
        SaveState(state);
        RemoveEmptyDirectory(Path.Combine(_targetRoot, "BepInEx", "plugins", "RhythmEditorMod"));
        Program.WriteColored(ConsoleColor.Green, "插件已卸载；BepInEx 运行环境已保留。");
    }

    public void RemoveManagedRuntime()
    {
        EnsureEditorStopped();
        var state = LoadState() ?? throw new InvalidOperationException("没有找到管理器安装记录。");
        EnsureNoOtherPlugins();

        foreach (var file in state.Files.OrderByDescending(file => file.Path.Length).ToArray())
        {
            if (!file.CreatedByManager)
                continue;
            if (DeleteManagedFile(file, leaveModified: true))
                state.Files.Remove(file);
        }
        if (state.Files.Count == 0)
            File.Delete(_statePath);
        else
            SaveState(state);
        RemoveKnownEmptyDirectories();
        Program.WriteColored(ConsoleColor.Green, "完整移除检查完成；已修改或非管理器创建的文件均已保留。");
    }

    private List<InstallPlanItem> BuildInstallPlan(InstallState? previous, bool preserveDisabledState)
    {
        var previousByPath = previous?.Files.ToDictionary(
            file => NormalizeRelative(file.Path),
            StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, InstalledFile>(StringComparer.OrdinalIgnoreCase);
        var plan = new List<InstallPlanItem>();

        foreach (var packageFile in _manifest.Files)
        {
            var sourceRelative = NormalizeRelative(packageFile.Path);
            var destinationRelative = preserveDisabledState
                && sourceRelative.Equals(Program.PluginDll, StringComparison.OrdinalIgnoreCase)
                    ? Program.DisabledPluginDll
                    : sourceRelative;
            var source = SafeCombine(_payloadRoot, sourceRelative);
            var destination = TargetPath(destinationRelative);
            previousByPath.TryGetValue(destinationRelative, out var previousEntry);
            if (previousEntry == null && destinationRelative == Program.DisabledPluginDll)
                previousByPath.TryGetValue(Program.PluginDll, out previousEntry);

            var existed = File.Exists(destination);
            var preserve = false;
            var skipCopy = false;
            if (existed)
            {
                var currentHash = HashFile(destination);
                if (currentHash.Equals(packageFile.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    skipCopy = true;
                }
                else if (packageFile.Role == "config" && previousEntry != null
                    && !currentHash.Equals(previousEntry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    preserve = true;
                    Console.WriteLine($"保留用户修改的配置：{destinationRelative}");
                }
                else if (previousEntry == null
                    && !currentHash.Equals(packageFile.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"目标中存在不受本管理器控制且内容不同的文件：{destinationRelative}。为避免覆盖，安装已停止。" );
                }
            }

            plan.Add(new InstallPlanItem
            {
                Source = source,
                Destination = destination,
                RelativeDestination = destinationRelative,
                ExpectedHash = packageFile.Sha256,
                Role = packageFile.Role,
                ExistedBefore = existed,
                PreserveExisting = preserve,
                SkipCopy = skipCopy,
                PreviousEntry = previousEntry,
            });
        }
        return plan;
    }

    private void ValidateManifestAndPayload()
    {
        if (_manifest.SchemaVersion != 1)
            throw new InvalidDataException($"不支持的安装包清单版本：{_manifest.SchemaVersion}");
        if (_manifest.Files.Count == 0)
            throw new InvalidDataException("安装包清单没有文件。" );
        foreach (var file in _manifest.Files)
        {
            var relative = NormalizeRelative(file.Path);
            var source = SafeCombine(_payloadRoot, relative);
            if (!File.Exists(source))
                throw new FileNotFoundException($"payload 缺少文件：{relative}", source);
            var actual = HashFile(source);
            if (!actual.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"payload 文件哈希不匹配：{relative}");
        }
    }

    private TargetValidation ValidateTarget(bool requireSupportedHash)
    {
        var executable = Path.Combine(_targetRoot, Program.EditorExecutable);
        var assembly = Path.Combine(_targetRoot, Program.GameAssembly);
        var executableFound = File.Exists(executable);
        var gameHash = File.Exists(assembly) ? HashFile(assembly) : null;
        var hashMatches = gameHash?.Equals(
            _manifest.ExpectedGameAssemblySha256,
            StringComparison.OrdinalIgnoreCase) == true;
        if (requireSupportedHash && !executableFound)
            throw new FileNotFoundException($"管理器上一级不是编辑器目录，未找到 {Program.EditorExecutable}。", executable);
        if (requireSupportedHash && gameHash == null)
            throw new FileNotFoundException($"目标目录缺少 {Program.GameAssembly}。", assembly);
        if (requireSupportedHash && !hashMatches)
            throw new InvalidOperationException(
                $"编辑器版本不受支持。期望 {_manifest.ExpectedGameAssemblySha256}，实际 {gameHash}。未修改任何文件。" );
        return new TargetValidation(executableFound, gameHash, hashMatches);
    }

    private void EnsureEditorStopped()
    {
        if (Process.GetProcessesByName("Rizline Editor").Length > 0)
            throw new InvalidOperationException("请先退出 Rizline Editor，再执行安装、更新或卸载。" );
    }

    private void EnsureNoOtherPlugins()
    {
        var pluginRoot = Path.Combine(_targetRoot, "BepInEx", "plugins");
        if (!Directory.Exists(pluginRoot))
            return;
        var ownRoot = Path.GetFullPath(Path.Combine(pluginRoot, "RhythmEditorMod"));
        var unknown = Directory.EnumerateFiles(pluginRoot, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFullPath(path).StartsWith(ownRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Take(1)
            .ToArray();
        if (unknown.Length > 0)
            throw new InvalidOperationException($"检测到其他 BepInEx 插件，拒绝完整移除运行环境：{unknown[0]}");
    }

    private bool DeleteManagedFile(InstalledFile file, bool leaveModified)
    {
        var path = TargetPath(file.Path);
        if (!File.Exists(path))
            return true;
        var actual = HashFile(path);
        if (!actual.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            if (leaveModified)
            {
                Console.WriteLine($"保留已修改文件：{file.Path}");
                return false;
            }
            throw new InvalidOperationException($"文件已被修改，拒绝删除：{file.Path}");
        }
        File.Delete(path);
        return true;
    }

    private void RenameStateEntry(string oldPath, string newPath)
    {
        var state = LoadState();
        if (state == null)
            return;
        var entry = state.Files.FirstOrDefault(file => file.Path.Equals(oldPath, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
            entry.Path = newPath;
        SaveState(state);
    }

    private string GetPluginStatus()
    {
        if (File.Exists(TargetPath(Program.PluginDll)))
            return "已启用";
        if (File.Exists(TargetPath(Program.DisabledPluginDll)))
            return "已禁用";
        return "未安装";
    }

    private InstallState? LoadState()
    {
        if (!File.Exists(_statePath))
            return null;
        return JsonSerializer.Deserialize<InstallState>(File.ReadAllText(_statePath), Program.JsonOptions)
            ?? throw new InvalidDataException("安装记录内容无效。" );
    }

    private void SaveState(InstallState state)
    {
        Directory.CreateDirectory(_stateDirectory);
        var temporary = _statePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, Program.JsonOptions), new UTF8Encoding(false));
        File.Move(temporary, _statePath, overwrite: true);
    }

    private string TargetPath(string relative) => SafeCombine(_targetRoot, NormalizeRelative(relative));

    private static string NormalizeRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            throw new InvalidDataException($"清单路径不是安全相对路径：{path}");
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new InvalidDataException($"清单路径包含非法片段：{path}");
        return normalized;
    }

    private static string SafeCombine(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"路径超出允许目录：{relative}");
        return full;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void RemoveEmptyDirectory(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            Directory.Delete(path);
    }

    private void RemoveKnownEmptyDirectories()
    {
        var directories = new[]
        {
            "BepInEx/plugins/RhythmEditorMod", "BepInEx/plugins", "BepInEx/config", "BepInEx/metadata",
            "BepInEx/unity-libs", "BepInEx/interop", "BepInEx/core", "BepInEx/patchers", "BepInEx", "dotnet",
        };
        foreach (var relative in directories)
            RemoveEmptyDirectory(TargetPath(relative));
    }

    private sealed record TargetValidation(bool ExecutableFound, string? GameHash, bool HashMatches);
}

internal sealed class CommandOptions
{
    public string? Command { get; private init; }
    public string TargetRoot { get; private init; } = string.Empty;
    public bool AssumeYes { get; private init; }

    public static CommandOptions Parse(string[] args, string packageRoot)
    {
        string? command = null;
        string? target = null;
        var assumeYes = false;
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].Equals("--target", StringComparison.OrdinalIgnoreCase))
            {
                if (++index >= args.Length)
                    throw new ArgumentException("--target 后必须提供目录。" );
                target = args[index];
            }
            else if (args[index].Equals("--yes", StringComparison.OrdinalIgnoreCase))
                assumeYes = true;
            else if (args[index].Equals("--debug", StringComparison.OrdinalIgnoreCase))
            {
            }
            else if (command == null)
                command = args[index];
            else
                throw new ArgumentException($"无法识别的参数：{args[index]}");
        }

        target ??= Directory.GetParent(packageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))?.FullName
            ?? throw new InvalidOperationException("无法从管理器位置确定编辑器目录。" );
        return new CommandOptions
        {
            Command = command,
            TargetRoot = Path.GetFullPath(target),
            AssumeYes = assumeYes,
        };
    }
}

internal sealed class PackageManifest
{
    public int SchemaVersion { get; set; }
    public string PackageVersion { get; set; } = string.Empty;
    public string BepInExVersion { get; set; } = string.Empty;
    public string ExpectedGameAssemblySha256 { get; set; } = string.Empty;
    public List<PackageFile> Files { get; set; } = new();
}

internal sealed class PackageFile
{
    public string Path { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

internal sealed class InstallState
{
    public int SchemaVersion { get; set; }
    public string PackageVersion { get; set; } = string.Empty;
    public string BepInExVersion { get; set; } = string.Empty;
    public string ExpectedGameAssemblySha256 { get; set; } = string.Empty;
    public DateTimeOffset InstalledAt { get; set; }
    public List<InstalledFile> Files { get; set; } = new();
}

internal sealed class InstalledFile
{
    public string Path { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool CreatedByManager { get; set; }
}

internal sealed class InstallPlanItem
{
    public string Source { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string RelativeDestination { get; init; } = string.Empty;
    public string ExpectedHash { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool ExistedBefore { get; init; }
    public bool PreserveExisting { get; init; }
    public bool SkipCopy { get; init; }
    public InstalledFile? PreviousEntry { get; init; }
    public string? BackupPath { get; set; }
}
