using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebsocketManager;

namespace NetCoreServer
{
    public class SimpleStockPriceTickerService : WebsocketHandler
    {
        private readonly CancellationTokenSource cTokenSource;
        private readonly CancellationToken cToken;
        private readonly bool _isConnected;
        private JsonSerializerOptions options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        private readonly bool disposed;
        public SimpleStockPriceTickerService(WebsocketConnection socketManager) : base(socketManager)
        {
            cTokenSource = new CancellationTokenSource();
            cToken = cTokenSource.Token;
            _isConnected = true;
        }

        public override async Task Connected(WebSocket webSocket)
        {
            await base.Connected(webSocket);                        
            var socketId = WebSocketConnection.GetSocketId(webSocket);
            new SimpleStockPriceSubscriber(this, socketId, webSocket);
            SimpleStockPriceSubscriber.Opened?.Invoke(webSocket, socketId);
        }

        public override async Task Disconnected(WebSocket webSocket)
        {
            var socketId = WebSocketConnection.GetSocketId(webSocket);          
            SimpleStockPriceSubscriber.Closed?.Invoke(webSocket, socketId);
            await base.Disconnected(webSocket);
        }

        public override async Task ReceiveAsync(WebSocket webSocket, WebSocketReceiveResult result, byte[] buffer)
        {
            await Task.Run(() =>
            {
                var socketId = WebSocketConnection.GetSocketId(webSocket);
                SimpleStockPriceSubscriber.Received?.Invoke(webSocket, result, buffer, socketId);
            });
        }

        internal async void BuildMessage(string eventName, Action<Utf8JsonWriter> body, string? socketId = null)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                    {
                        writer.WriteStartObject();
                        writer.WriteString("event", eventName);
                        writer.WriteStartObject("data");
                        body.Invoke(writer);
                        writer.WriteEndObject();
                        writer.WriteEndObject();
                        await writer.FlushAsync();
                    }
                    MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)ms.ToArray(), out var array);
                    string msg = Encoding.UTF8.GetString(array.Array!);
                    await SendMessageToAllAsync(msg);
                }
            }
            catch (NullReferenceException nrex)
            {
                BuildMessage("exception", (writer) => writer.WriteString("content", $"{nrex.Source} {nrex.StackTrace}-{nrex.Message}"));
            }
            catch (WebSocketException wsex)
            {
                BuildMessage("exception", (writer) => writer.WriteString("content", $"{wsex.Source} {wsex.StackTrace}-{wsex.Message}"));
            }
            catch (Exception ex)
            {
                BuildMessage("exception", (writer) => writer.WriteString("content", $"{ex.Source} {ex.StackTrace}-{ex.Message}"));
            }
        }
    }

    public class SimpleStockPriceSubscriber : IDisposable
    {
        private readonly CancellationTokenSource cTockenSource;
        private readonly CancellationToken cTocken;
        private bool disposed;
        static public Action<WebSocket, string>? Opened;
        static public Action<WebSocket, string>? Closed;
        static public Action<WebSocket, WebSocketReceiveResult, byte[], string>? Received;
        private SimpleStockPriceTickerService tickerService;

        public SimpleStockPriceSubscriber(SimpleStockPriceTickerService _tickerService, string _socketId, WebSocket _webSocket)
        {
            cTockenSource = new CancellationTokenSource();
            cTocken = cTockenSource.Token;
            tickerService = _tickerService;
            SimpleStockPriceSubscriber.Opened += sub_Opened;
            SimpleStockPriceSubscriber.Closed += sub_Closed;
            SimpleStockPriceSubscriber.Received += sub_Received;
        }

        private void sub_Opened(WebSocket webSocket, string socketId)
        {
            tickerService.BuildMessage("active", (writer) => writer.WriteString("content", $"{socketId} isConnected"), socketId);
        }

        private void sub_Closed(WebSocket webSocket, string socketId)
        {
            tickerService.BuildMessage("active", (writer) => writer.WriteString("content", $"{socketId} isDisconnected"), socketId);
            this.Dispose();
        }

        private void sub_Received(WebSocket webSocket, WebSocketReceiveResult result, byte[] buffer, string socketId)
        {
            try
            {
                var symbol = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Subscriber(new[] { symbol }, socketId);
            }
            catch (Exception ex)
            {
                tickerService.BuildMessage("exception", (writer) => writer.WriteString("content", $"{ex.Source} {ex.StackTrace}-{ex.Message}"), socketId);
            }
        }

        private void Subscriber(string[] symbols, string socketId)
        {
            try
            {
                var _prices = symbols.Select(s => new StockPrice(s, WebStock.GetInfo(s).ToString<StockInfo>())).ToArray();
                _ = RunAsync(_prices, new CancellationTokenSource().Token, socketId);
            }
            catch (Exception ex)
            {
                tickerService.BuildMessage("exception", (writer) => writer.WriteString("content", $"{ex.Source} {ex.StackTrace}-{ex.Message}"), socketId);
            }
        }

        private async Task RunAsync(StockPrice[] prices, CancellationToken token, string socketId)
        {
            try
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(15, token);
                    }
                    catch (Exception ex)
                    {
                        tickerService.BuildMessage("exception", (writer) => writer.WriteString("content", $"{ex.Source} {ex.StackTrace}-{ex.Message}"), socketId);
                    }
                    if (!token.IsCancellationRequested)
                    {
                        foreach (var item in prices)
                        {
                            var stockInfo = WebStock.GetInfo(item.Symbol);
                            if (stockInfo != null)
                            {
                                item.Price = stockInfo.ToString<StockInfo>();
                                tickerService.BuildMessage("Info", (writer) => writer.WriteString("content", $"{item.Price}"), socketId);
                            }
                        }
                    }
                }, token);
                await Task.Delay(10*1000, token);
                await RunAsync(prices, token, socketId);
            }
            catch (OperationCanceledException opex)
            {
                tickerService.BuildMessage("exception", (writer) => writer.WriteString("content", $"{opex.Source} {opex.StackTrace}-{opex.Message}"), socketId);
            }
        }

        protected void Dispose(bool disposing)
        {
            if (!disposing)
            {
                try
                {
                    cTockenSource.Cancel();
                    cTockenSource.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.Print($"{ex.Source} {ex.StackTrace}-{ex.Message}");
                }
            }
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(disposing:  true);
            GC.SuppressFinalize(this);
        }

        ~SimpleStockPriceSubscriber()
        {
            Dispose();  
        }
    }

    internal class StockPrice : IEquatable<StockPrice>
    {
        public StockPrice(string symbol, string price)
        {
            Symbol = symbol;
            Price = price;
        }

        public string Symbol { get; }
        //public decimal Price { get; set; }
        public string Price { get; set; }

        public bool Equals(StockPrice other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Symbol, other.Symbol, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StockPrice)obj);
        }

        public override int GetHashCode()
        {
            return (Symbol != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Symbol) : 0);
        }

        public static bool operator ==(StockPrice left, StockPrice right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StockPrice left, StockPrice right)
        {
            return !Equals(left, right);
        }
    }

    public class WebStock
    {
        static StockInfo info;
        static long ms;
        static public StockInfo GetInfo(string _symbol)
        {
            try
            {
                info = new StockInfo(_symbol);
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                ms = (long)(DateTime.UtcNow - epoch).TotalMilliseconds;
                var keys = StockBase($"https://mis.twse.com.tw/stock/api/getStock.jsp?ch={_symbol}.tw&json=1&_={ms}");
                Base baseData = JsonConvert.DeserializeObject<Base>(keys);
                if (baseData != null && baseData.msgArray.Any())
                {
                    var key = baseData.msgArray[0].key;
                    info.名稱 = baseData.msgArray[0].n;
                    info.日期 = baseData.msgArray[0].d;
                    info.昨收價 = baseData.msgArray[0].y;
                    info.跌停價 = baseData.msgArray[0].w;
                    info.漲停價 = baseData.msgArray[0].u;
                    var task = StockData($"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={key}&json=1&delay=0&_={ms}");
                    Data stockData = JsonConvert.DeserializeObject<Data>(task);
                    if (stockData != null && stockData.msgArray.Any())
                    {
                        info.時間 = stockData.msgArray[0].t;
                        info.成交量 = stockData.msgArray[0].tv;
                        info.累積成交量 = stockData.msgArray[0].v;
                        info.開盤價 = stockData.msgArray[0].o;
                        info.最低價 = stockData.msgArray[0].l;
                        info.最高價 = stockData.msgArray[0].h;
                        info.成交價 = stockData.msgArray[0].z;
                        if (!string.IsNullOrWhiteSpace(stockData.msgArray[0].b))
                            info.買價 = stockData.msgArray[0].b.Split('_').Where(w => !string.IsNullOrWhiteSpace(w)).Select(s => s).ToArray();
                        if (!string.IsNullOrWhiteSpace(stockData.msgArray[0].a))
                            info.賣價 = stockData.msgArray[0].a.Split('_').Where(w => !string.IsNullOrWhiteSpace(w)).Select(s => s).ToArray();
                        if (!string.IsNullOrWhiteSpace(stockData.msgArray[0].g))
                            info.買量 = stockData.msgArray[0].g.Split('_').Where(w => !string.IsNullOrWhiteSpace(w)).Select(s => s).ToArray();
                        if (!string.IsNullOrWhiteSpace(stockData.msgArray[0].f))
                            info.賣量 = stockData.msgArray[0].f.Split('_').Where(w => !string.IsNullOrWhiteSpace(w)).Select(s => s).ToArray();
                    }
                }
                return info;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        static string StockBase(string _url)
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls;
                    byte[] b = webClient.DownloadData(_url);
                    return Encoding.UTF8.GetString(b);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        static dynamic StockData(string _url)
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 |  SecurityProtocolType.Tls;
                    byte[] b = webClient.DownloadData(_url);
                    return Encoding.UTF8.GetString(b);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }

    public class StockInfo
    {
        public string 代碼 { get; set; }
        public string 名稱 { get; set; }
        //public decimal 昨收價 { get; set; }
        public string 昨收價 { get; set; }
        //public decimal 漲停價 { get; set; }
        public string 漲停價 { get; set; }
        //public decimal 跌停價 { get; set; }
        public string 跌停價 { get; set; }
        //public decimal 開盤價 { get; set; }
        public string 開盤價 { get; set; }
        //public decimal 最低價 { get; set; }
        public string 最低價 { get; set; }
        //public decimal 最高價 { get; set; }
        public string 最高價 { get; set; }
        //public decimal 成交價 { get; set; }
        public string 成交價 { get; set; }
        //public int 成交量 { get; set; }
        public string 成交量 { get; set; }
        //public long 累積成交量 { get; set; }
        public string 累積成交量 { get; set; }
        public string 日期 { get; set; }
        public string 時間 { get; set; }
        //public decimal[] 買價 { get; set; }
        public string[] 買價 { get; set; }
        //public int[] 買量 { get; set; }
        public string[] 買量 { get; set; }
        //public decimal[] 賣價 { get; set; }
        public string[] 賣價 { get; set; }
        //public int[] 賣量 { get; set; }
        public string[] 賣量 { get; set; }
        public StockInfo(string symbol)
        {
            代碼 = symbol;
        }
    }

    [Serializable]
    public class Data
    {
        public List<DataItem> msgArray { get; set; }
        public Data()
        {
            msgArray = new List<DataItem>();
        }
    }

    [Serializable]
    public class DataItem
    {
        public string c { get; set; }//代碼
        public string n { get; set; }//中文名稱
        public string o { get; set; }//開盤價
        public string l { get; set; }//最低價
        public string h { get; set; }//最高價
        public string v { get; set; }//累積成交量
        public string tv { get; set; }//成交量
        public string z { get; set; }//成交價
        public string t { get; set; }//時間
        public string d { get; set; }//日期
        public string b { get; set; }//買價5
        public string a { get; set; }//賣價5
        public string f { get; set; }//賣量5
        public string g { get; set; }//買量5
    }

    [Serializable]
    public class Base
    {
        public List<BaseItem> msgArray { get; set; }
        public Base()
        {
            msgArray = new List<BaseItem>();
        }
    }

    [Serializable]
    public class BaseItem
    {
        public string ex { get; set; }//交易所
        public string d { get; set; }//日期
        public string n { get; set; }//中文名稱
        public string w { get; set; }//跌停價
        public string u { get; set; }//漲停價
        public string t { get; set; }//時間
        public string ch { get; set; }//symbol code + .tw
        public string key { get; set; }//symbolKey
        public string y { get; set; }//昨日收盤價
    }

    public static class StringExtensions
    {
        public static string ToString<T>(this T t)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                string retVal = null;
                foreach (var prop in t.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (typeof(IList).IsAssignableFrom(prop.PropertyType))
                    {
                        IList list = (IList)prop.GetValue(t, null);
                        sb.Append($"{prop.Name} : ");
                        sb.Append($"{GetList<IList>(list)},");
                    }
                    else
                    {
                        sb.Append($"{prop.Name} : {prop.GetValue(t, null)},");
                    }
                }
                retVal = sb.ToString().TrimEnd(',');
                return string.IsNullOrEmpty(retVal) ? "{}" : "{ " + retVal + " }";
            }
            catch (Exception ex)
            {

            }
            return "{}";
        }

        private static string GetList<T>(this T lt)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                IList ilt = (IList)lt;
                sb.AppendLine("[");
                if (ilt == null)
                    return "";
                for (int j = 0; j < ilt.Count; j++)
                {
                    if (j == ilt.Count - 1)
                        sb.AppendLine($"[{j}] : {ilt[j]}");
                    else
                        sb.AppendLine($"[{j}] : {ilt[j]}, ");
                }
                sb.Append("]");
                return sb.ToString().TrimEnd(',');
            }
            catch (Exception ex)
            {

            }
            return "";
        }

        public static string ToString2<T>(this T t)
        {
            string retVal = GetProperties(t);
            return string.IsNullOrEmpty(retVal) ? "{}" : "{ " + retVal + " }";
        }

        private static string GetProperties<T>(this T obj)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (typeof(IList).IsAssignableFrom(prop.PropertyType))
                    {
                        IList list = (IList)prop.GetValue(obj, null);
                        sb.AppendLine($"{prop.Name} : [");
                        for (int j = 0; j < list.Count; j++)
                        {
                            if (j == list.Count - 1)
                                sb.AppendLine($"[{j}] : {list[j]}");
                            else
                                sb.AppendLine($"[{j}] : {list[j]}, ");
                        }
                        sb.AppendLine("],");
                    }
                    else
                    {
                        sb.Append($"{prop.Name} : {prop.GetValue(obj, null)},");
                    }
                }
                return sb.ToString().TrimEnd(',');
            }
            catch (Exception ex)
            {

            }
            return "";
        }
    }
}
