using Klijent.Modeli;
using Server.Modeli;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
namespace Klijent
{
    class Klijent
    {
        static async Task Main()
        {
            UdpClient udpClient = new UdpClient();
            var serverEndpoint = new IPEndPoint(IPAddress.Loopback, 9000);


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
           /* await udpClient.SendAsync(data, data.Length, serverEndpoint);*/

            // Čekamo potvrdu
            var response = await udpClient.ReceiveAsync();
            Console.WriteLine("Server: " + Encoding.UTF8.GetString(response.Buffer));

            Console.WriteLine("Client spreman za zadatke...");

            while (true)
            {
                try
                {
                    var zadatakMsg = await udpClient.ReceiveAsync();
                    string zadatakJson = Encoding.UTF8.GetString(zadatakMsg.Buffer);

                    Zadatak? zadatak = JsonSerializer.Deserialize<Zadatak>(zadatakJson);

                    if (zadatak != null)
                    {
                        Console.WriteLine($"Primljen zadatak: {zadatak.Tip} na polju ({zadatak.X},{zadatak.Y})");

                        l.Status = StatusLetelice.Zauzeta;
                        await Task.Delay(2000); 
                        zadatak.Status = StatusZadatka.Zavrsen;
                        l.X = zadatak.X;
                        l.Y = zadatak.Y;
                        l.Status = StatusLetelice.Slobodna;

                        string jsonZadatak = JsonSerializer.Serialize(zadatak);
                        byte[] bytes = Encoding.UTF8.GetBytes(jsonZadatak);
                        await udpClient.SendAsync(bytes, bytes.Length, serverEndpoint);

                        Console.WriteLine($"Zadatak ({zadatak.Tip}) završen i poslat serveru");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Greška u prijemu zadatka: " + ex.Message);
                }
            }
        }
    }
}