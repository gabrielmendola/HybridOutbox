#if NETSTANDARD2_0
// Enables C# 9 `init` accessors when targeting netstandard2.0.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif