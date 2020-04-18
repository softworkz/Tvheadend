namespace TVHeadEnd.Setup
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using MediaBrowser.Model.Logging;

    using TVHeadEnd.Configuration;

    public class RemoteSetupHelper
    {
        private readonly ILogger logger;

        public RemoteSetupHelper(ILogger logger)
        {
            this.logger = logger;
        }

        public async Task<CheckResult> TestNetworkLocation(TvHeadendTunerConfig remoteConnection, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);

                if (IpUtils.CheckIsSingleHop(remoteConnection.TvhServerName))
                {
                    return new CheckResult(true, "OK: Host is in the same network");
                }

                return new CheckResult(false, "Failed: Host needs to reside in the local network");
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("RemoteSetupHelper: Error checking network location of {0}", ex, remoteConnection);
                return new CheckResult(false, ex.Message);
            }
        }

        public async Task<CheckResult> TestNetworkLatency(TvHeadendTunerConfig remoteConnection, CancellationToken cancellationToken)
        {
            try
            {
                var times = await IpUtils.CheckAveragePingTime(remoteConnection.TvhServerName).ConfigureAwait(false);

                var averageMs = times
                    .Select(e => e.TotalMilliseconds)
                    .Average(e => e);

                if (averageMs < 10.0)
                {
                    return new CheckResult(true, string.Format("OK: Average ping is {0:n2} ms", averageMs));
                }

                return new CheckResult(false, string.Format("Failed: Average ping time (8192 bytes payload) is {0:n2} ms", averageMs));
            }
            catch (Exception ex)
            {
                this.logger.ErrorException("RemoteSetupHelper: Error checking network latency to {0}", ex, remoteConnection);
                return new CheckResult(false, ex.Message);
            }
        }

        public class CheckResult
        {
            public CheckResult(bool success, string message)
            {
                this.Success = success;
                this.Message = message;
            }

            public bool Success { get; }

            public string Message { get; }
        }
    }
}