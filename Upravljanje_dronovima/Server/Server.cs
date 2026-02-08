using Server.Modeli;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Server
{
    class Letelica
    {
        static async Task Main()
        {
            UdpClient udpServer = new UdpClient(9000);
            var letelice = new List<Letelica>();

            Console.WriteLine("Server pokrenut na portu 9000...");

            while (true)
            {
                var result = await udpServer.ReceiveAsync();
                string json = Encoding.UTF8.GetString(result.Buffer);

                Letelica l = JsonSerializer.Deserialize<Letelica>(json)!;
                letelice.Add(l);

                Console.WriteLine($"Primljena letelica: {l.Id} ({l.Tip}) | Pozicija: ({l.X},{l.Y})");

                string ack = "ACK";
                byte[] ackData = Encoding.UTF8.GetBytes(ack);
                await udpServer.SendAsync(ackData, ackData.Length, result.RemoteEndPoint);
            }
        }
    }
}
