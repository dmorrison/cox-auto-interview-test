
using System.Collections.Generic;

namespace CoxAutoInterviewTest
{
    class Dealer
    {
        public int DealerId { get; set; }
        public string Name { get; set; }
        public List<Vehicle> Vehicles { get; } = new List<Vehicle>();
    }
}
