namespace Mundialito.UnitTests.Application.Messaging;

/// <summary>
/// The dispatcher and behaviors are internal to Application on purpose (the API only sees ISender).
/// Tests reach them by name instead of widening their visibility.
/// </summary>
internal static class DispatcherAccessor
{
    public static readonly Type DispatcherType =
        Find("Mundialito.Application.Abstractions.Messaging.Dispatcher");

    public static readonly Type LoggingBehaviorType =
        Find("Mundialito.Application.Abstractions.Behaviors.LoggingBehavior`2");

    public static readonly Type ValidationBehaviorType =
        Find("Mundialito.Application.Abstractions.Behaviors.ValidationBehavior`2");

    public static readonly Type TracingBehaviorType =
        Find("Mundialito.Application.Abstractions.Behaviors.TracingBehavior`2");

    private static Type Find(string fullName) =>
        global::Mundialito.Application.AssemblyReference.Assembly.GetType(fullName, throwOnError: true)!;
}
