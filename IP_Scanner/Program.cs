using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace IP_Scanner
{
    internal class Program
    {
        record AliveHost(string Ip, string? Host, string? Mac, long Rtt, int? Ttl);

        static async Task Main()
        {
            Console.WriteLine($"Hostname: {Dns.GetHostName()}");
            Console.WriteLine();

            var primary = GetPrimaryLocalIPv4();
            Console.WriteLine(primary?.ToString() ?? "<none>");
            Console.WriteLine();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var props = nic.GetIPProperties();
                foreach (var ua in props.UnicastAddresses) if (ua.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6) Console.WriteLine($"  [{nic.Name}] {ua.Address} ({nic.NetworkInterfaceType})");

            }

            var octets = primary.GetAddressBytes();
            string baseIp = $"{octets[0]}.{octets[1]}.{octets[2]}.";
            int s = 1;
            int e = 254;

            Console.WriteLine($"\n== Scanning {baseIp}{s}-{e} ==");
            var aliveHosts = await ScanRange(baseIp, s, e);

            Console.WriteLine("\nAlive hosts:");
            foreach (var h in aliveHosts) Console.WriteLine($"  [{h.Host ?? "<no rdns>"}] {h.Ip}  mac={h.Mac ?? "??:??:??:??:??:??"}  rtt={h.Rtt}ms  ttl={h.Ttl?.ToString() ?? "?"}");

        }

        static async Task<List<AliveHost>> ScanRange(string baseIp, int start, int end)
        {
            var tasks = new List<Task<AliveHost?>>();
            var semaphore = new SemaphoreSlim(50);

            for (int i = start; i <= end; i++)
            {
                string ip = baseIp + i;

                await semaphore.WaitAsync();

                tasks.Add(Task.Run(async () => // js doom
                {
                    try
                    {
                        using (var ping = new Ping())
                        {
                            var reply = await ping.SendPingAsync(ip, 1000);
                            if (reply.Status == IPStatus.Success)
                            {
                                string? host = await TryReverseDns(ip);
                                string? mac = TryGetMac(ip);
                                return new AliveHost(ip, host, mac, reply.RoundtripTime, reply.Options?.Ttl);
                            }
                        }
                    }
                    catch { }
                    finally { semaphore.Release(); }
                    return null;
                    }
                )
              );
            }

            var results = await Task.WhenAll(tasks);

            var alive = new List<AliveHost>();
            foreach (var result in results) if (result != null) alive.Add(result);
            return alive;
        }

        static async Task<string?> TryReverseDns(string ip)
        {
            try { return (await Dns.GetHostEntryAsync(ip)).HostName; }
            catch { return null; }
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        static extern int SendARP(int destIp, int srcIp, byte[] macAddr, ref int macAddrLen);

        static string? TryGetMac(string ip)
        {
            try
            {
                int destIp = BitConverter.ToInt32(IPAddress.Parse(ip).GetAddressBytes(), 0);
                byte[] macBytes = new byte[6];
                int len = macBytes.Length;
                if (SendARP(destIp, 0, macBytes, ref len) != 0 || len == 0) return null;
                return string.Join(":", macBytes.Take(len).Select(b => b.ToString("X2")));
            }
            catch { return null; }
        }

        private static IPAddress? GetPrimaryLocalIPv4()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = (IPEndPoint?)socket.LocalEndPoint;
                return endPoint?.Address;
            }
            catch
            {
                return null;
            }
        }
    }
}
