#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <filesystem>
#include <string>

namespace fs = std::filesystem;

static std::wstring Quote(const std::wstring& value)
{
    std::wstring result = L"\"";
    for (const auto character : value)
    {
        if (character == L'\"')
            result += L'\\';
        result += character;
    }
    result += L'\"';
    return result;
}

int wmain(int argc, wchar_t* argv[])
{
    std::wstring executablePath(32768, L'\0');
    const auto length = GetModuleFileNameW(nullptr, executablePath.data(), static_cast<DWORD>(executablePath.size()));
    if (length == 0 || length >= executablePath.size())
    {
        MessageBoxW(nullptr, L"无法确定管理器所在目录。", L"RhythmEditorMod Manager", MB_ICONERROR);
        return 1;
    }
    executablePath.resize(length);
    const fs::path packageRoot = fs::path(executablePath).parent_path();
    const fs::path runtimeRoot = packageRoot / L"manager-runtime";
    const fs::path dotnetPath = runtimeRoot / L"dotnet.exe";
    const fs::path managerDll = runtimeRoot / L"RhythmEditorMod.Manager.dll";
    if (!fs::is_regular_file(dotnetPath) || !fs::is_regular_file(managerDll))
    {
        MessageBoxW(
            nullptr,
            L"安装包不完整：缺少 manager-runtime。请重新解压完整的 RhythmEditorMod 文件夹。",
            L"RhythmEditorMod Manager",
            MB_ICONERROR);
        return 1;
    }

    std::wstring commandLine = Quote(dotnetPath.wstring()) + L" " + Quote(managerDll.wstring());
    for (int index = 1; index < argc; ++index)
        commandLine += L" " + Quote(argv[index]);
    std::wstring mutableCommand = commandLine;

    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(
            dotnetPath.c_str(),
            mutableCommand.data(),
            nullptr,
            nullptr,
            TRUE,
            0,
            nullptr,
            packageRoot.c_str(),
            &startup,
            &process))
    {
        const auto error = GetLastError();
        MessageBoxW(
            nullptr,
            (L"无法启动管理器运行环境。Windows 错误码：" + std::to_wstring(error)).c_str(),
            L"RhythmEditorMod Manager",
            MB_ICONERROR);
        return static_cast<int>(error);
    }

    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD exitCode = 1;
    GetExitCodeProcess(process.hProcess, &exitCode);
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return static_cast<int>(exitCode);
}
