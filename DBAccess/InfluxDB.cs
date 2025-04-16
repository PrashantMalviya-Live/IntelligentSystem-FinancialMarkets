using System;
using System.Collections.Generic;
using System.Text;
using GlobalLayer;
//using Questdb.Net;
//using Questdb.Net.Write;
using Npgsql;
using InfluxDB.Client;
using InfluxDB.Client.Core;
using System.Threading.Tasks;
using InfluxDB.Client.Writes;
using InfluxDB.Client.Api.Domain;
//using BrokerConnectWrapper;

namespace DataAccess
{
    public class InfluxDB
    {
        //QuestDBClient _client;
        private static readonly char[] Token = "".ToCharArray();
        InfluxDBClient _client;
        WriteApi _writeApi;
        NpgsqlConnection connection;
        NpgsqlCommand command;
        public InfluxDB()
        {
            const string token = "nySICkF5mmg9RY5rqNzXb3e2nvp1vbT9jaHb2Knhv1viD1OzY0o9E_x7N_KL5RS_Q9puM4fbBMECWFYUJkkPpg==";
            //const string bucket = "Tick";
            //const string org = "NSE";

            _client = InfluxDBClientFactory.Create("http://localhost:8086", token);

            _writeApi = _client.GetWriteApi();

            

            //string username = "admin";
            //string password = "quest";
            //string database = "qdb";
            //int port = 8812;
            //var connectionString = $@"host=localhost;port={port};username={username};password={password}; database={database};ServerCompatibilityMode=NoTypeLoading;";

            //connection = new NpgsqlConnection(connectionString);
            //connection.Open();


        }
        public void WriteTick(Tick tick)
        {
            //using (var writeApi = _client.GetWriteApi())
            //{
            //
            // Write by Point
            //

            try
            {
                var point = PointData.Measurement("Ticks")
                    .Tag("TradingSymbol", GlobalObjects.InstrumentTokenSymbolCollection[tick.InstrumentToken])
                    .Field("LastPrice", tick.LastPrice)
                    .Field("Volume", tick.Volume)
                    .Field("OI", tick.OI)
                    .Field("BQ", tick.BuyQuantity)
                    .Field("SQ", tick.SellQuantity)
                    .Timestamp(tick.LastTradeTime.HasValue ? tick.LastTradeTime.Value.ToUniversalTime() : tick.Timestamp.Value.ToUniversalTime(), WritePrecision.Ns)
                    .Timestamp(tick.Timestamp.Value.ToUniversalTime(), WritePrecision.Ns);

                _writeApi.WritePoint("Ticks", "NSE", point);
            }
            catch (Exception exp)
            {

            }

                ////
                //// Write by LineProtocol
                ////
                //writeApi.WriteRecord("bucket_name", "org_id", WritePrecision.Ns, "temperature,location=north value=60.0");

                ////
                //// Write by POCO
                ////
                //var temperature = new Temperature { Location = "south", Value = 62D, Time = DateTime.UtcNow };
                //writeApi.WriteMeasurement("bucket_name", "org_id", WritePrecision.Ns, temperature);
            //}
        }

        public void WriteTickGreeks(Historical historical, uint token, decimal delta, decimal gamma, decimal iv, DateTime expiry, string tradingSymbol)
        {
            try
            {
                var point = PointData.Measurement("OptionGreeks")
                    .Tag("TradingSymbol", tradingSymbol)
                    .Field("Volume", historical.Volume)
                    .Field("Open", historical.Open)
                    .Field("High", historical.High)
                    .Field("Low", historical.Low)
                    .Field("Close", historical.Close)
                    .Field("Delta", delta)
                    .Field("Gamma", gamma)
                    .Field("IV", iv)
                    .Timestamp(historical.TimeStamp.ToUniversalTime(), WritePrecision.Ns);
                    //.Timestamp(expiry.ToUniversalTime(), WritePrecision.Ns);

                _writeApi.WritePoint("OptionGreeks", "NSE", point);
            }
            catch (Exception exp)
            {

            }
        }
        public void InsertTick(Tick tick)
        {
            string sql = "";
            if (tick.LastTradeTime.HasValue)
            {
                sql = string.Format("Insert into Ticks (InstrumentSymbol, LastPrice, Volume, OI, LastTradeTime, Timestamp) values ('{0}',{1},{2},{3}, '{4}', '{5}')", tick.InstrumentToken.ToString(),
                    tick.LastPrice, tick.Volume, tick.OI, tick.LastTradeTime.Value.ToString("yyyy-MM-dd HH:mm:ss.ffffff"), tick.Timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
            }
            else
            {
                sql = string.Format("Insert into Ticks (InstrumentSymbol, LastPrice, Volume, OI, Timestamp) values ('{0}',{1},{2},{3},'{4}')", tick.InstrumentToken.ToString(),
                    tick.LastPrice, tick.Volume, tick.OI, tick.Timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss.ffffff"));
            }
            try
            {
                command = new NpgsqlCommand(sql, connection);
                command.ExecuteNonQuery();
            }
            catch
            {
                connection.Close();
            }


            // var writeApi = _client.GetWriteApi();
            //var point = PointData.Measurement("Ticks")
            //                    .Tag("InstrumentSymbol", tick.InstrumentToken.ToString())
            //                    .Field("LastPrice", tick.LastPrice)
            //                    .Field("Volume", tick.Volume)
            //                    .Timestamp(DateTime.SpecifyKind(tick.LastTradeTime.Value, DateTimeKind.Utc), WritePrecision.Microseconds)
            //                    .Field("OI", tick.OI);
            //                   // .Timestamp(DateTime.SpecifyKind(tick.Timestamp.Value, DateTimeKind.Utc), WritePrecision.Nanoseconds);
            //writeApi.WritePoint(point);
        }
    }
}
