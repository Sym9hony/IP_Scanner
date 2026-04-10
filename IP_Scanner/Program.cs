using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IP_Scanner
{
    internal class Program
    {
        static async Task Main()
        {
            Console.WriteLine($"Hostname: {Dns.GetHostName()}");
            Console.WriteLine();

            Console.WriteLine("== Primary outbound IPv4 (preferred) ==");
            var primary = GetPrimaryLocalIPv4();
            Console.WriteLine(primary?.ToString() ?? "<none>");
            Console.WriteLine();

            Console.WriteLine("== All active interfaces ==");
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var props = nic.GetIPProperties();
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                    {
                        Console.WriteLine($"  [{nic.Name}] {ua.Address} ({nic.NetworkInterfaceType})");
                    }
                }
            }

            if (primary is null || primary.AddressFamily != AddressFamily.InterNetwork)
            {
                Console.WriteLine("\nNo IPv4 primary address — skipping subnet scan.");
                return;
            }

            // Derive /24 base from the primary IPv4 (e.g. 172.17.8.73 -> "172.17.8.")
            var octets = primary.GetAddressBytes();
            string baseIp = $"{octets[0]}.{octets[1]}.{octets[2]}.";
            int start = 1;
            int end = 254;

            Console.WriteLine($"\n== Scanning {baseIp}{start}-{end} ==");
            var aliveHosts = await ScanRange(baseIp, start, end);

            Console.WriteLine("\nAlive hosts:");
            foreach (var ip in aliveHosts)
            {
                Console.WriteLine(ip);
            }
        }

        static async Task<List<string>> ScanRange(string baseIp, int start, int end)
        {
            var tasks = new List<Task<string?>>();
            var semaphore = new SemaphoreSlim(50); // limit concurrency

            for (int i = start; i <= end; i++)
            {
                string ip = baseIp + i;

                await semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using (var ping = new Ping())
                        {
                            var reply = await ping.SendPingAsync(ip, 1000);

                            if (reply.Status == IPStatus.Success)
                            {
                                return ip;
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        semaphore.Release();
                    }

                    return null;
                }));
            }

            var results = await Task.WhenAll(tasks);

            var alive = new List<string>();
            foreach (var result in results)
            {
                if (result != null)
                    alive.Add(result);
            }

            return alive;
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
