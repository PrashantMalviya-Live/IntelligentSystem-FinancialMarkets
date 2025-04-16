using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Web.Script.Serialization;
using Microsoft.VisualBasic.FileIO;
using System.IO;
using System.Web;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
//using Newtonsoft.Json;
//using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;

namespace StockMarketAlertsApp
{
    public class Utils
    {
        /// <summary>
        /// Convert string to Date object
        /// </summary>
        /// <param name="DateString">Date string.</param>
        /// <returns>Date object/</returns>
        public static DateTime? StringToDate(string DateString)
        {
            try
            {
                if (DateString != null)
                {

                    if (DateString.Length == 10)
                    {
                        return DateTime.ParseExact(DateString, "yyyy-MM-dd", null);
                    }
                    else
                    {
                        return DateTime.ParseExact(DateString, "yyyy-MM-dd HH:mm:ss", null);
                    }
                }
                else
                    return null;
            }catch (Exception)
            {
                return null;
            }
        }
        

        /// <summary>
        /// Serialize C# object to JSON string.
        /// </summary>
        /// <param name="obj">C# object to serialize.</param>
        /// <returns>JSON string/</returns>
        //public static string JsonSerialize(object obj)
        //{
        //    //var jss = new JavaScriptSerializer();
        //    //string json = jss.Serialize(obj);

        //    string json = JsonConvert.SerializeObject(obj);

        //    MatchCollection mc = Regex.Matches(json, @"\\/Date\((\d*?)\)\\/");
        //    foreach (Match m in mc)
        //    {
        //        UInt64 unix = UInt64.Parse(m.Groups[1].Value) / 1000;
        //        json = json.Replace(m.Groups[0].Value, UnixToDateTime(unix).ToString());
        //    }
        //    return json;
        //}

        ///// <summary>
        ///// Deserialize Json string to nested string dictionary.
        ///// </summary>
        ///// <param name="Json">Json string to deserialize.</param>
        ///// <returns>Json in the form of nested string dictionary.</returns>
        //public static Dictionary<string, dynamic> JsonDeserialize(string Json)
        //{
        //    //var jss = new JavaScriptSerializer();
        //    //Dictionary<string, dynamic> dict = jss.Deserialize<Dictionary<string, dynamic>>(Json);


        //    //Dictionary<string, dynamic> dict = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(Json);

        //    Dictionary<string, dynamic> dict = deserializeToDictionaryOrList(Json, false) as Dictionary<string, object>;

        //    //Dictionary<string, dynamic> dict = (Dictionary<string, dynamic>)JsonConvert.DeserializeObject(Json);

        //    return dict;
        //}
        //private static Dictionary<string, object> deserializeToDictionary(string jo)
        //{
        //    var values = JsonConvert.DeserializeObject<Dictionary<string, object>>(jo);
        //    var values2 = new Dictionary<string, object>();
        //    foreach (KeyValuePair<string, object> d in values)
        //    {
        //        // if (d.Value.GetType().FullName.Contains("Newtonsoft.Json.Linq.JObject"))
        //        if (d.Value is JObject)
        //        {
        //            values2.Add(d.Key, deserializeToDictionary(d.Value.ToString()));
        //        }
        //        else
        //        {
        //            values2.Add(d.Key, d.Value);
        //        }
        //    }
        //    return values2;
        //}
        //private static object deserializeToDictionaryOrList(string jo, bool isArray = false)
        //{
        //    if (!isArray)
        //    {
        //        isArray = jo.Substring(0, 1) == "[";
        //    }
        //    if (!isArray)
        //    {
        //        var values = JsonConvert.DeserializeObject<Dictionary<string, object>>(jo);
        //        var values2 = new Dictionary<string, object>();
        //        foreach (KeyValuePair<string, object> d in values)
        //        {
        //            if (d.Value is JObject)
        //            {
        //                values2.Add(d.Key, deserializeToDictionary(d.Value.ToString()));
        //            }
        //            else if (d.Value is JArray)
        //            {
        //                values2.Add(d.Key, deserializeToDictionaryOrList(d.Value.ToString(), true));
        //            }
        //            else
        //            {
        //                values2.Add(d.Key, d.Value);
        //            }
        //        }
        //        return values2;
        //    }
        //    else
        //    {
        //        var values = JsonConvert.DeserializeObject<List<object>>(jo);
        //        var values2 = new List<object>();
        //        foreach (var d in values)
        //        {
        //            if (d is JObject)
        //            {
        //                values2.Add(deserializeToDictionary(d.ToString()));
        //            }
        //            else if (d is JArray)
        //            {
        //                values2.Add(deserializeToDictionaryOrList(d.ToString(), true));
        //            }
        //            else
        //            {
        //                values2.Add(d);
        //            }
        //        }
        //        return values2;
        //    }
        //}

        /// <summary>
        /// Parse instruments API's CSV response.
        /// </summary>
        /// <param name="Data">Response of instruments API.</param>
        /// <returns>CSV data as array of nested string dictionary.</returns>
        public static List<Dictionary<string, dynamic>> ParseCSV(string Data)
        {
            string[] lines = Data.Split('\n');

            List<Dictionary<string, dynamic>> instruments = new List<Dictionary<string, dynamic>>();

            using (TextFieldParser parser = new TextFieldParser(StreamFromString(Data)))
            {
                // parser.CommentTokens = new string[] { "#" };
                parser.SetDelimiters(new string[] { "," });
                parser.HasFieldsEnclosedInQuotes = true;

                // Skip over header line.
                string[] headers = parser.ReadLine().Split(',');

                while (!parser.EndOfData)
                {
                    string[] fields = parser.ReadFields();
                    Dictionary<string, dynamic> item = new Dictionary<string, dynamic>();

                    for (var i = 0; i < headers.Length; i++)
                        item.Add(headers[i], fields[i]);

                    instruments.Add(item);
                }
            }

            return instruments;
        }

        /// <summary>
        /// Wraps a string inside a stream
        /// </summary>
        /// <param name="value">string data</param>
        /// <returns>Stream that reads input string</returns>
        public static MemoryStream StreamFromString(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }

        /// <summary>
        /// Helper function to add parameter to the request only if it is not null or empty
        /// </summary>
        /// <param name="Params">Dictionary to add the key-value pair</param>
        /// <param name="Key">Key of the parameter</param>
        /// <param name="Value">Value of the parameter</param>
        public static void AddIfNotNull(Dictionary<string, dynamic> Params, string Key, dynamic Value)
        {
            if (!String.IsNullOrEmpty(Value))
                Params.Add(Key, Value);
        }

        /// <summary>
        /// Generates SHA256 checksum for login.
        /// </summary>
        /// <param name="Data">Input data to generate checksum for.</param>
        /// <returns>SHA256 checksum in hex format.</returns>
        public static string SHA256(string Data)
        {
            Console.WriteLine(Data);
            SHA256Managed sha256 = new SHA256Managed();
            StringBuilder hexhash = new StringBuilder();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(Data), 0, Encoding.UTF8.GetByteCount(Data));
            foreach (byte b in hash)
            {
                hexhash.Append(b.ToString("x2"));
            }
            return hexhash.ToString();
        }

        /// <summary>
        /// Creates key=value with url encoded value
        /// </summary>
        /// <param name="Key">Key</param>
        /// <param name="Value">Value</param>
        /// <returns>Combined string</returns>
        public static string BuildParam(string Key, dynamic Value)
        {
            if (Value is string)
            {
                return HttpUtility.UrlEncode(Key) + "=" + HttpUtility.UrlEncode((string)Value);
            }
            else
            {
                string[] values = (string[])Value;
                return String.Join("&", values.Select(x => HttpUtility.UrlEncode(Key) + "=" + HttpUtility.UrlEncode(x)));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unixTimeStamp"></param>
        /// <returns></returns>
        public static DateTime UnixToDateTime(UInt64 unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dateTime = new DateTime(1970, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified);
            dateTime = dateTime.AddSeconds(unixTimeStamp);
            return dateTime;
        }

        public static UInt64 UnixFromDateTime(DateTime? unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            DateTime dateTime = new DateTime(1970, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified);

            if (unixTimeStamp == null)
                return 0;
            return (UInt64) (unixTimeStamp.Value - dateTime).TotalSeconds;
        }

        //public static byte[] ObjectToByteArray(object obj)
        //{
        //    if (obj == null)
        //        return null;

        //    BinaryFormatter bf = new BinaryFormatter();
        //    using (MemoryStream ms = new MemoryStream())
        //    {
        //        bf.Serialize(ms, obj);
        //        return ms.ToArray();
        //    }
        //}

        //public static object ByteArrayToObject(byte[] ba)
        //{
        //    if (ba == null)
        //        return null;
        //    object obj = null;
        //    BinaryFormatter bf = new BinaryFormatter();
        //    using (MemoryStream ms = new MemoryStream(ba))
        //    {
        //        try
        //        {
        //            obj = bf.Deserialize(ms);
        //        }
        //        catch (Exception ex)
        //        {
        //            throw ex;
        //        }
        //        return obj;
        //    }
        //}

        //public static byte[] TickToByteArray(Tick tick)
        //{

        //    byte[] iToken = BitConverter.GetBytes(tick.InstrumentToken);

        //    decimal divisor = (tick.InstrumentToken & 0xff) == 3 ? 10000000.0m : 100.0m;

        //    byte[] lastPrice = BitConverter.GetBytes(Convert.ToInt32(tick.LastPrice * divisor));
        //    byte[] lastQuantity = BitConverter.GetBytes(tick.LastQuantity);
        //    byte[] averagePrice = BitConverter.GetBytes(Convert.ToInt32(tick.AveragePrice * divisor));
        //    byte[] volume = BitConverter.GetBytes(tick.Volume);
        //    byte[] buyQuantity = BitConverter.GetBytes(tick.BuyQuantity);
        //    byte[] sellQuantity = BitConverter.GetBytes(tick.SellQuantity);
        //    byte[] open = BitConverter.GetBytes(Convert.ToInt32(tick.Open * divisor));
        //    byte[] high = BitConverter.GetBytes(Convert.ToInt32(tick.High * divisor));
        //    byte[] low = BitConverter.GetBytes(Convert.ToInt32(tick.Low * divisor));
        //    byte[] close = BitConverter.GetBytes(Convert.ToInt32(tick.Close * divisor));


        //    byte[] lastTradeTime;
        //    if (tick.LastTradeTime.HasValue)
        //    {
        //        lastTradeTime = BitConverter.GetBytes(Convert.ToInt32(ConvertToUnixTimestamp(tick.LastTradeTime)));
        //    }
        //    else
        //    {
        //        lastTradeTime = BitConverter.GetBytes(0);
        //    }

        //    byte[] oi = BitConverter.GetBytes(tick.OI);
        //    byte[] oiDayHigh = BitConverter.GetBytes(tick.OIDayHigh);
        //    byte[] oiDayLow = BitConverter.GetBytes(tick.OIDayLow);
        //    byte[] timestamp = BitConverter.GetBytes(Convert.ToInt32(ConvertToUnixTimestamp(tick.Timestamp)));

        //    return Combine(iToken, lastPrice, lastQuantity, averagePrice, volume, buyQuantity, sellQuantity, open, high, low, close, lastTradeTime, oi, oiDayHigh, oiDayLow, timestamp);
        //}
        private static byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
        public static double ConvertToUnixTimestamp(DateTime? date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.Value.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }
    }
    public class FixedSizedQueue<T>
    {
        ConcurrentQueue<T> q = new ConcurrentQueue<T>();
        private object lockObject = new object();

        public int Limit { get; set; } = 300;
        public void Enqueue(T obj)
        {
            q.Enqueue(obj);
            lock (lockObject)
            {
                T overflow;
                while (q.Count > Limit && q.TryDequeue(out overflow)) ;
            }
        }
        public T GetLastElement()
        {
            return q.Last();
        }
        public T GetFirstElement()
        {
            return q.FirstOrDefault(null);
        }
        public T RemoveLastElement()
        {
            q.Reverse();
            T element;
            q.TryDequeue(out element);
            q.Reverse();
            return element;
        }
        public decimal Value
        {
            get
            {
                return q.Count > 0 ? q.Average(x => Convert.ToDecimal(x)) : 0;
            }
        }
        public decimal LastValue
        {
            get
            {
                return q.Count > 0 ? Convert.ToDecimal(q.Last()) : 0;
            }
        }
        public decimal SecondLastValue
        {
            get
            {
                return q.Count > 1 ? Convert.ToDecimal(q.SkipLast(1).Last()) : 0;
            }
        }

        public decimal Max
        {
            get
            {
                return q.Count > 0 ? q.Max(x => Convert.ToDecimal(x)) : 0;
            }
        }
        public decimal Min
        {
            get
            {
                return q.Count > 0 ? q.Min(x => Convert.ToDecimal(x)) : 0;
            }
        }
    }
}
