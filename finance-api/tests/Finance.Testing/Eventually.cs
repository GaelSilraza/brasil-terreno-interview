using System.Diagnostics;

namespace Finance.Testing;

public static class Eventually
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(200);

    public static async Task<TResult> WaitUntilAsync<TResult>(
        Func<CancellationToken, Task<TResult>> probe,
        Func<TResult, bool> isSatisfied,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var result = await probe(cancellationToken);

            if (isSatisfied(result))
            {
                return result;
            }

            if (stopwatch.Elapsed > timeout)
            {
                throw new TimeoutException($"The condition was not satisfied within {timeout.TotalSeconds} seconds. Last result: {result}.");
            }

            await Task.Delay(PollingInterval, cancellationToken);
        }
    }
}
