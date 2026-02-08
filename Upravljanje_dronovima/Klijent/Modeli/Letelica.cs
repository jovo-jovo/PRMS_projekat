using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Upravljanje_Dronovima.Models
{
    public enum TipLetelice { Nadzorna, Izvrsna }
    public enum StatusLetelice { Slobodna, Zauzeta, UKvaru }

    public class Letelica
    {
        public Guid Id { get; set; }
        public TipLetelice Tip { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public StatusLetelice Status { get; set; }
    }
}