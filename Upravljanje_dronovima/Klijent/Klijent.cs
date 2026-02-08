using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Klijent.Modeli;

namespace Klijent
{
    class Klijent
    {
        static async Task Main()
        {
            UdpClient udpClient = new UdpClient();

            Letelica l = new Letelica
            {
                Id = Guid.NewGuid(),
                Tip = TipLetelice.Izvrsna,
                X = 0,
                Y = 0,
                Status = StatusLetelice.Slobodna
            };

            string json = JsonSerializer.Serialize(l);
            byte[] data = Encoding.UTF8.GetBytes(json);

            // Šaljemo serveru
            await udpClient.SendAsync(data, data.Length, "127.0.0.1", 9000);

            // Čekamo potvrdu
            var response = await udpClient.ReceiveAsync();
            Console.WriteLine("Server: " + Encoding.UTF8.GetString(response.Buffer));
        }
    }
}