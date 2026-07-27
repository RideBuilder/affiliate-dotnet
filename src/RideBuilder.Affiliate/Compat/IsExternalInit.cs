#if NETSTANDARD2_0
// `init`-only setters and positional records emit references to IsExternalInit, which does not exist in
// the netstandard2.0 reference assemblies. Providing it here lets the core use records/init on both TFMs.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
