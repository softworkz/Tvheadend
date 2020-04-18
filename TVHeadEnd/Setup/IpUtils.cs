namespace TVHeadEnd.Setup
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;

    public static class IpUtils
    {
        public static IEnumerable<IPAddress> GetTraceRoute(string hostname)
        {
            // following are the defaults for the "traceroute" command in unix.
            const int Timeout = 10000;
            const int MaxTtl = 30;
            const int BufferSize = 32;

            byte[] buffer = new byte[BufferSize];
            new Random().NextBytes(buffer);
            Ping pinger = new Ping();

            for (int ttl = 1; ttl <= MaxTtl; ttl++)
            {
                PingOptions options = new PingOptions(ttl, true);
                PingReply reply = pinger.Send(hostname, Timeout, buffer, options);

                if (reply?.Status == IPStatus.TtlExpired)
                {
                    // TtlExpired means we've found an address, but there are more addresses
                    yield return reply.Address;
                    continue;
                }

                if (reply?.Status == IPStatus.TimedOut)
                {
                    // TimedOut means this ttl is no good, we should continue searching
                    continue;
                }

                if (reply?.Status == IPStatus.Success)
                {
                    // Success means the tracert has completed
                    yield return reply.Address;
                }

                // if we ever reach here, we're finished, so break
                break;
            }
        }

        public static bool CheckIsSingleHop(string hostname)
        {
            const int Timeout = 10000;
            const int MaxTtl = 3;
            const int BufferSize = 32;

            byte[] buffer = new byte[BufferSize];
            new Random().NextBytes(buffer);
            Ping pinger = new Ping();

            for (int ttl = 1; ttl <= MaxTtl; ttl++)
            {
                PingOptions options = new PingOptions(ttl, true);
                PingReply reply = pinger.Send(hostname, Timeout, buffer, options);

                if (reply?.Status == IPStatus.TtlExpired)
                {
                    // TtlExpired means we've found an address, but there are more addresses
                    return false;
                }

                if (reply?.Status == IPStatus.TimedOut)
                {
                    // TimedOut means this ttl is no good, we should continue searching
                    continue;
                }

                if (reply?.Status == IPStatus.Success)
                {
                    // Success means the tracert has completed
                    return true;
                }

                // if we ever reach here, we're finished, so break
                throw new ApplicationException(string.Format("Network error: {0}", reply?.Status.ToString()));
            }

            return false;
        }

        public static async Task<IList<TimeSpan>> CheckAveragePingTime(string hostname)
        {
            const int Timeout = 5_000;
            const int MaxTtl = 3;
            const int BufferSize = 65_500;

            byte[] buffer = new byte[BufferSize];
            new Random().NextBytes(buffer);
            Ping pinger = new Ping();

            PingOptions options = new PingOptions(MaxTtl, false);

            var list = new List<TimeSpan>();

            for (int i = 0; i < 3; i++)
            {
                var reply = await pinger.SendPingAsync(hostname, Timeout, buffer, options).ConfigureAwait(false);
                if (reply.Status == IPStatus.Success)
                {
                    list.Add(TimeSpan.FromMilliseconds(reply.RoundtripTime));
                }
                else
                {
                    throw new ApplicationException(string.Format("Ping error: {0}", reply.Status.ToString()));
                }
            }

            return list;
        }
    }
}