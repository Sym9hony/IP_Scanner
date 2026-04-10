using System.Net;

namespace IP_Scanner
{
    internal class Program
    {
        static void Main(string[] args)
        {
            static void Main(string[] args)
            {
                String strHostName = string.Empty;
                IPHostEntry ipEntry = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress[] addr = ipEntry.AddressList;

                for (int i = 0; i < addr.Length; i++)
                {
                    Console.WriteLine("IP Address {0}: {1} ", i, addr[i].ToString());
                }
            }
        }
    }
}
