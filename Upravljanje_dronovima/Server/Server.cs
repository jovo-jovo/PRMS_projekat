using Modeli;
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
            var letelicaEndpoints = new Dictionary<Guid, IPEndPoint>();

            Console.WriteLine("Server pokrenut na portu 9000");

            int sirina = 5;
            int visina = 5;

            Polje[,] teren = new Polje[sirina, visina];

            for (int i = 0; i < sirina; i++)
            {
                for (int j = 0; j < visina; j++)
                {
                    teren[i, j] = new Polje
                    {
                        X = i,
                        Y = j,
                        Tip = TipPolja.Neobradjeno,
                        Status = StatusPolja.Slobodno
                    };
                }
            }

            Console.WriteLine("Teren 5x5 inicijalizovan.");

            Random rnd = new Random();

            while (true)
            {
                // Prima poruke ako ih ima
                if (udpServer.Available > 0)
                {
                    var rezultat = await udpServer.ReceiveAsync();
                    string json = Encoding.UTF8.GetString(rezultat.Buffer);

                    // Pokusaj: Letelica
                    Letelica? letelicaPrimljena = null;
                    try { letelicaPrimljena = JsonSerializer.Deserialize<Letelica>(json); } catch { }

                    if (letelicaPrimljena != null && letelicaPrimljena.Id != Guid.Empty)
                    {
                        var postojeca = letelice.Find(x => x.Id == letelicaPrimljena.Id);

                        if (postojeca == null)
                        {
                            letelice.Add(letelicaPrimljena);
                            Console.WriteLine($"Registrovana letelica: {letelicaPrimljena.Id} ({letelicaPrimljena.Tip})");
                        }
                        else
                        {
                            postojeca.X = letelicaPrimljena.X;
                            postojeca.Y = letelicaPrimljena.Y;
                            postojeca.Status = letelicaPrimljena.Status;
                        }

                        // Endpoint za slanje zadataka nazad
                        letelicaEndpoints[letelicaPrimljena.Id] = rezultat.RemoteEndPoint;

                        // ACK
                        byte[] ackData = Encoding.UTF8.GetBytes("ACK");
                        await udpServer.SendAsync(ackData, ackData.Length, rezultat.RemoteEndPoint);

                        await Task.Delay(20);
                    }
                    else
                    {
                        // Pokusaj: Zadatak (zavrsen)
                        Zadatak? zavrseniZadatak = null;
                        try { zavrseniZadatak = JsonSerializer.Deserialize<Zadatak>(json); } catch { }

                        if (zavrseniZadatak != null && zavrseniZadatak.Status == StatusZadatka.Zavrsen)
                        {
                            var zad = zadaci.Find(z => z.LetelicaId == zavrseniZadatak.LetelicaId &&
                                                       z.X == zavrseniZadatak.X &&
                                                       z.Y == zavrseniZadatak.Y &&
                                                       z.Status == StatusZadatka.UToku);

                            if (zad != null)
                            {
                                zad.Status = StatusZadatka.Zavrsen;

                                // Oslobodi letelicu
                                var let = letelice.Find(l => l.Id == zad.LetelicaId);
                                if (let != null)
                                    let.Status = StatusLetelice.Slobodna;

                                // Azuriraj polje
                                teren[zad.X, zad.Y].Tip = TipPolja.Obradjeno;
                                teren[zad.X, zad.Y].Status = StatusPolja.Slobodno;

                                Console.WriteLine($"Zadatak završen ({zad.Tip}) na ({zad.X},{zad.Y}) | Letelica: {zad.LetelicaId}");
                            }
                        }
                    }
                }

                // Generisanje alarma (10% sanse po ciklusu za neobradjena polja)
                foreach (var polje in teren)
                {
                    if (polje.Tip == TipPolja.Neobradjeno && rnd.Next(0, 10) == 0)
                    {
                        polje.Tip = TipPolja.Alarm;
                        Console.WriteLine($"Alarm na polju ({polje.X},{polje.Y})");
                    }
                }

                // Dodela i slanje zadataka slobodnim izvrsnim letelicama
                foreach (var polje in teren)
                {
                    // samo slobodna polja koja su Alarm ili Neobradjena
                    if ((polje.Tip == TipPolja.Alarm || polje.Tip == TipPolja.Neobradjeno) && polje.Status == StatusPolja.Slobodno)
                    {
                        var slobodnaLetelica = letelice.Find(l => l.Status == StatusLetelice.Slobodna && l.Tip == TipLetelice.Izvrsna);
                        if (slobodnaLetelica == null)
                            break;

                        if (!letelicaEndpoints.TryGetValue(slobodnaLetelica.Id, out var ep))
                            continue;

                        bool vecUToku = zadaci.Exists(z => z.X == polje.X && z.Y == polje.Y && z.Status == StatusZadatka.UToku);
                        if (vecUToku)
                            continue;

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

                        // Obiljezi polje zauzetim dok se radi
                        polje.Status = StatusPolja.Zauzeto;

                        // Posalji zadatak letelici
                        string zadJson = JsonSerializer.Serialize(zadatak);
                        byte[] zadData = Encoding.UTF8.GetBytes(zadJson);
                        await udpServer.SendAsync(zadData, zadData.Length, ep);

                        Console.WriteLine($"Poslat zadatak {zadatak.Tip} letelici {slobodnaLetelica.Id} za ({polje.X},{polje.Y})");
                        break;
                    }
                }

                await Task.Delay(1000);
            }
        }
    }
}
