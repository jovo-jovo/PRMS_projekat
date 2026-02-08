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
            var zadaci = new List<Zadatak>();

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

                foreach (var polje in teren)
                {
                    if (polje.Tip == TipPolja.Alarm || polje.Tip == TipPolja.Neobradjeno)
                    {
                        var slobodnaLetelica = letelice.Find(l => l.Status == StatusLetelice.Slobodna && l.Tip == TipLetelice.Izvrsna);
                        if (slobodnaLetelica != null)
                        {
                            Zadatak zadatak = new Zadatak
                            {
                                Tip = TipZadatka.Navodnjavanje,
                                X = polje.X,
                                Y = polje.Y,
                                Status = StatusZadatka.UToku,
                                LetelicaId = slobodnaLetelica.Id
                            };

                            zadaci.Add(zadatak);
                            slobodnaLetelica.Status = StatusLetelice.Zauzeta;

                            Console.WriteLine($"Zadatak ({zadatak.Tip}) dodeljen letelici {slobodnaLetelica.Id} za polje ({polje.X},{polje.Y})");
                        }
                    }
                }

                var rezultat = await udpServer.ReceiveAsync();
                string json = Encoding.UTF8.GetString(rezultat.Buffer);

                try
                {
                    Letelica? letelicaPrimljena = JsonSerializer.Deserialize<Letelica>(json);
                    if (letelicaPrimljena != null && !letelice.Exists(l2 => l2.Id == letelicaPrimljena.Id))
                    {
                        letelice.Add(letelicaPrimljena);
                        Console.WriteLine($"Primljena letelica: {letelicaPrimljena.Id} ({letelicaPrimljena.Tip}) | Pozicija: ({letelicaPrimljena.X},{letelicaPrimljena.Y})");
                    }
                }
                catch
                {
                    try
                    {
                        Zadatak? zavrseniZadatak = JsonSerializer.Deserialize<Zadatak>(json);
                        if (zavrseniZadatak != null && zavrseniZadatak.Status == StatusZadatka.Zavrsen)
                        {
                            var zad = zadaci.Find(z => z.LetelicaId == zavrseniZadatak.LetelicaId &&
                                                        z.X == zavrseniZadatak.X && z.Y == zavrseniZadatak.Y);
                            if (zad != null)
                            {
                                zad.Status = StatusZadatka.Zavrsen;

                                var letelica = letelice.Find(l => l.Id == zad.LetelicaId);
                                if (letelica != null)
                                {
                                    letelica.Status = StatusLetelice.Slobodna;

                                    teren[zad.X, zad.Y].Tip = TipPolja.Obradjeno;

                                    Console.WriteLine($"Zadatak ({zad.Tip}) završen na polju ({zad.X},{zad.Y}) od letelice {letelica.Id}");
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }

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
