using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Security.AccessControl;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NetFrameworkServer_built
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class WebSocketStockTickerService : IWebSocketStockTickerService
    {
        private IWebSocketStockTickerCallback _callback;
        private SimpleStockPriceSubscriber _subscriber;
        public async Task Subscribe(Message msg)
        {
            _callback = OperationContext.Current.GetCallbackChannel<IWebSocketStockTickerCallback>();
            if (msg.IsEmpty || ((IChannel)_callback).State != CommunicationState.Opened)
            {
                return;
            }
            byte[] body = msg.GetBody<byte[]>();
            string msgFromClient = Encoding.UTF8.GetString(body);
            _subscriber = new SimpleStockPriceSubscriber(WCFType.WebSocket, new[] { msgFromClient });// item.Symbols);
            _subscriber.Update += SubscriberOnUpdate;
            await Task.Delay(2);
        }


        private void SubscriberOnUpdate(StockPriceUpdateEventArgs e)
        {
            try
            {
                var state = ((IChannel)_callback).State;
                var json = JsonConvert.SerializeObject(e);
                var stock = CreateMessage($"{json}");
                if (((IChannel)_callback).State == CommunicationState.Opened)
                    _callback.Update(stock);
            }
            catch (CommunicationException cex)
            {
                _subscriber.Dispose();
                _subscriber = null;
            }
        }

        Message CreateMessage(string msgText)
        {
            var msg = ByteStreamMessage.CreateMessage(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msgText)));
            msg.Properties["WebSocketMessageProperty"] = new WebSocketMessageProperty
            {
                MessageType = WebSocketMessageType.Text
            };
            return msg;
        }
    }
}
