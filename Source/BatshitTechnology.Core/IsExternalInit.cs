namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler support type that netstandard2.0 lacks. C# 9 records and
/// <c>init</c> accessors reference it; declaring it here lets
/// <see cref="BatshitTechnology.Core.WeightedOption{T}"/> compile for the
/// framework RimWorld loads.
/// </summary>
internal static class IsExternalInit
{
}
