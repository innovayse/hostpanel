using System.Runtime.InteropServices;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Hands the native memory the process no longer uses back to the operating system. Starting a host makes
/// Wolverine compile its handler code with Roslyn, which allocates and then frees hundreds of megabytes of
/// native memory for the metadata of every loaded assembly. glibc keeps those freed chunks inside its heap,
/// where the trim threshold cannot reach them (it only gives back free space at the top), so the resident
/// size of the test process grew by about 36 MB per released host for the whole run (issue 793: a shard was
/// killed at 6 GiB with a managed heap of a fraction of that). <c>malloc_trim</c> walks the whole heap and
/// returns every free page: the same runs stay level at about 800 MB.
/// </summary>
internal static class NativeHeap
{
    /// <summary>
    /// Returns the free pages of the C library's heap to the operating system. Does nothing where the
    /// function does not exist (a platform other than Linux with glibc), because trimming is only an
    /// optimisation of the resident size.
    /// </summary>
    public static void Trim()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        try
        {
            MallocTrim(0);
        }
        catch (DllNotFoundException)
        {
            // No glibc to ask (musl or a bare image): nothing to trim.
        }
        catch (EntryPointNotFoundException)
        {
            // A C library without malloc_trim: nothing to trim.
        }
    }

#pragma warning disable SYSLIB1054 // A source-generated import needs unsafe code, which this project does not allow.
    /// <summary>The C library's <c>malloc_trim</c>.</summary>
    /// <param name="pad">Free space to leave at the top of the heap, in bytes.</param>
    /// <returns>1 when memory was released, 0 when none could be.</returns>
    [DllImport("libc.so.6", EntryPoint = "malloc_trim")]
    private static extern int MallocTrim(nuint pad);
#pragma warning restore SYSLIB1054
}
