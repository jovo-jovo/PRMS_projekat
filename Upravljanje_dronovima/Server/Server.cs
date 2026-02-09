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
            var alarmi = new List<Alarm>();

            // statistika po zadatku
            var taskStart = new Dictionary<Guid, DateTime>();     
            var taskRedistribuisan = new HashSet<Guid>(); 

            // statistika po letelici
            var DronOdradio = new Dictionary<Guid, List<(Zadatak task, TimeSpan dur)>>();

            // statistika servera
            int alarmBrojac = 0;
            int redistBrojac = 0;
            int redistUspesnost = 0;

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
                // prima poruke ako ih ima
                if (udpServer.Available > 0)
                {
                    var rezultat = await udpServer.ReceiveAsync();
                    string json = Encoding.UTF8.GetString(rezultat.Buffer);

                    // pokusaj: letelica
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

                        // endpoint za slanje zadataka nazad
                        letelicaEndpoints[letelicaPrimljena.Id] = rezultat.RemoteEndPoint;

                        // ACK
                        byte[] ackData = Encoding.UTF8.GetBytes("ACK");
                        await udpServer.SendAsync(ackData, ackData.Length, rezultat.RemoteEndPoint);

                        await Task.Delay(20);
                    }
                    else
                    {
                        // pokusaj: zadatak je zavrsen
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

                                TimeSpan trajanje = TimeSpan.Zero;
                                if (taskStart.TryGetValue(zad.Id, out var start))
                                {
                                    trajanje = DateTime.Now - start;
                                    taskStart.Remove(zad.Id);
                                }

                                if (!DronOdradio.ContainsKey(zavrseniZadatak.LetelicaId))
                                    DronOdradio[zavrseniZadatak.LetelicaId] = new List<(Zadatak, TimeSpan)>();

                                DronOdradio[zavrseniZadatak.LetelicaId].Add((zad, trajanje));

                                // ako je bio redistribuisan i gotov je, onda uspesna redistribucija
                                if (taskRedistribuisan.Contains(zad.Id))
                                {
                                    redistUspesnost++;
                                    taskRedistribuisan.Remove(zad.Id);
                                }

                                // oslobodi letelicu
                                var let = letelice.Find(l => l.Id == zad.LetelicaId);
                                if (let != null)
                                    let.Status = StatusLetelice.Slobodna;

                                // azurira polje
                                teren[zad.X, zad.Y].Tip = TipPolja.Obradjeno;
                                teren[zad.X, zad.Y].Status = StatusPolja.Slobodno;

                                Console.WriteLine($"Zadatak završen ({zad.Tip}) na ({zad.X},{zad.Y}) | Letelica: {zad.LetelicaId}");
                            }
                        }
                        else
                        {
                            Alarm? alarm = null;
                            try { alarm = JsonSerializer.Deserialize<Alarm>(json); } catch { }

                            if (alarm != null && alarm.LetelicaId != Guid.Empty)
                            {
                                alarmi.Add(alarm);

                                Console.WriteLine($"ALARM: {alarm.Tip} | Letelica: {alarm.LetelicaId} | Prioritet: {alarm.Prioritet}");

                                // letelica u kvaru
                                var let = letelice.Find(l => l.Id == alarm.LetelicaId);
                                if (let != null)
                                    let.Status = StatusLetelice.UKvaru;

                                // redistribucija zadataka letelice
                                foreach (var z in zadaci)
                                {
                                    if (z.LetelicaId == alarm.LetelicaId && z.Status == StatusZadatka.UToku)
                                    {
                                        z.LetelicaId = Guid.Empty;
                                        z.Status = StatusZadatka.UToku;
                                        teren[z.X, z.Y].Status = StatusPolja.Slobodno;

                                        Console.WriteLine($"REDISTRIBUCIJA: zadatak ({z.Tip}) za ({z.X},{z.Y}) vraćen.");
                                    }
                                }
                            }
                        }
                    }
                }

                // generisanje alarma (10% sanse po ciklusu za neobradjena polja)
                foreach (var polje in teren)
                {
                    if (polje.Tip == TipPolja.Neobradjeno && rnd.Next(0, 10) == 0)
                    {
                        polje.Tip = TipPolja.Alarm;
                        Console.WriteLine($"Alarm na polju ({polje.X},{polje.Y})");
                    }
                }

                // dodela i slanje zadataka slobodnim izvrsnim letelicama
                foreach (var polje in teren)
                {
                    // samo slobodna polja koja su alarm ili neobradjena
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
                            Id = Guid.NewGuid(),
                            Tip = TipZadatka.Navodnjavanje,
                            X = polje.X,
                            Y = polje.Y,
                            Status = StatusZadatka.UToku,
                            LetelicaId = slobodnaLetelica.Id
                        };

                        zadaci.Add(zadatak);
                        taskStart[zadatak.Id] = DateTime.Now;       //startno vrijeme zadatka
                        slobodnaLetelica.Status = StatusLetelice.Zauzeta;

                        // biljezi polje zauzetim dok se radi
                        polje.Status = StatusPolja.Zauzeto;

                        // salje zadatak letelici
                        string zadJson = JsonSerializer.Serialize(zadatak);
                        byte[] zadData = Encoding.UTF8.GetBytes(zadJson);
                        await udpServer.SendAsync(zadData, zadData.Length, ep);

                        Console.WriteLine($"Poslat zadatak {zadatak.Tip} letelici {slobodnaLetelica.Id} za ({polje.X},{polje.Y})");
                        break;
                    }
                }

                if (Console.KeyAvailable)
                {
                    var kljuc = Console.ReadKey(true).Key;

                    if (kljuc == ConsoleKey.R)
                    {
                        PrintReport(DronOdradio, alarmBrojac, redistBrojac, redistUspesnost);
                    }
                }

                await Task.Delay(1000);
            }
        }
    }
}
