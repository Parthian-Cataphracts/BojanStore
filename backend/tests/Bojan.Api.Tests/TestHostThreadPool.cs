using System.Runtime.CompilerServices;

namespace Bojan.Api.Tests;

/// <summary>
/// Gives the thread pool enough threads to start this many web hosts at once.
/// </summary>
/// <remarks>
/// <para>
/// <c>WebApplicationFactory</c> starts its host <i>synchronously</i>: reading
/// <c>Services</c> calls <c>StartServer()</c>, which blocks the calling thread on
/// a Task until the host is up. Every test class here builds a factory, xUnit
/// runs several classes at once, and so a dozen or more pool threads sit blocked
/// on hosts that themselves need pool threads to finish booting.
/// </para>
/// <para>
/// The pool's answer to that is to inject about one thread per second, which is
/// far slower than tests arrive. The suite therefore ran fine for twenty minutes
/// and then fell off a cliff: requests to an in-memory server started taking
/// longer than <c>HttpClient</c>'s hundred-second timeout, and two dozen tests
/// across unrelated classes failed with <c>TaskCanceledException</c> while the
/// machine sat at eighty percent idle. Nothing was broken and nothing was slow —
/// everything was queued behind a pool that was still growing.
/// </para>
/// <para>
/// Raising the floor removes the growth delay entirely. It costs a little memory
/// in a process that exists for one test run, and it is the standard remedy for
/// a suite that hosts many servers in-process. Only ever raises: a machine whose
/// defaults are already higher keeps them.
/// </para>
/// </remarks>
internal static class TestHostThreadPool
{
    /// <summary>
    /// Comfortably above the number of hosts that can be starting at once —
    /// <c>maxParallelThreads</c> in <c>xunit.runner.json</c> bounds that, and
    /// each starting host wants several threads rather than one.
    /// </summary>
    private const int MinimumThreads = 200;

    [ModuleInitializer]
    internal static void Configure()
    {
        ThreadPool.GetMinThreads(out var workers, out var completionPorts);

        ThreadPool.SetMinThreads(
            Math.Max(workers, MinimumThreads),
            Math.Max(completionPorts, MinimumThreads));
    }
}
