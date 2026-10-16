using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace RhythmEditorMod;

internal static class SystemClipboard
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    public static void SetText(string text)
    {
        Exception? win32Error = null;
        try
        {
            SetTextWin32(text);
            return;
        }
        catch (Exception error)
        {
            win32Error = error;
        }

        try
        {
            GUIUtility.systemCopyBuffer = text;
        }
        catch (Exception guiError)
        {
            throw new InvalidOperationException($"{win32Error.Message}（{guiError.Message}）");
        }
    }

    public static string GetText()
    {
        Exception? win32Error = null;
        try
        {
            return GetTextWin32();
        }
        catch (Exception error)
        {
            win32Error = error;
        }

        try
        {
            return GUIUtility.systemCopyBuffer ?? string.Empty;
        }
        catch (Exception guiError)
        {
            throw new InvalidOperationException($"{win32Error.Message}（{guiError.Message}）");
        }
    }

    private static void SetTextWin32(string text)
    {
        OpenClipboardOrThrow();
        try
        {
            if (!EmptyClipboard())
                throw new InvalidOperationException("无法清空系统剪切板。");

            var bytes = Encoding.Unicode.GetBytes(text + "\0");
            var handle = GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException("无法分配剪切板内存。");

            try
            {
                var locked = GlobalLock(handle);
                if (locked == IntPtr.Zero)
                    throw new InvalidOperationException("无法锁定剪切板内存。");
                try
                {
                    Marshal.Copy(bytes, 0, locked, bytes.Length);
                }
                finally
                {
                    GlobalUnlock(handle);
                }

                if (SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
                    throw new InvalidOperationException("无法写入系统剪切板。");
                handle = IntPtr.Zero;
            }
            finally
            {
                if (handle != IntPtr.Zero)
                    GlobalFree(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static string GetTextWin32()
    {
        OpenClipboardOrThrow();
        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
                return string.Empty;

            var locked = GlobalLock(handle);
            if (locked == IntPtr.Zero)
                throw new InvalidOperationException("无法读取系统剪切板。");
            try
            {
                return Marshal.PtrToStringUni(locked) ?? string.Empty;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void OpenClipboardOrThrow()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
                return;
            Thread.Sleep(20);
        }

        throw new InvalidOperationException("无法打开系统剪切板。");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);
}
