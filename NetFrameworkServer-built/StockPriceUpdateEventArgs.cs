using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace NetFrameworkServer_built
{
    [DataContract]
    public class StockPriceUpdateEventArgs
    {
        public StockPriceUpdateEventArgs(string symbol, string price)
        {
            Symbol = symbol;
            Price = price;
        }
        [DataMember]
        public string Symbol { get; }
        [DataMember]
        //public decimal Price { get; }
        public string Price { get; }
    }

    public class SubscribeItem
    {
        public string[] Symbols { get; set; }
    }

    [DataContract]
    public class AckEventArgs
    {
        public AckEventArgs(string msg)
        {
            Msg = msg;
        }
        [DataMember] public string Msg { get; }
    }
}
