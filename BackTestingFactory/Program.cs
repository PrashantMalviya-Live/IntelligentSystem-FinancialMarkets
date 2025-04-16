using System;

namespace BackTestingFactory
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            MarketData marketData = new MarketData();
            marketData.RetrieveHistoricalTicksFromKite();
        }
    }
}
