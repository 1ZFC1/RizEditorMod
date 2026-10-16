using System.Runtime.InteropServices;

namespace RhythmEditorMod;

/// <summary>
/// Ctrl+wheel note speed is ProjectInformation.RelativeSpeed, clamped to [-9, 18].
/// Negative values display as 1 + relative/10, so -9 is the 0.1 floor.
/// -15 still keeps the coupled editor zoom positive.
/// Extension is only applied while「突破缩放极限」is on.
/// </summary>
internal static class NoteSpeedLimit
{
    private const int CompareOffset = 0x32;
    private const int AssignOffset = 0x42;
    private const byte OriginalFloor = 0xF7;
    private const byte ExtendedFloor = 0xF1;

    private static IntPtr _method;
    private static bool _extended;

    public static bool IsExtended => _extended;
    public static bool IsReady => _method != IntPtr.Zero;

    public static void Prepare()
    {
        try
        {
            var field = typeof(ProjectInformation).GetField(
                "NativeMethodInfoPtr_set_RelativeSpeed_Public_set_Void_Int32_0",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(null) is not IntPtr methodInfo || methodInfo == IntPtr.Zero)
            {
                Plugin.LogSource.LogWarning("未准备音符流速下限：找不到 set_RelativeSpeed。");
                return;
            }

            _method = ResolveFunction(methodInfo);
            if (_method == IntPtr.Zero)
            {
                Plugin.LogSource.LogWarning("未准备音符流速下限：set_RelativeSpeed 的限制字节和当前版本不一致。");
                return;
            }

            _extended = Marshal.ReadByte(_method, CompareOffset) == ExtendedFloor;
            Plugin.LogSource.LogInfo("音符流速下限补丁已就绪。打开「突破缩放极限」后才会放宽到 -0.5。");
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"未准备音符流速下限：{error.Message}");
        }
    }

    public static bool SetExtended(bool enabled)
    {
        if (_method == IntPtr.Zero)
            return false;
        if (_extended == enabled)
            return true;

        try
        {
            if (!VirtualProtect(_method, new UIntPtr(0x50), 0x40, out var previous))
            {
                Plugin.LogSource.LogWarning("无法修改 set_RelativeSpeed 的内存保护。");
                return false;
            }

            var floor = enabled ? ExtendedFloor : OriginalFloor;
            Marshal.WriteByte(_method, CompareOffset, floor);
            Marshal.WriteByte(_method, AssignOffset, floor);
            VirtualProtect(_method, new UIntPtr(0x50), previous, out _);
            FlushInstructionCache(GetCurrentProcess(), _method, new UIntPtr(0x50));
            _extended = enabled;
            if (!enabled)
                ClampRelativeSpeedToStockFloor();
            return true;
        }
        catch (Exception error)
        {
            Plugin.LogSource.LogWarning($"切换音符流速下限失败：{error.Message}");
            return false;
        }
    }

    private static void ClampRelativeSpeedToStockFloor()
    {
        try
        {
            var project = ProjectInformation.instance;
            if (project != null && project.RelativeSpeed < -9)
                project.RelativeSpeed = -9;
        }
        catch (Exception)
        {
            // Project may not be ready yet.
        }
    }

    private static IntPtr ResolveFunction(IntPtr methodInfo)
    {
        if (LooksLikeRelativeSpeedSetter(methodInfo))
            return methodInfo;

        foreach (var offset in new[] { 0, 8 })
        {
            IntPtr pointer;
            try
            {
                pointer = Marshal.ReadIntPtr(methodInfo, offset);
            }
            catch
            {
                continue;
            }

            if (LooksLikeRelativeSpeedSetter(pointer))
                return pointer;
        }

        return IntPtr.Zero;
    }

    private static bool LooksLikeRelativeSpeedSetter(IntPtr method)
    {
        if (method == IntPtr.Zero)
            return false;

        try
        {
            var floor = Marshal.ReadByte(method, CompareOffset);
            return Marshal.ReadByte(method, 0) == 0x48
                && Marshal.ReadByte(method, 1) == 0x89
                && Marshal.ReadByte(method, 2) == 0x5C
                && Marshal.ReadByte(method, 0x30) == 0x83
                && Marshal.ReadByte(method, 0x31) == 0xFB
                && (floor == OriginalFloor || floor == ExtendedFloor)
                && Marshal.ReadByte(method, 0x41) == 0xBB
                && Marshal.ReadByte(method, AssignOffset) == floor;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
}
