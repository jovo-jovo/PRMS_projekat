using System;
using System.Collections.Generic;
using System.Text;

namespace Modeli
{
        public enum TipZadatka { Setva, Navodnjavanje, Zetva }
        public enum StatusZadatka { UToku, Zavrsen }

        public class Zadatak
        {
            public Guid Id { get; set; }
            public TipZadatka Tip { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public StatusZadatka Status { get; set; } = StatusZadatka.UToku;
            public Guid LetelicaId { get; set; }
        }
}
