#if !NET5_0_OR_GREATER
// `init`-only setters and positional records emit references to IsExternalInit, which does not exist in the
// netstandard2.0 or .NET Framework reference assemblies. Providing it here lets the core use records/init
// on every target.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
