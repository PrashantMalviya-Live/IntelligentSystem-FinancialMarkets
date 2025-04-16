using System.Collections.Generic;

namespace GlobalLayer
{
    /// <summary>
    /// This class stores reference data of all stock groups that will be selected for alerts.
    /// These could be individual stocks, or group of stocks such as Nifty50, nifty500 , stocks with futures etc
    /// </summary>
    public class AlertStockUniverse
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public ICollection<Instrument> Instruments { get; set; }
    }
}
