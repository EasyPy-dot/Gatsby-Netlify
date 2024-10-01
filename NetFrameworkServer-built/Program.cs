using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace NetFrameworkServer_built
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Create WebSocket
            var binding = new CustomBinding();
            binding.Elements.Add(new ByteStreamMessageEncodingBindingElement());
            HttpTransportBindingElement transport = new HttpTransportBindingElement();
            transport.WebSocketSettings.MaxPendingConnections = 100;
            transport.WebSocketSettings.TransportUsage = WebSocketTransportUsage.Always;
            transport.WebSocketSettings.CreateNotificationOnConnection = true;
            binding.Elements.Add(transport);
            var baseAddress = new Uri("http://localhost:8080/TestWS");
            var host = new ServiceHost(typeof(NetFrameworkServer_built.WebSocketStockTickerService), baseAddress);
            // Enable metadata publishing.
            var behaviour = new ServiceMetadataBehavior();
            behaviour.HttpGetEnabled = true;
            behaviour.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
            host.Description.Behaviors.Add(behaviour);
            host.AddServiceEndpoint(typeof(IWebSocketStockTickerService), binding, "");

            // Open the ServiceHost to start listening for messages.
            Console.WriteLine("Opening websocket...");
            try
            {
                host.Open();
                Console.WriteLine("Opened!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot open: {e.Message}");
                throw;
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
