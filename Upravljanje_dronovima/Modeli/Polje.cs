using System;
using System.Collections.Generic;
using System.Text;

namespace Modeli
{
    public enum TipPolja { Neobradjeno, Obradjeno, Alarm }
    public enum StatusPolja { Slobodno, Zauzeto }

    public class Polje
    {
        public int X { get; set; }
        public int Y { get; set; }
        public TipPolja Tip { get; set; } = TipPolja.Neobradjeno;
        public StatusPolja Status { get; set; } = StatusPolja.Slobodno;
    }
}
