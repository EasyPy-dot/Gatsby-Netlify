# Websocket-Sample

client example

C# using WebSocketSharp



internal class Program
{
    static private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    static WebSocket ws;
    
    static void Main(string[] args)
    {
        ws = new WebSocketSharp.WebSocket("ws://localhost:8080/TestWS");
        ws.OnClose += Ws_OnClose;
        ws.OnError += Ws_OnError;
        ws.OnMessage += Ws_OnMessage;
        ws.OnOpen += Ws_OnOpen;
        ws.Connect();
        while (true)
        {
            Thread.Sleep(1000);
            var msg = Console.ReadLine();
            if (msg == "exit")
                break;
            string testString = "2330";
            if (msg == "1")
                ws.Send(testString);
            if (msg == "2")
                ws.Send(Encoding.UTF8.GetBytes(testString));
            if (msg == "3")
            {
                var stream = new MemoryStream();
                var writer = new StreamWriter(stream);
                writer.Write(testString);
                writer.Flush();
                stream.Position = 0;
                ws.Send(stream, (int)stream.Length);
            }
        }
        Console.ReadKey();
    }

    private static void Ws_OnOpen(object sender, EventArgs e)
    {
        Console.WriteLine("Opened connection");
    }

    private static void Ws_OnMessage(object sender, MessageEventArgs e)
    {
        Console.WriteLine($"Received Message: {DateTime.Now.ToString("HHmmss.ffffff")} {e.Data}");
    }

    private static void Ws_OnError(object sender, WebSocketSharp.ErrorEventArgs e)
    {
        Console.WriteLine($"Received Message: {DateTime.Now.ToString("HHmmss.ffffff")} {e.Message}");
    }

    private static void Ws_OnClose(object sender, CloseEventArgs e)
    {
        Console.WriteLine($"Close: {e.Code} {e.Reason}");
        if ((int)e.Code == 1001)
        {
            ws.Connect();
        }
    }
}
