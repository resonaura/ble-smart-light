using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace BLELight
{
    class LampColor
    {
        public Color Color { get; set; }
        public int Warm { get; set; }
        public bool IsWarm { get; set; }
        public bool IsTurnOn { get; set; }
    }
}
