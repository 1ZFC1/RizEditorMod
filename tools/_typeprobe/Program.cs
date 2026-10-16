using System.Reflection;
using System.Runtime.Loader;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var interop = Path.Combine(root, "BepInEx", "interop");
var core = Path.Combine(root, "BepInEx", "core");
var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var resolver = new PathAssemblyResolver(
    Directory.GetFiles(interop, "*.dll")
        .Concat(Directory.GetFiles(core, "*.dll"))
        .Concat(Directory.GetFiles(runtime, "*.dll")));
using var mlc = new MetadataLoadContext(resolver);
var asm = mlc.LoadFromAssemblyPath(Path.Combine(interop, "Assembly-CSharp.dll"));

const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

Type? Find(string typeName) =>
    asm.GetType(typeName) ?? asm.GetTypes().FirstOrDefault(t => t.Name == typeName);

void Dump(string typeName, params string[] members)
{
    var type = Find(typeName);
    Console.WriteLine($"== {typeName} {type?.FullName} ==");
    if (type == null)
        return;
    foreach (var name in members)
    {
        var prop = type.GetProperty(name, Any);
        var field = type.GetField(name, Any);
        if (prop != null)
            Console.WriteLine($"  prop {name}: {prop.PropertyType.FullName}");
        else if (field != null)
            Console.WriteLine($"  field {name}: {field.FieldType.FullName}");
        else
            Console.WriteLine($"  missing {name}");
    }
}

// Every declared member, for types whose full shape matters.
void DumpAll(string typeName)
{
    var type = Find(typeName);
    Console.WriteLine($"== {typeName} {type?.FullName} : all declared members ==");
    if (type == null)
        return;
    foreach (var field in type.GetFields(Any).OrderBy(f => f.Name, StringComparer.Ordinal))
        Console.WriteLine($"  field {field.Name}: {field.FieldType.FullName}");
    foreach (var prop in type.GetProperties(Any).OrderBy(p => p.Name, StringComparer.Ordinal))
        Console.WriteLine($"  prop  {prop.Name}: {prop.PropertyType.FullName}{(prop.CanWrite ? " {get;set;}" : " {get;}")}");
}

// The interop proxy surface: which native methods are callable from the plugin.
void DumpMethods(string typeName)
{
    var type = Find(typeName);
    Console.WriteLine($"== {typeName} {type?.FullName} : callable methods ==");
    if (type == null)
        return;
    foreach (var method in type.GetMethods(Any).OrderBy(m => m.Name, StringComparer.Ordinal))
    {
        if (method.Name.StartsWith("NativeMethodInfoPtr_", StringComparison.Ordinal)
            || method.Name.StartsWith("NativeFieldInfoPtr_", StringComparison.Ordinal))
            continue;
        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        Console.WriteLine($"  {method.ReturnType.Name} {method.Name}({parameters})");
    }
}

Dump("ProjectInformation", "colorPanels", "OnColorPanelsChanged", "linesGroups", "isPlaying", "isPlayingWithoutPreview", "instance");
Dump("LineInformation", "lineRenderer");
Dump("LineEditor", "previewLine");
Dump("LinesGroupInformation", "lines");
Dump("ColorSelectorControl", "colorPresets", "SyncColorPanels");

Console.WriteLine();
DumpAll("ColorKeyPoint");
Console.WriteLine();
DumpAll("LinePoint");
Console.WriteLine();
DumpAll("LineEditor");
Console.WriteLine();
DumpAll("_SelectLines_d__35");
Console.WriteLine();
DumpAll("ColorEditor");
Console.WriteLine();
DumpAll("InputControl");
Console.WriteLine();
DumpAll("ToolbarToolButton");
Console.WriteLine();
DumpAll("PublishControl");
Console.WriteLine();
DumpAll("ToolbarBodyHolder");
Console.WriteLine();
DumpMethods("ToolbarToolButton");
Console.WriteLine();
DumpMethods("ToolbarBodyHolder");

// How a managed handler can be attached to an Il2Cpp UnityEvent (onClick), if the clone's
// own Start() turns out not to wire it.
var runtimeAssembly = mlc.LoadFromAssemblyPath(Path.Combine(core, "Il2CppInterop.Runtime.dll"));
var delegateSupport = runtimeAssembly.GetType("Il2CppInterop.Runtime.DelegateSupport");
Console.WriteLine();
Console.WriteLine($"== DelegateSupport {delegateSupport?.FullName} ==");
if (delegateSupport != null)
{
    foreach (var method in delegateSupport.GetMethods(Any).OrderBy(m => m.Name, StringComparer.Ordinal))
    {
        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        Console.WriteLine($"  {(method.IsStatic ? "static " : string.Empty)}{method.ReturnType.Name} {method.Name}({parameters})");
    }
}

// Component lookup surface: GetComponentInChildren<T>(bool) throws VerificationException at
// runtime, so the alternatives matter.
void DumpTypeMethods(Type? type, string label, string filter)
{
    Console.WriteLine();
    Console.WriteLine($"== {label} {type?.FullName} : methods matching '{filter}' ==");
    if (type == null)
        return;
    foreach (var method in type.GetMethods(Any)
                 .Where(m => m.Name.Contains(filter, StringComparison.Ordinal))
                 .OrderBy(m => m.Name, StringComparer.Ordinal))
    {
        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        Console.WriteLine($"  {method.ReturnType.Name} {method.Name}({parameters})");
    }
}

var coreModule = mlc.LoadFromAssemblyPath(Path.Combine(interop, "UnityEngine.CoreModule.dll"));
DumpTypeMethods(coreModule.GetType("UnityEngine.Component"), "Component", "GetComponent");
DumpTypeMethods(
    runtimeAssembly.GetType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase"),
    "Il2CppObjectBase",
    "Cast");

var tmpAssembly = mlc.LoadFromAssemblyPath(Path.Combine(interop, "Unity.TextMeshPro.dll"));
var tmpGui = tmpAssembly.GetType("TMPro.TextMeshProUGUI");
Console.WriteLine();
Console.WriteLine($"== TextMeshProUGUI {tmpGui?.FullName} : text members ==");
if (tmpGui != null)
{
    var textProp = tmpGui.GetProperty("text", Any);
    Console.WriteLine($"  prop text: {textProp?.PropertyType.FullName} CanWrite={textProp?.CanWrite}");
}

// Easing investigation: which types exist, and what the ease evaluator exposes.
void ListTypes(string pattern)
{
    Console.WriteLine();
    Console.WriteLine($"== Assembly-CSharp types whose name contains '{pattern}' ==");
    foreach (var type in asm.GetTypes()
                 .Where(t => t.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                 .OrderBy(t => t.Name, StringComparer.Ordinal))
    {
        Console.WriteLine($"  {type.FullName}");
    }
}

ListTypes("Ease");
ListTypes("EaseBar");
Console.WriteLine();
DumpAll("GetEase");
Console.WriteLine();
DumpMethods("GetEase");

// Any member anywhere whose name mentions ease: that is where the UI keeps the choice.
void SearchMembers(string namePart, string? typePart = null)
{
    Console.WriteLine();
    Console.WriteLine($"== members whose name contains '{namePart}'" +
        $"{(typePart == null ? string.Empty : $" (type name contains '{typePart}')")} ==");
    foreach (var type in asm.GetTypes().OrderBy(t => t.Name, StringComparer.Ordinal))
    {
        if (typePart != null && !type.Name.Contains(typePart, StringComparison.OrdinalIgnoreCase))
            continue;

        foreach (var field in type.GetFields(Any).OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (field.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"  {type.Name}.{field.Name}: {field.FieldType.Name}");
        }

        foreach (var prop in type.GetProperties(Any).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (prop.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"  {type.Name}.{prop.Name}: {prop.PropertyType.Name}");
        }

        foreach (var method in type.GetMethods(Any).OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            if (method.Name.StartsWith("Native", StringComparison.Ordinal))
                continue;
            if (method.Name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                Console.WriteLine($"  {type.Name}.{method.Name}({parameters}) -> {method.ReturnType.Name}");
            }
        }
    }
}

SearchMembers("ease");

// The 19 names live in a string table somewhere; find string-array members.
void SearchFieldType(string typeNamePart)
{
    Console.WriteLine();
    Console.WriteLine($"== fields/props whose type name contains '{typeNamePart}' ==");
    foreach (var type in asm.GetTypes().OrderBy(t => t.Name, StringComparer.Ordinal))
    {
        foreach (var field in type.GetFields(Any).OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (field.FieldType.Name.Contains(typeNamePart, StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"  field {type.Name}.{field.Name}: {field.FieldType.Name}");
        }

        foreach (var prop in type.GetProperties(Any).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (prop.PropertyType.Name.Contains(typeNamePart, StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"  prop  {type.Name}.{prop.Name}: {prop.PropertyType.Name}");
        }
    }
}

SearchFieldType("StringArray");
SearchFieldType("Single[]");

Console.WriteLine();
DumpAll("KeyPointEaseMark");
Console.WriteLine();
DumpMethods("KeyPointEaseMark");
Console.WriteLine();
DumpAll("StatusInfoProvider");
Console.WriteLine();
DumpAll("CanvasEditor");
Console.WriteLine();
DumpMethods("CanvasEditor");
Console.WriteLine();
DumpAll("CanvasKeyPointInformation");
Console.WriteLine();
DumpAll("CanvasMove");
Console.WriteLine();
DumpMethods("CanvasMove");
Console.WriteLine();
DumpAll("EditorInput");
Console.WriteLine();
DumpMethods("EditorInput");
Console.WriteLine();
DumpMethods("InputControl");
Console.WriteLine();
DumpAll("LineInformation");Console.WriteLine();
DumpMethods("LineInformation");
Console.WriteLine();
DumpAll("LinesGroupInformation");
Console.WriteLine();
DumpMethods("LinesGroupInformation");
Console.WriteLine();
DumpAll("Line");
Console.WriteLine();
DumpMethods("Line");
Console.WriteLine();
DumpAll("ProjectInformation");
Console.WriteLine();
DumpMethods("ProjectInformation");
Console.WriteLine();
DumpAll("FileControl");
Console.WriteLine();
DumpMethods("FileControl");
Console.WriteLine();
DumpAll("CameraEditor");
Console.WriteLine();
DumpMethods("CameraEditor");
Console.WriteLine();
DumpAll("LineControl");
Console.WriteLine();
DumpMethods("LineControl");
Console.WriteLine();
DumpAll("CameraMove");
Console.WriteLine();
DumpMethods("CameraMove");
Console.WriteLine();
DumpAll("DataManagerController");
Console.WriteLine();
DumpMethods("DataManagerController");
