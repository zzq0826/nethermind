using System;
using System.Runtime.InteropServices;

namespace Nethermind.Core.Extensions;

public static class MemoryExtensions
{
    /// <summary>
    /// Try to get the underlying array if possible to prevent copying.
    /// </summary>
    /// <param name="memory"></param>
    /// <returns></returns>
    public static byte[]? AsArray(this in Memory<byte>? memory) => memory?.AsArray();

    public static byte[] AsArray(this in Memory<byte> memory) =>
        MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length
            ? segment.Array
            : memory.Span.ToArray();
}
