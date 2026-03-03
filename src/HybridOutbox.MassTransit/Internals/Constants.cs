namespace HybridOutbox.MassTransit;

internal static class Constants
{
    public const string DispatcherKind = "MassTransit";

    public static class ContextKeys
    {
        public const string IsPublish = "IsPublish";
    }
}
