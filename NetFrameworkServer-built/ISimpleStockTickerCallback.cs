using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace NetFrameworkServer_built
{
    [ServiceContract]
    public interface IWebSocketStockTickerCallback
    {
        [OperationContract(IsOneWay = true, Action = "*")]
        void Update(Message msg);
    }
}
