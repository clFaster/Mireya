using Avalonia.Headless;

namespace Mireya.Client.Core.Tests;

/// <summary>
///     Shares a single headless Avalonia session between the tests of a class. Avalonia can
///     only be initialized once per process and owns its own UI thread, so test bodies that
///     touch Avalonia types have to be dispatched onto that thread.
/// </summary>
public sealed class HeadlessSessionFixture : IDisposable
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(
        typeof(TestAppBuilder)
    );

    /// <summary>Runs the test body on the Avalonia UI thread of the headless session.</summary>
    public Task RunAsync(Action testBody)
    {
        Func<Task> dispatched = () =>
        {
            testBody();
            return Task.CompletedTask;
        };

        return _session.Dispatch(dispatched, CancellationToken.None);
    }

    public void Dispose() => _session.Dispose();
}
