using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace RhythmEditorMod;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "local.rhythm-editor.speed-editing";
    public const string Name = "Rhythm Editor Canvas Editing";
    public const string Version = "0.4.0";
    public const string ExpectedGameAssemblySha256 = "F6CCB82225839ABC8155C2A1241B4ACAF22E4A35BDCDF49B64B2AA550D683876";

    internal static ManualLogSource LogSource { get; private set; } = null!;

    public override void Load()
    {
        LogSource = Log;
        var gameAssemblyPath = Path.Combine(Paths.GameRootPath, "GameAssembly.dll");
        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(gameAssemblyPath)));
        if (!string.Equals(actualHash, ExpectedGameAssemblySha256, StringComparison.OrdinalIgnoreCase))
        {
            Log.LogError($"Unsupported GameAssembly.dll. Expected {ExpectedGameAssemblySha256}, got {actualHash}. No patches were applied.");
            return;
        }

        Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, Guid);
        NoteSpeedLimit.Prepare();
        ProjectSlots.Load();
        PluginLayout.Load();
        AddComponent<SpeedEasePanel>();
        Log.LogInfo("Loaded. Press F8 to show or hide the canvas-editing panel.");
    }
}
