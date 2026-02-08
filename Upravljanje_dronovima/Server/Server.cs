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
    class Server
    {
        static async Task Main()
        {
            UdpClient udpServer = new UdpClient(9000);
            var letelice = new List<Letelica>();

            Console.WriteLine("Server pokrenut na portu 9000");

            int sirina = 5;
            int visina = 5;
            Polje[,] teren = new Polje[sirina, visina];

            for (int i = 0; i < sirina; i++)
            {
                for (int j = 0; j < visina; j++)
                {
                    teren[i, j] = new Polje { X = i, Y = j };
                }
            }

            Console.WriteLine("Teren 5x5 inicijalizovan.");

            Random rnd = new Random();

            while (true)
            {
                foreach (var polje in teren)
                {
                    if (polje.Tip == TipPolja.Neobradjeno && rnd.Next(0, 10) == 0)
                    {
                        polje.Tip = TipPolja.Alarm;
                        Console.WriteLine($"Alarm na polju ({polje.X},{polje.Y})");
                    }
                }

                var rezultat = await udpServer.ReceiveAsync();
                string json = Encoding.UTF8.GetString(rezultat.Buffer);

                Letelica l = JsonSerializer.Deserialize<Letelica>(json)!;
                letelice.Add(l);

                Console.WriteLine($"Primljena letelica: {l.Id} ({l.Tip}) | Pozicija: ({l.X},{l.Y})");

                string ack = "ACK";
                byte[] ackData = Encoding.UTF8.GetBytes(ack);
                await udpServer.SendAsync(ackData, ackData.Length, rezultat.RemoteEndPoint);
            }
        }
    }
}
