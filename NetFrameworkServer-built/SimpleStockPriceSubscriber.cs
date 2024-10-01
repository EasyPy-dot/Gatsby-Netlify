using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentScheduler;
using Newtonsoft.Json;

namespace NetFrameworkServer_built
{
    public enum WCFType
    { NetTcp, WebSocket };

    public class SimpleStockPriceSubscriber : IDisposable
    {
        private readonly StockPrice[] _prices;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _task;
        private readonly Random _random;

        public SimpleStockPriceSubscriber(WCFType type, string[] symbols)
        {
            _prices = symbols.Select(s => new StockPrice(s, WebStock.GetInfo(s).ToString<StockInfo>())).ToArray();
            foreach (var item in _prices)
            {
                if (item.Price != null)
                {
                    Update?.Invoke(new StockPriceUpdateEventArgs(item.Symbol, item.Price));
                }
            }
            JobManager.Stop();
            JobManager.Initialize();
            _cancellationTokenSource = new CancellationTokenSource();
            _task = RunAsync(_cancellationTokenSource.Token);
        }
        public Action<StockPriceUpdateEventArgs> Update;

        private async Task RunAsync(CancellationToken token)
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
                        throw ex;
                    }
                    if (!token.IsCancellationRequested)
                    {
                        if (Update != null)
                        {
                            JobManager.AddJob(() =>
                            {
                                foreach (var item in _prices)
                                {
                                    var stockInfo = WebStock.GetInfo(item.Symbol);
                                    if (stockInfo != null)
                                    {
                                        item.Price = stockInfo.ToString<StockInfo>();
                                        Update?.Invoke(new StockPriceUpdateEventArgs(item.Symbol, item.Price));
                                    }
                                }
                            },
                                s => s.ToRunEvery(10).Seconds()
                            );
                        }
                    }
                }, token);
            }
            catch (OperationCanceledException opex)
            {
                throw opex;
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
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
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
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
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls;
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
