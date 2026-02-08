using System;
using System.Collections.Generic;
using System.Text;

namespace Modeli
{
        public enum TipAlarma { Kvar, LoseVreme }

        public class Alarm
        {
            public TipAlarma Tip { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Prioritet { get; set; }
        }
}
