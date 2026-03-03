// Polyfill for netstandard2.0: the compiler requires this type for 'init' accessors and 'record' types.
// It is built into .NET 5+ but must be defined manually for older TFMs.

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit;
}