using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using GlobalLayer;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Metadata.Ecma335;
//using Alachisoft.NCache.Common.Util;

namespace DataAccess
{
    public class MarketDAO
    {
        public string GetAccessToken()
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand sqlCMD = new SqlCommand("GetAccessToken", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.Add("@AccessToken", SqlDbType.VarChar, 100).Direction = ParameterDirection.Output;

            sqlConnection.Open();
            sqlCMD.ExecuteNonQuery();
            sqlConnection.Close();

            return (string)sqlCMD.Parameters["@AccessToken"].Value;
        }
        public bool UpdateUser(User activeUser)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand sqlCMD = new SqlCommand("UpdateUser", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@UserId", activeUser.UserId);
            sqlCMD.Parameters.AddWithValue("@UserName", activeUser.UserName);
            sqlCMD.Parameters.AddWithValue("@Broker", activeUser.Broker);
            sqlCMD.Parameters.AddWithValue("@Email", activeUser.Email);
            sqlCMD.Parameters.AddWithValue("@ApiKey", activeUser.APIKey);
            sqlCMD.Parameters.AddWithValue("@AccessToken", activeUser.AccessToken);

            try
            {
                sqlConnection.Open();
                sqlCMD.ExecuteNonQuery();
                sqlConnection.Close();
                return true;
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return false;
        }
        public DataSet GetActiveUser()
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand selectCMD = new SqlCommand("GetActiveUser", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsUser = new DataSet();
            daInstruments.Fill(dsUser);
            sqlConnection.Close();

            return dsUser;
        }
        public int GenerateAlgoInstance(AlgoIndex algoIndex, uint bToken, DateTime timeStamp, DateTime expiry,
            int initialQtyInLotsSize, int maxQtyInLotSize = 0, int stepQtyInLotSize = 0, decimal upperLimit = 0, 
            decimal upperLimitPercent = 0, decimal lowerLimit = 0, decimal lowerLimitPercent = 0, 
            float stopLossPoints = 0, int optionType = 0, int optionIndex = 0, float candleTimeFrameInMins = 5, 
            CandleType candleType = CandleType.Time, decimal arg1 = 0, decimal arg2 = 0,
            decimal arg3 = 0, decimal arg4 = 0, decimal arg5 = 0, bool positionSizing = false, decimal maxLossPerTrade = 0)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand insertCMD = new SqlCommand("CreateAlgoInstance", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            insertCMD.Parameters.AddWithValue("@AlgoID", Convert.ToInt32(algoIndex));
            insertCMD.Parameters.AddWithValue("@BToken", (Int64)bToken);
            insertCMD.Parameters.AddWithValue("@TimeStamp", timeStamp);
            insertCMD.Parameters.AddWithValue("@Expiry", expiry);
            insertCMD.Parameters.AddWithValue("@InitialQtyInLotSize", initialQtyInLotsSize);
            insertCMD.Parameters.AddWithValue("@MaxQtyInLotSize", maxQtyInLotSize);
            insertCMD.Parameters.AddWithValue("@StepQtyInLotSize", stepQtyInLotSize);
            insertCMD.Parameters.AddWithValue("@UpperLimit", upperLimit);
            insertCMD.Parameters.AddWithValue("@UpperLimitPercent", upperLimitPercent);
            insertCMD.Parameters.AddWithValue("@LowerLimit", lowerLimit);
            insertCMD.Parameters.AddWithValue("@LowerLimitPercent", lowerLimitPercent);
            insertCMD.Parameters.AddWithValue("@StopLossPoints", stopLossPoints);
            insertCMD.Parameters.AddWithValue("@OptionType", optionType);
            insertCMD.Parameters.AddWithValue("@OptionIndex", optionIndex);
            insertCMD.Parameters.AddWithValue("@CandleTimeFrame_Mins", candleTimeFrameInMins);
            insertCMD.Parameters.AddWithValue("@CandleType", (int) candleType);

            insertCMD.Parameters.AddWithValue("@Arg1", arg1);
            insertCMD.Parameters.AddWithValue("@Arg2", arg2);
            insertCMD.Parameters.AddWithValue("@Arg3", arg3);
            insertCMD.Parameters.AddWithValue("@Arg4", arg4);
            insertCMD.Parameters.AddWithValue("@Arg5", arg5);
            insertCMD.Parameters.AddWithValue("@PositionSizing", positionSizing);
            insertCMD.Parameters.AddWithValue("@MaxLossPerTrade", maxLossPerTrade);

            insertCMD.Parameters.Add("@AlgoInstance", SqlDbType.Int).Direction = ParameterDirection.Output;

            try
            {
                sqlConnection.Open();
                insertCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)insertCMD.Parameters["@AlgoInstance"].Value;
        }
        public DataSet RetrieveActiveData(AlgoIndex algoIndex)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetActiveData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoIndex);
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }
        public DataSet GetActiveAlgos(AlgoIndex algoIndex)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetActiveAlgosData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoIndex);
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }
        public DataSet GetInstrument(uint instrumentToken)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetInstrument", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@InstrumentToken", Convert.ToInt64(instrumentToken));
            SqlDataAdapter daInstrument = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            sqlConnection.Open();
            DataSet dsInstrument = new DataSet();
            daInstrument.Fill(dsInstrument);
            sqlConnection.Close();

            return dsInstrument;
        }
        public DataSet GetInstrument(DateTime expiry, uint bInstrumentToken, decimal strike, string instrumentType)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("FindInstrument", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            
            selectCMD.Parameters.AddWithValue("@BToken", Convert.ToInt64(bInstrumentToken));
            selectCMD.Parameters.AddWithValue("@Expiry", expiry);
            selectCMD.Parameters.AddWithValue("@Strike", strike);
            selectCMD.Parameters.AddWithValue("@InstrumentType", instrumentType);

            SqlDataAdapter daInstrument = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            sqlConnection.Open();
            DataSet dsInstrument = new DataSet();
            daInstrument.Fill(dsInstrument);
            sqlConnection.Close();

            return dsInstrument;
        }
        public DataSet GetOrders(AlgoIndex algoindex, int orderid = 0)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetOrders", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoindex);
            selectCMD.Parameters.AddWithValue("@OrderId", orderid);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }
        public DataSet LoadInstruments(AlgoIndex algoIndex, DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0)
            //,decimal FromStrikePrice = 0, decimal ToStrikePrice = 0, InstrumentType instrumentType = InstrumentType.ALL)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetInstruments", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoIndex);
            if (expiry.HasValue)
                selectCMD.Parameters.AddWithValue("@ExpiryDate", expiry.Value);

            selectCMD.Parameters.AddWithValue("@BaseInstrumentToken", (Int64)baseInstrumentToken);
            selectCMD.Parameters.AddWithValue("@BaseInstrumentPrice", (decimal)baseInstrumentPrice);
            //selectCMD.Parameters.AddWithValue("@InstrumentType", (int) instrumentType);
            //selectCMD.Parameters.AddWithValue("@FromStrikePrice", FromStrikePrice);
            //selectCMD.Parameters.AddWithValue("@ToStrikePrice", ToStrikePrice);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }


        public DataSet LoadCloseByOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0, 
            decimal maxDistanceFromBInstrument= 500)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetNearByOptions", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@ExpiryDate", expiry.Value);
            selectCMD.Parameters.AddWithValue("@BaseInstrumentToken", (Int64)baseInstrumentToken);
            selectCMD.Parameters.AddWithValue("@BaseInstrumentPrice", baseInstrumentPrice);
            selectCMD.Parameters.AddWithValue("@MaxDistanceFromBInstrument", maxDistanceFromBInstrument);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }
        public DataSet LoadHistoricalTokenVolume(AlgoIndex algoIndex, DateTime startTime,
            DateTime endTime, TimeSpan candleTimeSpan, int numberOfCandles)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetHistoricalMaxCandleVolume", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoIndex);
            selectCMD.Parameters.AddWithValue("@NoOfCandles", numberOfCandles);
            selectCMD.Parameters.AddWithValue("@EndDateTime", endTime);
            selectCMD.Parameters.AddWithValue("@CandleTimeSpan", candleTimeSpan);
            SqlDataAdapter daTokenVolume = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsTokenVolume = new DataSet();
            daTokenVolume.Fill(dsTokenVolume);
            sqlConnection.Close();

            return dsTokenVolume;
        }

        public DataSet GetHistoricalCandlePrices(int numberofCandles, DateTime endDateTime, string tokenList, TimeSpan timeFrame, bool isBaseInstrument = false)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            string storedProc = isBaseInstrument ? "GetCandleClosePricesForBInstrument" : "GetCandleClosePrices";

            SqlCommand selectCMD = new SqlCommand(storedProc, sqlConnection)
            {
                CommandTimeout = 6000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@NoOfCandles", numberofCandles);
            selectCMD.Parameters.AddWithValue("@EndTime", endDateTime);
            selectCMD.Parameters.AddWithValue("@TimeFrame", timeFrame);
            selectCMD.Parameters.AddWithValue("@InstrumentTokenList", tokenList);
            selectCMD.Parameters.AddWithValue("@CandleType", (int)CandleType.Time);

            SqlDataAdapter daTokenVolume = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsTokenVolume = new DataSet();
           daTokenVolume.Fill(dsTokenVolume);
            sqlConnection.Close();

            return dsTokenVolume;
        }

        public DataSet GetHistoricalClosePricesFromCandles(int numberofCandles, DateTime endDateTime, string tokenList, TimeSpan timeFrame)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetClosePricesFromCandles", sqlConnection)
            {
                CommandTimeout = 6000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@NoOfCandles", numberofCandles);
            selectCMD.Parameters.AddWithValue("@EndTime", endDateTime);
            selectCMD.Parameters.AddWithValue("@TimeFrame", timeFrame);
            selectCMD.Parameters.AddWithValue("@InstrumentTokenList", tokenList);
            selectCMD.Parameters.AddWithValue("@CandleType", (int)CandleType.Time);

            SqlDataAdapter daTokenVolume = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsTokenVolume = new DataSet();
            daTokenVolume.Fill(dsTokenVolume);
            sqlConnection.Close();

            return dsTokenVolume;
        }

        

        public Tick GetLastTick(uint instrumentToken)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetLatestTickForInstrument", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@InstrumentToken", (Int64)instrumentToken);
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            DataRow dr = dsInstruments.Tables[0].Rows[0];

            Tick tick = new Tick(){
                InstrumentToken = (uint) dr["InstrumentToken"],
                LastPrice = (decimal)dr["LastPrice"],
                LastQuantity = (uint)dr["LastQuantity"],
                AveragePrice = (decimal)dr["AveragePrice"],
                Volume = (uint)dr["Volume"],
                BuyQuantity = (uint)dr["BuyQuantity"],
                SellQuantity = (uint)dr["SellQuantity"],
                Open = (decimal)dr["AveragePrice"],
                High = (decimal)dr["AveragePrice"],
                Low = (decimal)dr["AveragePrice"],
                Close = (decimal)dr["Close"],
                LastTradeTime = (DateTime)dr["LastTradeTime"],
                OI = (uint) dr["OI"]
                //Daylow = (uint)dr["OIDaylow"],
                //Dayhigh = (uint)dr["OIDayhigh],
                //TimeStamp = (DateTime)dr["TimeStamp]


                ///Backlog details pendin
            };


            return tick;
        }
        public DataSet GetInstruments(string strikePrices, string instrumentTypes)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetInstruments", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@StrikePrices", strikePrices);
            selectCMD.Parameters.AddWithValue("@InstrumentTypes", instrumentTypes);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }


        public DataSet RetrieveActiveStrangleData(AlgoIndex algoIndex)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetActiveStrangleData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoIndex);
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }

        public DataSet RetrieveActivePivotInstruments(AlgoIndex algoIndex)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetActivePivotInstruments", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoIndex);
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }
        public DataSet TestRetrieveActivePivotInstruments(AlgoIndex algoIndex)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("TestGetActivePivotInstruments", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoIndex);
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }
        
        
        public int SaveCandle(object Arg, decimal closePrice,  DateTime CloseTime, decimal? closeVolume, int? downTicks,
                decimal? highPrice, DateTime highTime, decimal? highVolume, uint instrumentToken,
                decimal?  lowPrice, DateTime lowTime, decimal? lowVolume, 
                int maxPriceLevelBuyCount, decimal maxPriceLevelBuyVolume, decimal maxPriceLevelMoney, decimal maxPriceLevelPrice,
                int maxPriceLevelSellCount, decimal maxPriceLevelSellVolume, decimal maxPriceLevelTotalVolume,
                int minPriceLevelBuyCount, decimal minPriceLevelBuyVolume, decimal minPriceLevelMoney, decimal minPriceLevelPrice,
                int minPriceLevelSellCount, decimal minPriceLevelSellVolume, decimal minPriceLevelTotalVolume,
                decimal? openInterest, decimal? openPrice, DateTime openTime, decimal? openVolume, decimal? relativeVolume, 
                CandleStates candleState,
                decimal? totalPrice, int? totalTicks, decimal? totalVolume, int? upTicks, CandleType candleType)
        {
            MarketDAO marketDAO = new MarketDAO();

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("SaveCandle", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            
            updateCMD.Parameters.AddWithValue("@instrumentToken", (Int64)instrumentToken);
            updateCMD.Parameters.AddWithValue("@closePrice", closePrice);
            updateCMD.Parameters.AddWithValue("@CloseTime", CloseTime);
            updateCMD.Parameters.AddWithValue("@closeVolume", closeVolume);
            updateCMD.Parameters.AddWithValue("@downTicks", downTicks);
            updateCMD.Parameters.AddWithValue("@highPrice", highPrice);
            updateCMD.Parameters.AddWithValue("@highTime", highTime);
            updateCMD.Parameters.AddWithValue("@highVolume", highVolume);
            updateCMD.Parameters.AddWithValue("@lowPrice", lowPrice);
            updateCMD.Parameters.AddWithValue("@lowTime", lowTime);
            updateCMD.Parameters.AddWithValue("@lowVolume", lowVolume);
            updateCMD.Parameters.AddWithValue("@maxPriceLevelBuyCount", (int)maxPriceLevelBuyCount);
            updateCMD.Parameters.AddWithValue("@maxPriceLevelBuyVolume", (decimal)maxPriceLevelBuyVolume);
            updateCMD.Parameters.AddWithValue("@maxPriceLevelMoney", maxPriceLevelMoney);
            updateCMD.Parameters.AddWithValue("@maxPriceLevelPrice", maxPriceLevelPrice);
            updateCMD.Parameters.AddWithValue("@maxPriceLevelSellCount", (int)maxPriceLevelSellCount);
            updateCMD.Parameters.AddWithValue("@maxPriceLevelSellVolume", (decimal)maxPriceLevelSellVolume);
            updateCMD.Parameters.AddWithValue("@maxPriceLevelTotalVolume", (decimal)maxPriceLevelTotalVolume);
            updateCMD.Parameters.AddWithValue("@minPriceLevelBuyCount", (int)minPriceLevelBuyCount);
            updateCMD.Parameters.AddWithValue("@minPriceLevelBuyVolume", (decimal)minPriceLevelBuyVolume);
            updateCMD.Parameters.AddWithValue("@minPriceLevelMoney", minPriceLevelMoney);
            updateCMD.Parameters.AddWithValue("@minPriceLevelPrice", minPriceLevelPrice);
            updateCMD.Parameters.AddWithValue("@minPriceLevelSellCount", (int)minPriceLevelSellCount);
            updateCMD.Parameters.AddWithValue("@minPriceLevelSellVolume", (decimal)minPriceLevelSellVolume);
            updateCMD.Parameters.AddWithValue("@minPriceLevelTotalVolume", (decimal)minPriceLevelTotalVolume);
            updateCMD.Parameters.AddWithValue("@openInterest", openInterest);
            updateCMD.Parameters.AddWithValue("@openPrice", openPrice);
            updateCMD.Parameters.AddWithValue("@openTime", openTime);
            updateCMD.Parameters.AddWithValue("@openVolume", openVolume);
            updateCMD.Parameters.AddWithValue("@relativeVolume", (decimal)relativeVolume);
            updateCMD.Parameters.AddWithValue("@totalPrice", totalPrice);
            updateCMD.Parameters.AddWithValue("@totalTicks", totalTicks);
            updateCMD.Parameters.AddWithValue("@totalVolume", totalVolume);
            updateCMD.Parameters.AddWithValue("@upTicks", upTicks);
            updateCMD.Parameters.AddWithValue("@Arg", Arg.ToString());
            updateCMD.Parameters.AddWithValue("@CandleType", (int)candleType);

            int candleID = 0;
            try
            {
                sqlConnection.Open();
                candleID = (int)updateCMD.ExecuteScalar();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return candleID;

        }
        public DataSet LoadCandles(int numberofCandles, CandleType candleType, DateTime endDateTime, uint instrumentToken, TimeSpan timeFrame)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetCandles", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@NoOfCandles", numberofCandles);
            selectCMD.Parameters.AddWithValue("@EndTime",  endDateTime);
            selectCMD.Parameters.AddWithValue("@TimeFrame", timeFrame);
            selectCMD.Parameters.AddWithValue("@InstrumentToken", (Int64) instrumentToken);
            selectCMD.Parameters.AddWithValue("@CandleType", (int)candleType);

            SqlDataAdapter daCandles = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            DataSet dsCandles = new DataSet();
            
            sqlConnection.Open();
            daCandles.Fill(dsCandles);
            sqlConnection.Close();


            return dsCandles;
        }
        public void SaveCandlePriceLevels(int candleId, int candlePriceLevelBuyCount, decimal candlePriceLevelBuyVolume, decimal candlePriceLevelMoney, decimal candlePriceLevelPrice,
                int candlePriceLevelSellCount, decimal candlePriceLevelSellVolume, decimal candlePriceLevelTotalVolume)
        {
            MarketDAO marketDAO = new MarketDAO();

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("SaveCandlePriceLevels", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@CandleId", (Int64)candleId);
            updateCMD.Parameters.AddWithValue("@BuyCount", candlePriceLevelBuyCount);
            updateCMD.Parameters.AddWithValue("@BuyVolume", candlePriceLevelBuyVolume);
            updateCMD.Parameters.AddWithValue("@Money", candlePriceLevelMoney);
            updateCMD.Parameters.AddWithValue("@Price", candlePriceLevelPrice);
            updateCMD.Parameters.AddWithValue("@SellCount", candlePriceLevelSellCount);
            updateCMD.Parameters.AddWithValue("@SellVolume", candlePriceLevelSellVolume);
            updateCMD.Parameters.AddWithValue("@TotalVolume", candlePriceLevelTotalVolume);

            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
        }

        public void UpdateOrder(Order order, int algoInstance)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("UpdateOrder", sqlConnection)
            {
                CommandTimeout = 60,
                CommandType = CommandType.StoredProcedure
            };

            int strategyID = 0;
            if(order.Tag != null)
            {
                try
                {
                    strategyID = Convert.ToInt32(order.Tag);
                }
                catch
                {

                }
            }
            updateCMD.Parameters.AddWithValue("@StrategyID", strategyID);
            updateCMD.Parameters.AddWithValue("@OrderId", order.OrderId);
            updateCMD.Parameters.AddWithValue("@AveragePrice", (decimal)order.AveragePrice);
            updateCMD.Parameters.AddWithValue("@ExchangeOrderId", (DateTime)order.ExchangeTimestamp.Value);
            updateCMD.Parameters.AddWithValue("@ExchangeTimestamp", (DateTime)order.ExchangeTimestamp.Value);
            updateCMD.Parameters.AddWithValue("@FilledQuantity", order.FilledQuantity);
            updateCMD.Parameters.AddWithValue("@InstrumentToken", (Int64)order.InstrumentToken);
            updateCMD.Parameters.AddWithValue("@OrderTimestamp", order.OrderTimestamp.HasValue? order.OrderTimestamp:DateTime.Now);
            updateCMD.Parameters.AddWithValue("@OrderType", order.OrderType);
            updateCMD.Parameters.AddWithValue("@Price", order.Price);
            updateCMD.Parameters.AddWithValue("@ParentOrderId", order.ParentOrderId);
            updateCMD.Parameters.AddWithValue("@PendingQuantity", order.PendingQuantity);
            updateCMD.Parameters.AddWithValue("@Product", order.Product);
            updateCMD.Parameters.AddWithValue("@Quantity", order.Quantity);
            updateCMD.Parameters.AddWithValue("@Status", order.Status);
            updateCMD.Parameters.AddWithValue("@StatusMessage", order.StatusMessage);
            updateCMD.Parameters.AddWithValue("@Tag", order.Tag);
            updateCMD.Parameters.AddWithValue("@TradingSymbol", order.Tradingsymbol);
            updateCMD.Parameters.AddWithValue("@TransactionType", order.TransactionType);
            updateCMD.Parameters.AddWithValue("@Validity", order.Validity);
            updateCMD.Parameters.AddWithValue("@Variety", order.Variety);
            updateCMD.Parameters.AddWithValue("@TriggerPrice", order.TriggerPrice);
            updateCMD.Parameters.AddWithValue("@AlgoIndex", (int) order.AlgoIndex);
            updateCMD.Parameters.AddWithValue("@AlgoInstance", algoInstance);

            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
        }

        public decimal UpdateTrade(int strategyID, UInt32 instrumentToken, decimal averageTradePrice, DateTime? exchangeTimeStamp, 
            string orderId, int qty, string transactionType, AlgoIndex algoIndex, int tradedLot = 0, int triggerID = 0)
        {
            decimal netPnL = 0;
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("UpdateTrade", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            //if (strategyID != 0)
           // {
                updateCMD.Parameters.AddWithValue("@StrategyID", (Int32)strategyID);
           // }
            updateCMD.Parameters.AddWithValue("@instrumentToken", (Int64)instrumentToken);
            updateCMD.Parameters.AddWithValue("@averageTradePrice", (decimal)averageTradePrice);
            updateCMD.Parameters.AddWithValue("@exchangeTimeStamp", (DateTime)exchangeTimeStamp.Value);
            updateCMD.Parameters.AddWithValue("@orderId", orderId);

            updateCMD.Parameters.AddWithValue("@qty", qty);
            updateCMD.Parameters.AddWithValue("@transactionType", transactionType);
            updateCMD.Parameters.AddWithValue("@TradedLot", tradedLot);
            updateCMD.Parameters.AddWithValue("@TriggerID", triggerID);
            updateCMD.Parameters.AddWithValue("@algoIndex", algoIndex);

            try
            {
                sqlConnection.Open();
                netPnL = (decimal)updateCMD.ExecuteScalar();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return netPnL;
        }
        public int StoreBoxData(int boxID, UInt32 firstCallToken, UInt32 secondCallToken, UInt32 firstPutToken, UInt32 secondPutToken,
           decimal firstCallPrice, decimal secondCallPrice, decimal firstPutPrice, decimal secondPutPrice, decimal boxValue)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("StoreBoxData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            if (boxID != 0)
            {
                updateCMD.Parameters.AddWithValue("@BoxID", (Int32)boxID);
            }
            updateCMD.Parameters.AddWithValue("@FirstCallToken", (Int64)firstCallToken);
            updateCMD.Parameters.AddWithValue("@SecondCallToken", (Int64)secondCallToken);
            updateCMD.Parameters.AddWithValue("@FirstPutToken", (Int64)firstPutToken);
            updateCMD.Parameters.AddWithValue("@SecondPutToken", (Int64)secondPutToken);

            updateCMD.Parameters.AddWithValue("@FirstCallPrice", firstCallPrice);
            updateCMD.Parameters.AddWithValue("@SecondCallPrice", secondCallPrice);
            updateCMD.Parameters.AddWithValue("@FirstPutPrice", firstPutPrice);
            updateCMD.Parameters.AddWithValue("@SecondPutPrice", secondPutPrice);

            updateCMD.Parameters.AddWithValue("@BoxValue", boxValue);

            updateCMD.Parameters.Add("@BoxID", SqlDbType.Int, 4).Direction = ParameterDirection.ReturnValue;


            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@BoxID"].Value;
        }

        public DataSet RetrieveBoxData(int boxID)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetBoxData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@BoxId", boxID);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            sqlConnection.Open();
            DataSet dsBox = new DataSet();
            daInstruments.Fill(dsBox);
            sqlConnection.Close();

            return dsBox;
        }

        public DataSet RetrieveActiveStrangles(AlgoIndex algoIndex)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetActiveStrangles", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int) algoIndex);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }

        public List<Instrument> RetrieveBaseInstruments()
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetTicks", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
           
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();


            List<Instrument> binstruments = new List<Instrument>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Token"]);
                instrument.TradingSymbol = Convert.ToString(data["Symbol"]);
                //instrument.Strike = Convert.ToDecimal(data["StrikePrice"]);
                //instrument.Expiry = Convert.ToDateTime(data["ExpiryDate"]);
                //instrument.InstrumentType = Convert.ToString(data["Type"]);
                //instrument.LotSize = Convert.ToUInt32(data["LotSize"]);

                binstruments.Add(instrument);
            }
            return binstruments;
        }

        public decimal RetrieveLastPrice(UInt32 token, DateTime? time = null, bool buyOrder = false)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetLastPrice", sqlConnection)
            {
                CommandTimeout = 1000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@Token", Convert.ToInt64(token));

            if (time != null)
                selectCMD.Parameters.AddWithValue("@Time", time.Value);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            decimal lastPrice;

            sqlConnection.Open();
            lastPrice = (decimal)selectCMD.ExecuteScalar();
            sqlConnection.Close();

            return lastPrice;
            //DepthItem[][] price = GetTickDataFromBacklog(backlog);

            //DepthItem[] bids = price[0];
            //DepthItem[] Offers = price[1];

            //decimal offerPrice = Offers[2].Price != 0 ? Offers[2].Price : Offers[1].Price != 0 ? Offers[1].Price : Offers[0].Price;
            //decimal bidPrice = bids[2].Price != 0 ? bids[2].Price : bids[1].Price != 0 ? bids[1].Price : bids[0].Price;

            //return buyOrder ? offerPrice : bidPrice;
        }
        private DepthItem[][] GetTickDataFromBacklog(string backlogData)
        {
            StringBuilder backlog = new StringBuilder();

            DepthItem[] bids = new DepthItem[5];
            DepthItem[] asks = new DepthItem[5];
            int i = 0;
            foreach (string bklog in backlogData.Split('|'))
            {
                if (bklog != "")
                {
                    bids[i] = new DepthItem();

                    string[] data = bklog.Split(',');
                    bids[i].Price = Convert.ToDecimal(data[1]);
                    bids[i].Orders = Convert.ToUInt32(data[2]);
                    bids[i].Quantity = Convert.ToUInt32(data[3]);

                    asks[i] = new DepthItem();
                    asks[i].Price = Convert.ToDecimal(data[4]);
                    asks[i].Orders = Convert.ToUInt32(data[5]);
                    asks[i].Quantity = Convert.ToUInt32(data[6]);
                    i++;
                }
            }

            DepthItem[][] backlogTicks = new DepthItem[2][];
            backlogTicks[0] = bids;
            backlogTicks[1] = asks;

            return backlogTicks;
        }

        public SortedList<Decimal, Instrument>[] RetrieveNextStrangleNodes(UInt32 baseInstrumentToken, DateTime expiry,
            decimal callStrikePrice, decimal putStrikePrice, int updownboth)
        {

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetNextStrangleNodes", sqlConnection)
            {
                CommandTimeout = 60,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@BToken", Convert.ToInt64(baseInstrumentToken));
            selectCMD.Parameters.AddWithValue("@CStrike", callStrikePrice);
            selectCMD.Parameters.AddWithValue("@PStrike", putStrikePrice);
            selectCMD.Parameters.AddWithValue("@ExpiryDate", expiry);
            selectCMD.Parameters.AddWithValue("@UPDOWNBOTH", updownboth);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            SortedList<Decimal, Instrument>[] dInstruments = new SortedList<decimal, Instrument>[2];

            dInstruments[0] = new SortedList<Decimal, Instrument>();
            dInstruments[1] = new SortedList<Decimal, Instrument>();

            foreach (DataRow data in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32(data["Token"]);
                instrument.TradingSymbol = Convert.ToString(data["Symbol"]);
                instrument.Strike = Convert.ToDecimal(data["StrikePrice"]);
                instrument.Expiry = Convert.ToDateTime(data["ExpiryDate"]);
                instrument.InstrumentType = Convert.ToString(data["Type"]);
                instrument.LotSize = Convert.ToUInt32(data["LotSize"]);
                instrument.BaseInstrumentToken = Convert.ToUInt32(data["BInstrumentToken"]);
                ///TODO:This field is causing delay. See what you can do.
                ///Commenting again. Query it from Zerodha as GetLTP for instrument if this is not updated when required
                // instrument.LastPrice = Convert.ToUInt32(data["LastPrice"]);
                if (instrument.InstrumentType.Trim() == "CE")
                {
                    dInstruments[0].Add(instrument.Strike, instrument);
                }
                else
                {
                    dInstruments[1].Add(instrument.Strike, instrument);
                }

            }
            return dInstruments;
        }


        public DataSet RetrieveNextNodes(UInt32 baseInstrumentToken, string instrumentType,
          decimal currentStrikePrice, DateTime expiry, int updownboth, int noOfRecords = 10)
        {

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetNextNodes", sqlConnection)
            {
                CommandTimeout = 60,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@BToken", Convert.ToInt64(baseInstrumentToken));
            selectCMD.Parameters.AddWithValue("@CEPE", instrumentType);
            selectCMD.Parameters.AddWithValue("@Strike", currentStrikePrice);
            selectCMD.Parameters.AddWithValue("@ExpiryDate", expiry);
            selectCMD.Parameters.AddWithValue("@UPDOWNBOTH", updownboth);
            selectCMD.Parameters.AddWithValue("@NoOfOptions", noOfRecords);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            return dsInstruments;
        }



        public List<string> RetrieveOptionExpiries(UInt32 baseInstrumentToken)
        {

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetOptionExpiries", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@BToken", Convert.ToInt64(baseInstrumentToken));

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsExpiries = new DataSet();
            daInstruments.Fill(dsExpiries);
            sqlConnection.Close();


            List<string> optionExpiries = new List<string>();

            foreach (DataRow data in dsExpiries.Tables[0].Rows)
            {
                DateTime expiry = Convert.ToDateTime(data["ExpiryDate"]);
                string expiryDate = expiry.ToString("yyyy-MM-dd");

                optionExpiries.Add(expiryDate);
            }
            return optionExpiries;
        }
        

        public List<Instrument> RetrieveOptions(UInt32 baseInstrumentToken, DateTime expiryDate)
        {

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetOptionsForBase", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@BToken", Convert.ToInt64(baseInstrumentToken));
            selectCMD.Parameters.AddWithValue("@ExpiryDate", expiryDate);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();


            List<Instrument> options = new List<Instrument>();

            foreach (DataRow optionData in dsInstruments.Tables[0].Rows)
            {
                Instrument instrument = new Instrument();
                instrument.InstrumentToken = Convert.ToUInt32( optionData["Token"]);
                instrument.TradingSymbol = Convert.ToString(optionData["Symbol"]);
                instrument.Strike = Convert.ToDecimal(optionData["StrikePrice"]);
                instrument.Expiry = Convert.ToDateTime(optionData["ExpiryDate"]);
                instrument.InstrumentType = Convert.ToString(optionData["Type"]);
                instrument.LotSize = Convert.ToUInt32(optionData["LotSize"]);

                options.Add(instrument);
            }
            return options;
        }

        public int StoreStrangleInstrumentList(int strategyID = 0, int callToken = 0, 
            int putToken = 0, int callIndex = 0, int putIndex = 0)
        {

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand insertCMD = new SqlCommand("StoreStrangleInstrumentList", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            insertCMD.Parameters.AddWithValue("@StrangleID", strategyID);
            insertCMD.Parameters.AddWithValue("@CallToken", callToken);
            insertCMD.Parameters.AddWithValue("@PutToken", putToken);
            insertCMD.Parameters.AddWithValue("@CallIndex", callIndex);
            insertCMD.Parameters.AddWithValue("@PutIndex", putIndex);

            insertCMD.Parameters.Add("@StrangleID", SqlDbType.Int).Direction = ParameterDirection.ReturnValue;


            sqlConnection.Open();
            insertCMD.ExecuteNonQuery();
            sqlConnection.Close();

            return (int)insertCMD.Parameters["@retValue"].Value;

        }
        public UInt32[] GetInstrumentListToSubscribe(decimal niftyPrice, decimal bankniftyPrice)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetInstrumentListToSubscribe", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@NiftyPrice", niftyPrice);
            selectCMD.Parameters.AddWithValue("@BankNiftyPrice", bankniftyPrice);
            

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };
           

            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();


            List<UInt32> instrumentTokenList = new List<UInt32>();
            foreach (DataRow dr in dsInstruments.Tables[0].Rows)
            {
                instrumentTokenList.Add(Convert.ToUInt32(dr["InstrumentToken"]));
            }
            return instrumentTokenList.ToArray();
        }


        public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, decimal bInstPrice, AlgoIndex algoIndex,
            decimal safetyMargin = 10, double stopLossPoints = 0, int initialQty = 0, int maxQty = 0, int stepQty=0, string ceOrderId="",string peOrderId="", string transactionType="")
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("StoreStrangleData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@CEToken", (Int64)ceToken);
            updateCMD.Parameters.AddWithValue("@PEToken", (Int64)peToken);

            updateCMD.Parameters.AddWithValue("@CEPrice", cePrice);
            updateCMD.Parameters.AddWithValue("@PEPrice", pePrice);
            updateCMD.Parameters.AddWithValue("@BaseInstrumentPrice", bInstPrice);

            updateCMD.Parameters.AddWithValue("@InitialQty", initialQty);
            updateCMD.Parameters.AddWithValue("@MaxQty", maxQty);

            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));

            updateCMD.Parameters.AddWithValue("@StopLossPoints", stopLossPoints);

            updateCMD.Parameters.AddWithValue("@CELowerLimit", stepQty);
            updateCMD.Parameters.AddWithValue("@PELowerLimit", stepQty);
            updateCMD.Parameters.AddWithValue("@TransactionType", transactionType);
            updateCMD.Parameters.AddWithValue("@CEOrderId", ceOrderId);
            updateCMD.Parameters.AddWithValue("@PEOrderId", peOrderId);

            updateCMD.Parameters.Add("@StrangleId", SqlDbType.Int).Direction = ParameterDirection.Output;


            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@StrangleId"].Value;
        }

        public int StoreStrategyData(string tokens, string prices, string qtys, string tags, string transactiontypes, string orderIds, string optionTypes,
            AlgoIndex algoIndex, UInt32 parentToken = 0, decimal parentPrice = 0, int stoplosspoints = 0, int initialQty = 0, int maxQty = 0, int stepQty = 0,
            decimal upperThreshold = 0, decimal lowerThreshold = 0, bool thresholdinpercent = false, string triggerIds="")
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("StoreStrategyData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@Tokens", tokens);
            updateCMD.Parameters.AddWithValue("@Prices", prices);

            updateCMD.Parameters.AddWithValue("@Qtys", qtys);
            updateCMD.Parameters.AddWithValue("@Tags", tags);
            updateCMD.Parameters.AddWithValue("@OptionTypes", optionTypes);
            updateCMD.Parameters.AddWithValue("@TransactionTypes", transactiontypes);
            updateCMD.Parameters.AddWithValue("@OrderIds", orderIds);
            updateCMD.Parameters.AddWithValue("@TriggerIds", triggerIds);

            updateCMD.Parameters.AddWithValue("@ParentToken", (UInt32)parentToken);
            updateCMD.Parameters.AddWithValue("@ParentPrice", (decimal)parentPrice);

            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));

            updateCMD.Parameters.AddWithValue("@StopLossPoints", stoplosspoints);

            updateCMD.Parameters.AddWithValue("@InitialQty", initialQty);
            updateCMD.Parameters.AddWithValue("@MaxQty", maxQty);
            updateCMD.Parameters.AddWithValue("@StepQty", stepQty);
            updateCMD.Parameters.AddWithValue("@UpperThreshold", (decimal)upperThreshold);
            updateCMD.Parameters.AddWithValue("@LowerThreshold", (decimal)lowerThreshold);
            updateCMD.Parameters.AddWithValue("@ThresholdInPercent", (bool)thresholdinpercent);
            updateCMD.Parameters.Add("@StrangleId", SqlDbType.Int).Direction = ParameterDirection.Output;
            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@StrangleId"].Value;
        }
        public int CreateOptionStrategy(AlgoIndex algoIndex, UInt32 parentToken = 0, int stoplosspoints = 0, 
            int initialQty = 0, int maxQty = 0, int stepQty = 0,
            decimal upperThreshold = 0, decimal lowerThreshold = 0, bool thresholdinpercent = false)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("CreateOptionStrategy", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@ParentToken", (UInt32)parentToken);
            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));
            updateCMD.Parameters.AddWithValue("@StopLossPoints", stoplosspoints);
            updateCMD.Parameters.AddWithValue("@InitialQty", initialQty);
            updateCMD.Parameters.AddWithValue("@MaxQty", maxQty);
            updateCMD.Parameters.AddWithValue("@StepQty", stepQty);
            updateCMD.Parameters.AddWithValue("@UpperThreshold", (decimal)upperThreshold);
            updateCMD.Parameters.AddWithValue("@LowerThreshold", (decimal)lowerThreshold);
            updateCMD.Parameters.AddWithValue("@ThresholdInPercent", (bool)thresholdinpercent);
            updateCMD.Parameters.Add("@StrangleId", SqlDbType.Int).Direction = ParameterDirection.Output;
            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@StrangleId"].Value;
        }

        public int StoreStrategyData(string tokens, string prices, string qtys, string tags, string transactiontypes, string orderIds,
            AlgoIndex algoIndex, UInt32 parentToken = 0, decimal parentPrice = 0, int stoplosspoints = 0, int initialQty = 0, int maxQty = 0, int stepQty = 0,
            decimal upperThreshold = 0, decimal lowerThreshold = 0, bool thresholdinpercent = false)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("StoreStrategyData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@Tokens", tokens);
            updateCMD.Parameters.AddWithValue("@Prices", prices);

            updateCMD.Parameters.AddWithValue("@Qtys", qtys);
            updateCMD.Parameters.AddWithValue("@Tags", tags);

            updateCMD.Parameters.AddWithValue("@TransactionTypes", transactiontypes);
            updateCMD.Parameters.AddWithValue("@OrderIds", orderIds);

            updateCMD.Parameters.AddWithValue("@ParentToken", (UInt32)parentToken);
            updateCMD.Parameters.AddWithValue("@ParentPrice", (decimal)parentPrice);

            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));

            updateCMD.Parameters.AddWithValue("@StopLossPoints", stoplosspoints);

            updateCMD.Parameters.Add("@StrangleId", SqlDbType.Int).Direction = ParameterDirection.Output;

            updateCMD.Parameters.AddWithValue("@InitialQty", initialQty);
            updateCMD.Parameters.AddWithValue("@MaxQty", maxQty);
            updateCMD.Parameters.AddWithValue("@StepQty", stepQty);
            updateCMD.Parameters.AddWithValue("@UpperThreshold", (decimal)upperThreshold);
            updateCMD.Parameters.AddWithValue("@LowerThreshold", (decimal)lowerThreshold);
            updateCMD.Parameters.AddWithValue("@ThresholdInPercent", (bool)thresholdinpercent);

            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@StrangleId"].Value;
        }

        public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice,
            AlgoIndex algoIndex, decimal ceMaxProfitPoints = 0, decimal ceMaxLossPoints = 0,
            decimal peMaxProfitPoints = 0, decimal peMaxLossPoints = 0, decimal ceMaxProfitPercent = 0, 
            decimal ceMaxLossPercent = 0, decimal peMaxProfitPercent = 0, decimal peMaxLossPercent = 0,
            double stopLossPoints = 0)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("StoreStrangleData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@CEToken", (Int64)ceToken);
            updateCMD.Parameters.AddWithValue("@PEToken", (Int64)peToken);

            updateCMD.Parameters.AddWithValue("@CEPrice", (decimal)cePrice);
            updateCMD.Parameters.AddWithValue("@PEPrice", (decimal)pePrice);

            updateCMD.Parameters.AddWithValue("@CELowerLimit", ceMaxProfitPoints);
            updateCMD.Parameters.AddWithValue("@CEUpperLimit", ceMaxLossPoints);

            updateCMD.Parameters.AddWithValue("@CELowerPercent", ceMaxProfitPercent);
            updateCMD.Parameters.AddWithValue("@CEUpperPercent", ceMaxLossPercent);


            updateCMD.Parameters.AddWithValue("@PELowerLimit", peMaxProfitPoints);
            updateCMD.Parameters.AddWithValue("@PEUpperLimit", peMaxLossPoints);

            
            updateCMD.Parameters.AddWithValue("@PELowerPercent", peMaxProfitPercent);
            updateCMD.Parameters.AddWithValue("@PEUpperPercent", peMaxLossPercent);

            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));

            updateCMD.Parameters.AddWithValue("@StopLossPoints", stopLossPoints);

            updateCMD.Parameters.Add("@StrangleId", SqlDbType.Int).Direction = ParameterDirection.Output;


            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@StrangleId"].Value;
        }
        public int StoreIndexForMainPain(uint  bToken, DateTime expiry,int strikePriceIncrement, AlgoIndex algoIndex, 
            int tradingQty, decimal maxLossThreshold, decimal profitTarget, DateTime timeOfOrder)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("StoreIndexForMainPain", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@BToken", (Int64)bToken);
            updateCMD.Parameters.AddWithValue("@Expiry", expiry);
            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));
            updateCMD.Parameters.AddWithValue("@TradingQty", tradingQty);
            updateCMD.Parameters.AddWithValue("@StrikePriceIncrement", strikePriceIncrement);
            updateCMD.Parameters.AddWithValue("@MaxLossThreshold", maxLossThreshold);
            updateCMD.Parameters.AddWithValue("@ProfitTarget", profitTarget);
            updateCMD.Parameters.AddWithValue("@TimeOfOrder", timeOfOrder);
            updateCMD.Parameters.Add("@StrangleId", SqlDbType.Int).Direction = ParameterDirection.Output;
            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@StrangleId"].Value;
        }
        

        public int StorePivotInstruments(UInt32 primaryInstrumentToken, DateTime expiry, 
            AlgoIndex algoIndex, int maxlot, int lotunit, PivotFrequency pivotFrequency, int pivotWindow)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("StorePivotInstruments", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@PrimaryInstrumentToken", (Int64)primaryInstrumentToken);
            updateCMD.Parameters.AddWithValue("@Expiry", (DateTime)expiry);

            updateCMD.Parameters.AddWithValue("@MaxLot", (decimal)maxlot);
            updateCMD.Parameters.AddWithValue("@TradingLotUnits", (decimal)lotunit);

            //updateCMD.Parameters.AddWithValue("@CELowerPrice", ceLowerValue);
            updateCMD.Parameters.AddWithValue("@PivotFrequency", (int)pivotFrequency);
            //updateCMD.Parameters.AddWithValue("@CEUpperPrice", ceUpperValue);
            updateCMD.Parameters.AddWithValue("@PivotWindow", pivotWindow);

            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));

            updateCMD.Parameters.Add("@PivotStrategyID", SqlDbType.Int).Direction = ParameterDirection.Output;


            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@PivotStrategyID"].Value;
        }
        public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice,
        AlgoIndex algoIndex, double ceLowerDelta = 0, double ceUpperDelta = 0, double peLowerDelta = 0,
        double peUpperDelta = 0, double stopLossPoints = 0, decimal baseInstrumentPrice = 0)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("StoreStrangleData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@CEToken", (Int64)ceToken);
            updateCMD.Parameters.AddWithValue("@PEToken", (Int64)peToken);

            updateCMD.Parameters.AddWithValue("@CEPrice", (decimal)cePrice);
            updateCMD.Parameters.AddWithValue("@PEPrice", (decimal)pePrice);

            //updateCMD.Parameters.AddWithValue("@CELowerPrice", ceLowerValue);
            updateCMD.Parameters.AddWithValue("@CELowerLimit", ceLowerDelta);
            //updateCMD.Parameters.AddWithValue("@CEUpperPrice", ceUpperValue);
            updateCMD.Parameters.AddWithValue("@CEUpperLimit", ceUpperDelta);

            //updateCMD.Parameters.AddWithValue("@PELowerPrice", peLowerValue);
            updateCMD.Parameters.AddWithValue("@PELowerLimit", peLowerDelta);
            //updateCMD.Parameters.AddWithValue("@PEUpperPrice", peUpperValue);
            updateCMD.Parameters.AddWithValue("@PEUpperLimit", peUpperDelta);
            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));


            updateCMD.Parameters.AddWithValue("@BaseInstrumentPrice", baseInstrumentPrice);
            updateCMD.Parameters.AddWithValue("@StopLossPoints", stopLossPoints);

            updateCMD.Parameters.Add("@StrangleId", SqlDbType.Int).Direction = ParameterDirection.Output;


            try
            {
                sqlConnection.Open();
                updateCMD.ExecuteNonQuery();
                sqlConnection.Close();
            }
            catch (Exception e)
            {
                Logger.LogWrite(e.Message);
            }
            finally
            {
                if (sqlConnection.State == ConnectionState.Open)
                    sqlConnection.Close();
            }
            return (int)updateCMD.Parameters["@StrangleId"].Value;
        }

        public void UpdateListData(int strangleId, UInt32 currentCallToken, UInt32 currentPutToken, UInt32 prevCallToken, UInt32 prevPutToken,
           int currentIndex, int prevIndex, decimal[] currentSellPrice, decimal[] prevBuyPrice, AlgoIndex algoIndex, decimal bInstPrice)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("UpdateStrangle", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@StrangleID", strangleId);
            updateCMD.Parameters.AddWithValue("@CurrentCallToken", Convert.ToInt64(currentCallToken));
            updateCMD.Parameters.AddWithValue("@CurrentPutToken", Convert.ToInt64(currentPutToken));

            updateCMD.Parameters.AddWithValue("@CurrentIndex", currentIndex);
            updateCMD.Parameters.AddWithValue("@PrevIndex", prevIndex);
            
            updateCMD.Parameters.AddWithValue("@PrevCallToken", Convert.ToInt64(prevCallToken));
            updateCMD.Parameters.AddWithValue("@PrevPutToken", Convert.ToInt64(prevPutToken));

            updateCMD.Parameters.AddWithValue("@CurrentCallPrice", currentSellPrice[0]);
            updateCMD.Parameters.AddWithValue("@CurrentPutPrice", currentSellPrice[1]);
            updateCMD.Parameters.AddWithValue("@BaseInstrumentPrice", currentSellPrice[2]);

            updateCMD.Parameters.AddWithValue("@PrevCallPrice", prevBuyPrice[0]);
            updateCMD.Parameters.AddWithValue("@PrevPutPrice", prevBuyPrice[1]);

            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));


            sqlConnection.Open();
            updateCMD.ExecuteNonQuery();
            sqlConnection.Close();
        }
        public void UpdateListData(int strangleId, UInt32 instrumentToken, int instrumentIndex, int previousInstrumentIndex, string instrumentType, 
            UInt32 previousInstrumentToken, decimal currentNodePrice, decimal previousNodePrice, AlgoIndex algoIndex)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("UpdateStrangleData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@StrangleID", strangleId);
            updateCMD.Parameters.AddWithValue("@InstrumentToken", Convert.ToInt64(instrumentToken));
            updateCMD.Parameters.AddWithValue("@InstrumentIndex", instrumentIndex);
            updateCMD.Parameters.AddWithValue("@PreviousInstrumentIndex", previousInstrumentIndex);
            updateCMD.Parameters.AddWithValue("@InstrumentType", instrumentType);
            updateCMD.Parameters.AddWithValue("@PreviousToken", Convert.ToInt64(previousInstrumentToken));
            updateCMD.Parameters.AddWithValue("@CurrentInstrumentPrice", currentNodePrice);
            updateCMD.Parameters.AddWithValue("@PreviousInstrumentPrice", previousNodePrice);
            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));


            sqlConnection.Open();
            updateCMD.ExecuteNonQuery();
            sqlConnection.Close();

        }

        public void UpdateOptionData(int strangleId, UInt32 instrumentToken, string instrumentType,
           decimal lastPrice, decimal currentPrice, AlgoIndex algoIndex)
        {
            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand updateCMD = new SqlCommand("UpdateOptionData", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@StrangleID", strangleId);
            updateCMD.Parameters.AddWithValue("@InstrumentToken", Convert.ToInt64(instrumentToken));
            updateCMD.Parameters.AddWithValue("@InstrumentType", instrumentType);
            updateCMD.Parameters.AddWithValue("@CurrentPrice", currentPrice);
            //updateCMD.Parameters.AddWithValue("@PreviousPrice", previousNodePrice);
            updateCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoIndex));


            sqlConnection.Open();
            updateCMD.ExecuteNonQuery();
            sqlConnection.Close();

        }

        public DataSet GetDailyOHLC(IEnumerable<uint> tokens, DateTime date)
        {
            DataTable dtTokens = new DataTable("Tokens");
            dtTokens.Columns.Add("Token");
            foreach (uint token in tokens)
            {
                dtTokens.Rows.Add(token);
            }

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand selectCMD = new SqlCommand("GetDailyOHLC", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            selectCMD.Parameters.AddWithValue("@Date", date);
            selectCMD.Parameters.AddWithValue("@InstrumentTokenList", dtTokens);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            sqlConnection.Open();
            DataSet dsOHLC = new DataSet();
            daInstruments.Fill(dsOHLC);
            sqlConnection.Close();

            return dsOHLC;
        }
        public void StoreTickData(Queue<Tick> liveTicks)
        {
            DataTable dtLiveTicks = ConvertObjectToDataTable(liveTicks);

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand insertCMD = new SqlCommand("InsertTicks", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            insertCMD.Parameters.AddWithValue("@Ticks", dtLiveTicks);

            sqlConnection.Open();
            insertCMD.ExecuteNonQuery();
            sqlConnection.Close();
        }

        public void StoreInstrumentList(List<Instrument> instruments)
        {
            DataTable dtInstruments = ConvertObjectToDataTable(instruments);

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());
            SqlCommand insertCMD = new SqlCommand("InsertInstruments", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            insertCMD.Parameters.AddWithValue("@Instruments", dtInstruments);

            sqlConnection.Open();
            insertCMD.ExecuteNonQuery();
            sqlConnection.Close();
        }

        public void StoreData(LiveOHLCData liveOHLC)
        {

            //LiveOHLCDataReader ohlcDataReader = new LiveOHLCDataReader(liveOHLC);

            DataSet dsLiveOHLC = ConvertObjectToDataset(liveOHLC);

            //IDataReader dataReader = dsLiveOHLC.Tables[0].CreateDataReader();

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("InsertTransaction", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };

            selectCMD.Parameters.AddWithValue("@InstrumentToken", dsLiveOHLC.Tables["Transaction"].Rows[0]["InstrumentToken"]);
            selectCMD.Parameters.AddWithValue("@Open", dsLiveOHLC.Tables["Transaction"].Rows[0]["Open"]);
            selectCMD.Parameters.AddWithValue("@High", dsLiveOHLC.Tables["Transaction"].Rows[0]["High"]);
            selectCMD.Parameters.AddWithValue("@Low", dsLiveOHLC.Tables["Transaction"].Rows[0]["Low"]);
            selectCMD.Parameters.AddWithValue("@Close", dsLiveOHLC.Tables["Transaction"].Rows[0]["Close"]);
            selectCMD.Parameters.AddWithValue("@Volume", dsLiveOHLC.Tables["Transaction"].Rows[0]["Volume"]);
            selectCMD.Parameters.AddWithValue("@CloseTime", dsLiveOHLC.Tables["Transaction"].Rows[0]["CloseTime"]);
            selectCMD.Parameters.AddWithValue("@OpenTime", dsLiveOHLC.Tables["Transaction"].Rows[0]["OpenTime"]);
            selectCMD.Parameters.AddWithValue("@OBidAskSpread", dsLiveOHLC.Tables["Transaction"].Rows[0]["OBidAskSpread"]);

            sqlConnection.Open();
            Int64 TransactionID = (Int64) selectCMD.ExecuteScalar();
            sqlConnection.Close();

            
            dsLiveOHLC.Tables["OrderBook"].Columns["TransactionID"].DefaultValue = TransactionID;

            // Set up the bulk copy object.
            using (SqlBulkCopy bulkCopy =
                       new SqlBulkCopy(Utility.GetConnectionString()))
            {
                //bulkCopy.DestinationTableName =
                //    "dbo.Transaction";

                //bulkCopy.WriteToServer(dsLiveOHLC.Tables["Transaction"]);

                bulkCopy.DestinationTableName =
                   "dbo.OrderBook";

                bulkCopy.WriteToServer(dsLiveOHLC.Tables["OrderBook"]);
            }
        }

        private DataTable ConvertObjectToDataTable(List<Instrument> instruments)
        {
          DataTable dtInstruments = new DataTable("Instrument");
            dtInstruments.Columns.Add("InstrumentToken", typeof(Int64));
            dtInstruments.Columns.Add("exchange_token", typeof(Int64));
            dtInstruments.Columns.Add("tradingsymbol", typeof(String));
            dtInstruments.Columns.Add("name", typeof(String));
            dtInstruments.Columns.Add("last_price", typeof(Decimal));
            dtInstruments.Columns.Add("expiry", typeof(DateTime));
            dtInstruments.Columns.Add("strike", typeof(Decimal));
            dtInstruments.Columns.Add("tick_size", typeof(Decimal));
            dtInstruments.Columns.Add("lot_size", typeof(Int64));
            dtInstruments.Columns.Add("instrument_type", typeof(String));
            dtInstruments.Columns.Add("segment", typeof(String));
            dtInstruments.Columns.Add("exchange", typeof(String));
            

            foreach (Instrument instrument in instruments)
            {
                if (instrument.InstrumentToken == 0)
                {
                    continue;
                }
                DataRow drInstrument = dtInstruments.NewRow();


                drInstrument["InstrumentToken"] = instrument.InstrumentToken;
                drInstrument["exchange_token"] = instrument.ExchangeToken;
                drInstrument["tradingsymbol"] = instrument.TradingSymbol;
                drInstrument["name"] = instrument.Name;
                drInstrument["last_price"] = instrument.LastPrice;
                if (instrument.Expiry != null)
                {
                    drInstrument["expiry"] = instrument.Expiry;
                }
                else
                {
                    drInstrument["expiry"] = DBNull.Value;
                }

                drInstrument["strike"] = instrument.Strike;
                drInstrument["tick_size"] = instrument.TickSize;
                drInstrument["lot_size"] = instrument.LotSize;
                drInstrument["instrument_type"] = instrument.InstrumentType;
                drInstrument["segment"] = instrument.Segment;
                drInstrument["exchange"] = instrument.Exchange;

                dtInstruments.Rows.Add(drInstrument);
            }

            return dtInstruments;
        }



        private DataTable ConvertObjectToDataTable(Queue<Tick> liveTicks, bool shortenedTick = false)
        {
            uint NIFTY_TOKEN = 256265;
            uint BANK_NIFTY_TOKEN = 260105;

            DataTable dtTicks = new DataTable("Ticks");
            dtTicks.Columns.Add("InstrumentToken", typeof(Int64));
            dtTicks.Columns.Add("LastPrice", typeof(Decimal));
            dtTicks.Columns.Add("LastQuantity", typeof(Int64));
            dtTicks.Columns.Add("AveragePrice", typeof(Decimal));
            dtTicks.Columns.Add("Volume", typeof(Int64));
            dtTicks.Columns.Add("BuyQuantity", typeof(Int64));
            dtTicks.Columns.Add("SellQuantity", typeof(Int64));
            dtTicks.Columns.Add("Open", typeof(Decimal));
            dtTicks.Columns.Add("High", typeof(Decimal));
            dtTicks.Columns.Add("Low", typeof(Decimal));
            dtTicks.Columns.Add("Close", typeof(Decimal));
            dtTicks.Columns.Add("Change", typeof(Decimal));
            dtTicks.Columns.Add("LastTradeTime", typeof(DateTime));
            dtTicks.Columns.Add("BackLogs", typeof(String));
            dtTicks.Columns.Add("OI", typeof(Int32));
            dtTicks.Columns.Add("OIDayHigh", typeof(Int32));
            dtTicks.Columns.Add("OIDayLow", typeof(Int32));
            dtTicks.Columns.Add("TimeStamp", typeof(DateTime));

            foreach (Tick tick in liveTicks)
            {
                if (tick.InstrumentToken == 0)
                {
                    continue;
                }
                DataRow drTick = dtTicks.NewRow();

                drTick["InstrumentToken"] = tick.InstrumentToken;
                drTick["LastPrice"] = tick.LastPrice;
                drTick["Volume"] = tick.Volume;
                drTick["OI"] = tick.OI;

                if (tick.InstrumentToken == NIFTY_TOKEN || tick.InstrumentToken == BANK_NIFTY_TOKEN)
                {
                    tick.LastTradeTime = tick.Timestamp;
                }

                if (tick.LastTradeTime != null)
                {
                    drTick["LastTradeTime"] = tick.LastTradeTime;
                }
                else
                {
                    drTick["LastTradeTime"] = DBNull.Value;
                }
                if (tick.Timestamp != null)
                {
                    drTick["TimeStamp"] = tick.Timestamp;
                }
                else
                {
                    drTick["TimeStamp"] = DBNull.Value;
                }



                if (!shortenedTick)
                {
                    //DATA STOPPED TO REDUCE DB SPACE. THIS CAN BE RE ENABLED LATER.
                    drTick["LastQuantity"] = tick.LastQuantity;
                    drTick["AveragePrice"] = tick.AveragePrice;
                    drTick["BuyQuantity"] = tick.BuyQuantity;
                    drTick["SellQuantity"] = tick.SellQuantity;
                    drTick["Change"] = tick.Change;
                    drTick["BackLogs"] = GetBacklog(tick);
                    drTick["Open"] = tick.Open;
                    drTick["High"] = tick.High;
                    drTick["Low"] = tick.Low;
                    drTick["Close"] = tick.Close;
                    drTick["OIDayHigh"] = tick.OIDayHigh;
                    drTick["OIDayLow"] = tick.OIDayLow;
                }

                dtTicks.Rows.Add(drTick);
            }

            return dtTicks;
        }
        /// <summary>
        /// -- DepthLevel,B1,BV1,BQ1,A1,AV1,AQ1|DepthLevel,B2,BV2,BQ2,A2,AV2,AQ2|...
        /// </summary>
        /// <param name="tick"></param>
        /// <returns></returns>
        private string GetBacklog(Tick tick)
        {
            StringBuilder backlog = new StringBuilder();

            ///TODO: there can be asks and no bid. Correct this below
            if (tick.Bids != null)
            {
                for (int i = 0; i < tick.Bids.Length; i++)
                {
                    DepthItem bidBacklog = tick.Bids[i];
                    DepthItem askBacklog = tick.Offers[i];

                    backlog.AppendFormat("{0},{1},{2},{3},{4},{5},{6}|", i + 1, bidBacklog.Price, bidBacklog.Orders,
                        bidBacklog.Quantity, askBacklog.Price, askBacklog.Orders, askBacklog.Quantity);
                }
            }
            return backlog.ToString();
        }
        private DataSet ConvertObjectToDataset(LiveOHLCData liveOHLC)
        {
            DataSet dsLiveOHLC = new DataSet("LiveOHLC");
            DataTable dtTransaction = new DataTable("Transaction");
            dtTransaction.Columns.Add("InstrumentToken", typeof(Int64));
            dtTransaction.Columns.Add("Open", typeof(Decimal));
            dtTransaction.Columns.Add("High", typeof(Decimal));
            dtTransaction.Columns.Add("Low", typeof(Decimal));
            dtTransaction.Columns.Add("Close", typeof(Decimal));
            dtTransaction.Columns.Add("Volume", typeof(Int64));
            dtTransaction.Columns.Add("CloseTime", typeof(DateTime));
            dtTransaction.Columns.Add("OBidAskSpread", typeof(Int32));
            dtTransaction.Columns.Add("OpenTime", typeof(DateTime));


            DataRow drTransaction = dtTransaction.NewRow();
            drTransaction["InstrumentId"] = liveOHLC.OHLCData.InstrumentToken;
            drTransaction["Open"] = liveOHLC.OHLCData.Open;
            drTransaction["High"] = liveOHLC.OHLCData.High;
            drTransaction["Low"] = liveOHLC.OHLCData.Low;
            drTransaction["Close"] = liveOHLC.OHLCData.Close;
            drTransaction["Volume"] = liveOHLC.OHLCData.Volume;
            drTransaction["CloseTime"] = liveOHLC.OHLCData.CloseTime;
            drTransaction["OBidAskSpread"] = liveOHLC.Bids[3][0].Price - liveOHLC.Offers[3][0].Price;
            drTransaction["OpenTime"] = liveOHLC.OHLCData.OpenTime;
            dtTransaction.Rows.Add(drTransaction);

            DataTable dtOrderBook = new DataTable("OrderBook");
            dtOrderBook.Columns.Add("TransactionID", typeof(Int64));
            dtOrderBook.Columns.Add("Time", typeof(DateTime));
            dtOrderBook.Columns.Add("OHLC", typeof(Byte));
            dtOrderBook.Columns.Add("B1", typeof(Decimal));
            dtOrderBook.Columns.Add("BV1", typeof(Int32));
            dtOrderBook.Columns.Add("B2", typeof(Decimal));
            dtOrderBook.Columns.Add("BV2", typeof(Int32));
            dtOrderBook.Columns.Add("B3", typeof(Decimal));
            dtOrderBook.Columns.Add("BV3", typeof(Int32));
            dtOrderBook.Columns.Add("B4", typeof(Decimal));
            dtOrderBook.Columns.Add("BV4", typeof(Int32));
            dtOrderBook.Columns.Add("B5", typeof(Decimal));
            dtOrderBook.Columns.Add("BV5", typeof(Int32));

            dtOrderBook.Columns.Add("A1", typeof(Decimal));
            dtOrderBook.Columns.Add("AV1", typeof(Int32));
            dtOrderBook.Columns.Add("A2", typeof(Decimal));
            dtOrderBook.Columns.Add("AV2", typeof(Int32));
            dtOrderBook.Columns.Add("A3", typeof(Decimal));
            dtOrderBook.Columns.Add("AV3", typeof(Int32));
            dtOrderBook.Columns.Add("A4", typeof(Decimal));
            dtOrderBook.Columns.Add("AV4", typeof(Int32));
            dtOrderBook.Columns.Add("A5", typeof(Decimal));
            dtOrderBook.Columns.Add("AV5", typeof(Int32));

            

            foreach (byte key in liveOHLC.Bids.Keys)
            {
                DataRow drOrderBook = dtOrderBook.NewRow();

                ///TODO: Put correct value for TransactionID
                //drOrderBook["TransactionID"] = 1;
                drOrderBook["Time"] = DateTime.Now;


                drOrderBook["OHLC"] = key;
                drOrderBook["B1"] = liveOHLC.Bids[key][0].Price;
                drOrderBook["BV1"] = liveOHLC.Bids[key][0].Quantity;
                drOrderBook["B2"] = liveOHLC.Bids[key][1].Quantity;
                drOrderBook["BV2"] = liveOHLC.Bids[key][1].Quantity;
                drOrderBook["B3"] = liveOHLC.Bids[key][2].Price;
                drOrderBook["BV3"] = liveOHLC.Bids[key][2].Quantity;
                drOrderBook["B4"] = liveOHLC.Bids[key][3].Price;
                drOrderBook["BV4"] = liveOHLC.Bids[key][3].Quantity;
                drOrderBook["B5"] = liveOHLC.Bids[key][4].Price;
                drOrderBook["BV5"] = liveOHLC.Bids[key][4].Quantity;

                drOrderBook["A1"] = liveOHLC.Offers[key][0].Price;
                drOrderBook["AV1"] = liveOHLC.Offers[key][0].Quantity;
                drOrderBook["A2"] = liveOHLC.Offers[key][1].Price;
                drOrderBook["AV2"] = liveOHLC.Offers[key][1].Quantity;
                drOrderBook["A3"] = liveOHLC.Offers[key][2].Price;
                drOrderBook["AV3"] = liveOHLC.Offers[key][2].Quantity;
                drOrderBook["A4"] = liveOHLC.Offers[key][3].Price;
                drOrderBook["AV4"] = liveOHLC.Offers[key][3].Quantity;
                drOrderBook["A5"] = liveOHLC.Offers[key][4].Price;
                drOrderBook["AV5"] = liveOHLC.Offers[key][4].Quantity;

                dtOrderBook.Rows.Add(drOrderBook);
            }

            dsLiveOHLC.Tables.Add(dtTransaction);
            dsLiveOHLC.Tables.Add(dtOrderBook);

            return dsLiveOHLC;
        }
        //public class LiveOHLCDataReader : IDataReader
        //{
        //    private LiveOHLCData _object;
        //    private byte _currentIndex = 0;
        //    public LiveOHLCDataReader(LiveOHLCData o)
        //    {
        //        _object = o;
        //    }

        //    public int FieldCount { get { return 28; } }

        //    public string GetName(int i)
        //    {
        //        switch (i)
        //        {
        //            case 0: return "IntrumentToken";
        //            case 1: return "Open";
        //            case 2: return "High";
        //            case 3: return "Low";
        //            case 4: return "Close";
        //            case 5: return "Volume";
        //            case 6: return "OpenTime";
        //            case 7: return "CloseTime";

        //            case 8: return "BidPrice1";
        //            case 9: return "BidQty1";
        //            case 10: return "OfferPrice1";
        //            case 11: return "OfferQty1";

        //            case 12: return "BidPrice2";
        //            case 13: return "BidQty2";
        //            case 14: return "OfferPrice2";
        //            case 15: return "OfferQty2";

        //            case 16: return "BidPrice3";
        //            case 17: return "BidQty3";
        //            case 18: return "OfferPrice3";
        //            case 19: return "OfferQty3";

        //            case 20: return "BidPrice4";
        //            case 21: return "BidQty4";
        //            case 22: return "OfferPrice4";
        //            case 23: return "OfferQty4";

        //            case 24: return "BidPrice5";
        //            case 25: return "BidQty5";
        //            case 26: return "OfferPrice5";
        //            case 27: return "OfferQty5";
        //            case 28: return "OrderBookTypeOHLC";
        //            default: return string.Empty;
        //        }
        //    }
        //    public int GetOrdinal(string name)
        //    {
        //        switch (name)
        //        {
        //            case "IntrumentToken": return 0;
        //            case "Open": return 1;
        //            case "High": return 2;
        //            case "Low": return 3;
        //            case "Close": return 4;
        //            case "Volume": return 5;
        //            case "OpenTime": return 6;
        //            case "CloseTime": return 7;

        //            case "BidPrice1": return 8;
        //            case "BidQty1": return 9;
        //            case "OfferPrice1": return 10;
        //            case "OfferQty1": return 11;

        //            case "BidPrice2": return 12;
        //            case "BidQty2": return 13;
        //            case "OfferPrice2": return 14;
        //            case "OfferQty2": return 15;

        //            case "BidPrice3": return 16;
        //            case "BidQty3": return 17;
        //            case "OfferPrice3": return 18;
        //            case "OfferQty3": return 19;

        //            case "BidPrice4": return 20;
        //            case "BidQty4": return 21;
        //            case "OfferPrice4": return 22;
        //            case "OfferQty4": return 23;

        //            case "BidPrice5": return 24;
        //            case "BidQty5": return 25;
        //            case "OfferPrice5": return 26;
        //            case "OfferQty5": return 27;
        //            case "OrderBookTypeOHLC": return 28;
        //            default: return -1;
        //        }
        //    }
        //    public object GetValue(int i)
        //    {
        //        switch (i)
        //        {
        //            case 0: return _object.OHLCData.InstrumentToken;
        //            case 1: return _object.OHLCData.Open;
        //            case 2: return _object.OHLCData.High;
        //            case 3: return _object.OHLCData.Low;
        //            case 4: return _object.OHLCData.Close;
        //            case 5: return _object.OHLCData.Volume;
        //            case 6: return _object.OHLCData.OpenTime;
        //            case 7: return _object.OHLCData.CloseTime;
        //            case 8: return _object.Bids[_currentIndex][0].Price;
        //            case 9: return _object.Bids[_currentIndex][0].Quantity;
        //            case 10: return _object.Offers[_currentIndex][0].Price;
        //            case 11: return _object.Offers[_currentIndex][0].Quantity;
        //            case 12: return _object.Bids[_currentIndex][1].Price;
        //            case 13: return _object.Bids[_currentIndex][1].Quantity;
        //            case 14: return _object.Offers[_currentIndex][1].Price;
        //            case 15: return _object.Offers[_currentIndex][1].Quantity;
        //            case 16: return _object.Bids[_currentIndex][2].Price;
        //            case 17: return _object.Bids[_currentIndex][2].Quantity;
        //            case 18: return _object.Offers[_currentIndex][2].Price;
        //            case 19: return _object.Offers[_currentIndex][2].Quantity;
        //            case 20: return _object.Bids[_currentIndex][3].Price;
        //            case 21: return _object.Bids[_currentIndex][3].Quantity;
        //            case 22: return _object.Offers[_currentIndex][3].Price;
        //            case 23: return _object.Offers[_currentIndex][3].Quantity;
        //            case 24: return _object.Bids[_currentIndex][4].Price;
        //            case 25: return _object.Bids[_currentIndex][4].Quantity;
        //            case 26: return _object.Offers[_currentIndex][4].Price;
        //            case 27: return _object.Offers[_currentIndex][4].Quantity;
        //            case 28: return _currentIndex;
        //            default: return null;
        //        }
        //    }
        //    public bool Read()
        //    {
        //        if (_currentIndex < 4)
        //        {
        //            _currentIndex++;
        //            return true;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }

        //    public void Close(){return;}
        //    public DataTable GetSchemaTable()
        //    {
        //       return new DataTable();
        //    }


        //}

        public DataSet GetLHSSubMenu(Int16 menuID, short cityID)
        {

            SqlConnection sqlConnection = new SqlConnection(Utility.GetConnectionString());

            SqlCommand selectCMD = new SqlCommand("GetLHSSubMenu", sqlConnection)
            {
                CommandTimeout = 30,
                CommandType = CommandType.StoredProcedure
            };


            SqlParameter sqlMenuID = new SqlParameter("@menuID", SqlDbType.Int)
            {
                Value = menuID
            };
            selectCMD.Parameters.Add(sqlMenuID);


            SqlParameter sqlCityID = new SqlParameter("@CityID", SqlDbType.TinyInt) { Value = cityID };
           
            selectCMD.Parameters.Add(sqlCityID);

            SqlDataAdapter menuDA = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };
            

            sqlConnection.Open();
            DataSet menuDS = new DataSet();
            menuDA.Fill(menuDS);
            sqlConnection.Close();

            return menuDS;

        }
    }
}
