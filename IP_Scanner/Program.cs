using System.Net.NetworkInformation;

namespace IP_Scanner
{
    internal class Program
    {
        static async Task Main()
        {
            string baseIp = "172.17.8.";
            int start = 1;
            int end = 254;

            var aliveHosts = await ScanRange(baseIp, start, end);

            Console.WriteLine("\nAlive hosts:");
            foreach (var ip in aliveHosts)
            {
                Console.WriteLine(ip);
            }
        }

        static async Task<List<string>> ScanRange(string baseIp, int start, int end)
        {
            var tasks = new List<Task<string>>();
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
    }
}
