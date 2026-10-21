using System.Reflection;

namespace Mundialito.Api;

/// <summary>
/// Marker used to locate this assembly (handler/validator scanning, architecture tests).
/// </summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
