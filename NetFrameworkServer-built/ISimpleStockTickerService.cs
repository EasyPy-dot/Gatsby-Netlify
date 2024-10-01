using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace NetFrameworkServer_built
{
    [ServiceContract(CallbackContract = typeof(IWebSocketStockTickerCallback))]
    public interface IWebSocketStockTickerService
    {
        [OperationContract(IsOneWay = true, Action = "*")]
        Task Subscribe(Message msg);
    }
}
