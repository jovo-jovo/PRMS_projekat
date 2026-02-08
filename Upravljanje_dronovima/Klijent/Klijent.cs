using Modeli;
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
            UdpClient udpClient = new UdpClient(0);
            var serverEndpoint = new IPEndPoint(IPAddress.Loopback, 9000);

            Letelica l = new Letelica
            {
                Id = Guid.NewGuid(),
                Tip = TipLetelice.Izvrsna,
                X = 0,
                Y = 0,
                Status = StatusLetelice.Slobodna
            };

            // Registracija letelice
            string json = JsonSerializer.Serialize(l);
            byte[] data = Encoding.UTF8.GetBytes(json);
            await udpClient.SendAsync(data, data.Length, serverEndpoint);

            // Ceka potvrdu (ACK)
            var response = await udpClient.ReceiveAsync();
            Console.WriteLine("Server: " + Encoding.UTF8.GetString(response.Buffer));

            Console.WriteLine("Client spreman za zadatke...");

            while (true)
            {
                try
                {
                    var msg = await udpClient.ReceiveAsync();
                    string text = Encoding.UTF8.GetString(msg.Buffer);

                    // Ignorisi ACK poruke
                    if (text == "ACK")
                        continue;

                    Zadatak? zadatak = null;
                    try { zadatak = JsonSerializer.Deserialize<Zadatak>(text); } catch { }

                    if (zadatak == null)
                        continue;

                    Console.WriteLine($"Primljen zadatak: {zadatak.Tip} na polju ({zadatak.X},{zadatak.Y})");

                    // Simulacija izvrsenja
                    l.Status = StatusLetelice.Zauzeta;

                    // simulacija kvara (5%)
                    Random rnd = new Random();
                    if (rnd.Next(0, 20) == 0)
                    {
                        l.Status = StatusLetelice.UKvaru;

                        Alarm alarm = new Alarm
                        {
                            Tip = TipAlarma.Kvar,
                            X = l.X,
                            Y = l.Y,
                            Prioritet = 5,
                            LetelicaId = l.Id
                        };

                        string alarmJson = JsonSerializer.Serialize(alarm);
                        byte[] alarmData = Encoding.UTF8.GetBytes(alarmJson);
                        await udpClient.SendAsync(alarmData, alarmData.Length, serverEndpoint);

                        Console.WriteLine($"ALARM poslat serveru: {alarm.Tip} | Letelica: {l.Id}");

                        // posalji i update letelice (status je u kvaru)
                        string letJson2 = JsonSerializer.Serialize(l);
                        byte[] letData2 = Encoding.UTF8.GetBytes(letJson2);
                        await udpClient.SendAsync(letData2, letData2.Length, serverEndpoint);

                        continue;
                    }

                    // ako nema alarma nastavlja normalno izvrsavanje
                    await Task.Delay(2000);

                    zadatak.Status = StatusZadatka.Zavrsen;

                    zadatak.LetelicaId = l.Id;

                    l.X = zadatak.X;
                    l.Y = zadatak.Y;
                    l.Status = StatusLetelice.Slobodna;

                    // Salje zavrsen zadatak serveru
                    string jsonZadatak = JsonSerializer.Serialize(zadatak);
                    byte[] bytes = Encoding.UTF8.GetBytes(jsonZadatak);
                    await udpClient.SendAsync(bytes, bytes.Length, serverEndpoint);

                    // Salje update letelice (status i poziciju)
                    string letJson = JsonSerializer.Serialize(l);
                    byte[] letData = Encoding.UTF8.GetBytes(letJson);
                    await udpClient.SendAsync(letData, letData.Length, serverEndpoint);

                    Console.WriteLine($"Zadatak ({zadatak.Tip}) završen i poslat serveru");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Greška: " + ex.Message);
                }
            }
        }
    }
}
