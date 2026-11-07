using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Server;

if (args.Length != 2)
{
    return 2;
}

var failRecovery = string.Equals(args[0], "failure", StringComparison.Ordinal);
var rejectRecovery = string.Equals(args[0], "rejected", StringComparison.Ordinal);
if (string.Equals(args[0], "hang", StringComparison.Ordinal))
{
    await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
}

try
{
    await MulletaFlix.Server.Program.RunAfterStartupRecoveryAsync(
        () => failRecovery
            ? Task.FromException<bool>(new InvalidOperationException("recovery failed"))
            : Task.FromResult(!rejectRecovery),
        () =>
        {
            File.WriteAllText(args[1], "initialized");
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
