using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;
using GlobalLayer;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Metadata.Ecma335;
using Newtonsoft.Json.Linq;
using Microsoft.SqlServer.Server;
using System.Linq;
using static System.Formats.Asn1.AsnWriter;
using Google.Protobuf.WellKnownTypes;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using DBAccess;
//using System.Diagnostics.Metrics;
//using Alachisoft.NCache.Common.Util;

namespace DataAccess
{
    public class SQlDAO : IRDSDAO, ITimeStreamDAO
    {
        private readonly string _connectionString;

        public SQlDAO(string connectionString)
        {
            _connectionString = connectionString;
        }

        public List<string> GetActiveTradingDays(DateTime fromDate, DateTime toDate)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetTradingDays", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@FromDate", fromDate);
            sqlCMD.Parameters.AddWithValue("@ToDate", toDate);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            DataSet dsTradingDays = new DataSet();
            daInstruments.Fill(dsTradingDays);
            sqlConnection.Close();

            List<string> days = new List<string>();

            foreach (DataRow dr in dsTradingDays.Tables[0].Rows)
            {
                days.Add(Convert.ToDateTime(dr[0]).ToShortDateString());
            }

            return days;
        }

        public DateTime GetPreviousTradingDate(DateTime tradeDate, int numberOfDaysPrior, uint token)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetPreviousTradingDate", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@Date", tradeDate);
            sqlCMD.Parameters.AddWithValue("@NumberOfDaysPrior", numberOfDaysPrior);
            sqlCMD.Parameters.AddWithValue("@Token", Convert.ToInt64(token));


            sqlConnection.Open();
            DateTime ptDate = Convert.ToDateTime(sqlCMD.ExecuteScalar());
            sqlConnection.Close();

            return ptDate;
        }

        public DateTime GetPreviousWeeklyExpiry(DateTime tradeDate, int numberofWeeksPrior = 1)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetPreviousWeekExpiry", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@TradeDate", tradeDate);
            sqlCMD.Parameters.AddWithValue("@NumberOfWeeksPrior", numberofWeeksPrior);


            sqlConnection.Open();
            DateTime expiryDate = Convert.ToDateTime(sqlCMD.ExecuteScalar());
            sqlConnection.Close();

            return expiryDate;
        }
        public DateTime GetCurrentMonthlyExpiry(DateTime tradeDate, uint binstrument)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetCurrentMonthExpiry", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@TradeDate", tradeDate);
            sqlCMD.Parameters.AddWithValue("@BInstrument", Convert.ToInt64(binstrument));


            sqlConnection.Open();
            DateTime expiryDate = Convert.ToDateTime(sqlCMD.ExecuteScalar());
            sqlConnection.Close();

            return expiryDate;
        }
        public DateTime GetCurrentWeeklyExpiry(DateTime tradeDate, uint binstrument)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetCurrentWeekExpiry", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@TradeDate", tradeDate);
            sqlCMD.Parameters.AddWithValue("@BInstrument", Convert.ToInt64(binstrument));


            sqlConnection.Open();
            DateTime expiryDate = Convert.ToDateTime(sqlCMD.ExecuteScalar());
            sqlConnection.Close();

            return expiryDate;
        }
        public OHLC GetPreviousDayRange(uint token, DateTime dateTime)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetPreviousDayRange", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@Token", Convert.ToInt64(token));
            sqlCMD.Parameters.AddWithValue("@DateTime", dateTime);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            sqlConnection.Open();
            DataSet dsOHLC = new DataSet();
            daInstruments.Fill(dsOHLC);
            sqlConnection.Close();
            OHLC ohlc = null;
            if (dsOHLC.Tables[0].Rows.Count > 0)
            {
                DataRow dataRow = dsOHLC.Tables[0].Rows[0];

                if (dataRow["Open"] != DBNull.Value && dataRow["High"] != null && dataRow["Low"] != null && dataRow["Close"] !=  null)
                {
                    ohlc = new OHLC()
                    {
                        Open = Convert.ToDecimal(dataRow["Open"]),
                        High = Convert.ToDecimal(dataRow["High"]),
                        Low = Convert.ToDecimal(dataRow["Low"]),
                        Close = Convert.ToDecimal(dataRow["Close"])
                    };
                }
            }
            return ohlc;
        }
        public OHLC GetPriceRange(uint token, DateTime startDateTime, DateTime endDateTime)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetPriceRange", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@Token", Convert.ToInt64(token));
            sqlCMD.Parameters.AddWithValue("@StartDateTime", startDateTime);
            sqlCMD.Parameters.AddWithValue("@EndDateTime", endDateTime);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            sqlConnection.Open();
            DataSet dsOHLC = new DataSet();
            daInstruments.Fill(dsOHLC);
            sqlConnection.Close();

            DataRow dataRow = dsOHLC.Tables[0].Rows[0];

            OHLC ohlc = new OHLC()
            {
                Open = Convert.ToDecimal(dataRow["Open"]),
                High = Convert.ToDecimal(dataRow["High"]),
                Low = Convert.ToDecimal(dataRow["Low"]),
                Close = Convert.ToDecimal(dataRow["Close"])
            };

            return ohlc;
        }

       
        public string GetAccessToken()
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetAccessToken", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.Add("@AccessToken", SqlDbType.VarChar, 100).Direction = ParameterDirection.Output;

            sqlConnection.Open();
            sqlCMD.ExecuteNonQuery();
            sqlConnection.Close();

            return (string)sqlCMD.Parameters["@AccessToken"].Value;
        }

        public DataSet GetAlertTriggersforUser(string userId)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetAlertTriggersforUser", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@UserId", userId);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            DataSet dsAlertTriggers = new DataSet();
            daInstruments.Fill(dsAlertTriggers);
            sqlConnection.Close();

            return dsAlertTriggers;
        }


        public DataSet GetActiveAlertTriggers()
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetActiveAlertTriggers", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            DataSet dsAlertTriggers = new DataSet();
            daInstruments.Fill(dsAlertTriggers);
            sqlConnection.Close();

            return dsAlertTriggers;
        }

        public DataSet GetAlertsbyAlertId(int alertId)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetAlertsbyAlertId", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@AlertId", alertId);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            DataSet dsAlertTriggers = new DataSet();
            daInstruments.Fill(dsAlertTriggers);
            sqlConnection.Close();

            return dsAlertTriggers;
        }

        public DataSet GetGeneratedAlerts(string userId)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetGeneratedAlerts", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@UserId", userId);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            DataSet dsGeneratedAlerts = new DataSet();
            daInstruments.Fill(dsGeneratedAlerts);
            sqlConnection.Close();

            return dsGeneratedAlerts;
        }

        public async Task<decimal> GetUserCreditsAsync(string userId)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetUserCredits", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@UserId", userId);

            decimal userCredit = 0;
            try
            {
                sqlConnection.Open();
                userCredit =(decimal) await sqlCMD.ExecuteScalarAsync();
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
            return userCredit;
        }

        

        public DataSet GetInstrumentList()
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetInstrumentListForAlert", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            DataSet dsInstrumentList = new DataSet();
            daInstruments.Fill(dsInstrumentList);
            sqlConnection.Close();

            return dsInstrumentList;
        }
        public DataSet GetCandleTimeFrames()
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetCandleTimeFrames", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            
            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };


            DataSet dsList = new DataSet();
            daInstruments.Fill(dsList);
            sqlConnection.Close();

            return dsList;
        }
        public DataSet GetIndicatorList()
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetIndicatorList", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            DataSet dsIndicatorList = new DataSet();
            daInstruments.Fill(dsIndicatorList);
            sqlConnection.Close();

            return dsIndicatorList;
        }

        public DataSet GetIndicatorProperties(int indicatorId)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("GetIndicatorProperties", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@IndicatorId", indicatorId);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = sqlCMD
            };

            DataSet dsIndicatorProperties = new DataSet();
            daInstruments.Fill(dsIndicatorProperties);
            sqlConnection.Close();

            return dsIndicatorProperties;
        }

        
            public async Task<int> UpdateGeneratedAlertsAsync(Alert alert)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand insertCMD = new SqlCommand("UpdateGeneratedAlerts", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            insertCMD.Parameters.AddWithValue("@UserId", alert.UserId);
            insertCMD.Parameters.AddWithValue("@AlertId", alert.ID);
            insertCMD.Parameters.AddWithValue("@AlertTriggerId", alert.AlertTriggerID);
            insertCMD.Parameters.AddWithValue("@AlertModes", alert.AlertModes);
            insertCMD.Parameters.AddWithValue("@TriggerDateTime", alert.TriggeredDateTime);

            insertCMD.Parameters.Add("@TriggerId", SqlDbType.Int).Direction = ParameterDirection.Output;

            try
            {
                sqlConnection.Open();
                await insertCMD.ExecuteNonQueryAsync();
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
            return (int)insertCMD.Parameters["@TriggerId"].Value;
        }

        public async Task<int> UpdateAlertTriggerAsync(
            int id, uint instrumentToken, string tradingSymbol, string userid, 
            DateTime setupdate, DateTime startDate, DateTime endDate,  byte numberOfTriggersPerInterval, 
            byte triggerFrequency, byte totalNumberOfTriggers, string alertCriteria)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand insertCMD = new SqlCommand("UpdateAlertTrigger", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            insertCMD.Parameters.AddWithValue("@Id", id);
            insertCMD.Parameters.AddWithValue("@UserId", userid);
            insertCMD.Parameters.AddWithValue("@InstrumentToken", (Int64)instrumentToken);
            insertCMD.Parameters.AddWithValue("@SetUpDate", setupdate);
            insertCMD.Parameters.AddWithValue("@StartDate", startDate);
            insertCMD.Parameters.AddWithValue("@EndDate", endDate);
            insertCMD.Parameters.AddWithValue("@TriggerFrequency", triggerFrequency);
            insertCMD.Parameters.AddWithValue("@NumberOfTriggersPerInterval", numberOfTriggersPerInterval);
            insertCMD.Parameters.AddWithValue("@TotalNumberOfTriggers", totalNumberOfTriggers);
            insertCMD.Parameters.AddWithValue("@TradingSymbol", tradingSymbol);
            insertCMD.Parameters.AddWithValue("@AlertCriteria", alertCriteria);

            insertCMD.Parameters.Add("@AlertTriggerId", SqlDbType.Int).Direction = ParameterDirection.Output;

            try
            {
                sqlConnection.Open();
                await insertCMD.ExecuteNonQueryAsync();
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
            return (int)insertCMD.Parameters["@AlertTriggerId"].Value;
        }
        public bool UpdateUser(User activeUser)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("UpdateUser", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@UserId", activeUser.UserId);
            sqlCMD.Parameters.AddWithValue("@UserName", activeUser.UserName);
            sqlCMD.Parameters.AddWithValue("@Broker", activeUser.Broker);
            sqlCMD.Parameters.AddWithValue("@Email", activeUser.Email);
            sqlCMD.Parameters.AddWithValue("@ApiKey", activeUser.APIKey);
            sqlCMD.Parameters.AddWithValue("@AccessToken", activeUser.AccessToken);

            sqlCMD.Parameters.AddWithValue("@ConsumerKey", activeUser.ConsumerKey);
            sqlCMD.Parameters.AddWithValue("@SessionToken", activeUser.SessionToken);

            sqlCMD.Parameters.AddWithValue("@SID", activeUser.SID);
            sqlCMD.Parameters.AddWithValue("@HsServerId", activeUser.HsServerId);
            sqlCMD.Parameters.AddWithValue("@ApplicationUserId", activeUser.ApplicationUserId);

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

        public void UpdateArg8(int algoInstance, decimal arg8)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("UpdateArg8", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@AlgoInstance", algoInstance);
            sqlCMD.Parameters.AddWithValue("@Arg8", arg8);

            try
            {
                sqlConnection.Open();
                sqlCMD.ExecuteNonQuery();
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
        public void UpdateAlgoParamaters(int algoInstance, decimal upperLimit = 0, decimal lowerLimit = 0, decimal arg1 = 0, decimal arg2 = 0,
            decimal arg3 = 0, decimal arg4 = 0, decimal arg5 = 0, decimal arg6 = 0, decimal arg7 = 0, decimal arg8 = 0, string arg9 = "")
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("UpdateAlgoParameters", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@AlgoInstance", algoInstance);
            if (upperLimit != 0)
            {
                sqlCMD.Parameters.AddWithValue("@UpperLimit", upperLimit);
            }
            if (lowerLimit != 0)
            {
                sqlCMD.Parameters.AddWithValue("@LowerLimit", lowerLimit);
            }
            if (arg1 != 0)
            {
                sqlCMD.Parameters.AddWithValue("@Arg1", arg1);
            }
            if (arg2 != 0)
            {
                sqlCMD.Parameters.AddWithValue("@Arg2", arg2);
            }
            if (arg3 != 0)
            {
                sqlCMD.Parameters.AddWithValue("@Arg3", arg3);
            }
            if (arg4 != 0)
            {
                sqlCMD.Parameters.AddWithValue("@Arg4", arg4);
            }
            if (arg5 != 0)
            {
                sqlCMD.Parameters.AddWithValue("@Arg5", arg5);
            }
            if (arg6 != 0)
            {
                sqlCMD.Parameters.AddWithValue("@Arg6", arg6);
            }
            if (arg7 != 0)
            {
                sqlCMD.Parameters.AddWithValue("@Arg7", arg7);
            }
            if (arg8 != 0)
            {
                sqlCMD.Parameters.AddWithValue("@Arg8", arg8);
            }
            if (arg9 != "")
            {
                sqlCMD.Parameters.AddWithValue("@Arg9", arg9);
            }

            try
            {
                sqlConnection.Open();
                sqlCMD.ExecuteNonQuery();
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
        public bool UpdateCandleScore(int algoInstance, DateTime candleTime, Decimal candlePrice, int score)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand sqlCMD = new SqlCommand("UpdateCandleScore", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            sqlCMD.Parameters.AddWithValue("@AlgoInstance", algoInstance);
            sqlCMD.Parameters.AddWithValue("@CandleCloseTime", candleTime);
            sqlCMD.Parameters.AddWithValue("@CandlePrice", candlePrice);
            sqlCMD.Parameters.AddWithValue("@CandleScore", score);

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
      
        public DataSet GetActiveUser(int brokerId = 0, string userid = "")
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand selectCMD = new SqlCommand("GetActiveUser", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@BrokerId", brokerId);
            selectCMD.Parameters.AddWithValue("@UserId", userid);
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
       
        public DataSet GetUserByApplicationUserId(string userid, int brokerId)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand selectCMD = new SqlCommand("GetUserByApplicationUserId", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            
            selectCMD.Parameters.AddWithValue("@UserId", userid);
            selectCMD.Parameters.AddWithValue("@BrokerId", brokerId);

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
        public DataSet GetActiveApplicationUser(string userid = "")
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand selectCMD = new SqlCommand("GetActiveApplicationUser", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@UserId", userid);
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
            decimal arg3 = 0, decimal arg4 = 0, decimal arg5 = 0, decimal arg6 = 0, decimal arg7 = 0,
            decimal arg8 = 0, string arg9 = "", bool positionSizing = false, decimal maxLossPerTrade = 0)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand insertCMD = new SqlCommand("CreateAlgoInstance", sqlConnection)
            {
                CommandTimeout = 3000,
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
            insertCMD.Parameters.AddWithValue("@Arg6", arg6);
            insertCMD.Parameters.AddWithValue("@Arg7", arg7);
            insertCMD.Parameters.AddWithValue("@Arg8", arg8);
            insertCMD.Parameters.AddWithValue("@Arg9", arg9);
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetActiveData", sqlConnection)
            {
                CommandTimeout = 3000,
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
        public DataSet GetActiveAlgos(AlgoIndex algoIndex, string userId)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetActiveAlgosData", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", (int)algoIndex);
            selectCMD.Parameters.AddWithValue("@UserId", userId);
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
        public DataSet GetInstrument(uint instrumentToken, DateTime? expiry)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetInstrument", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@InstrumentToken", Convert.ToInt64(instrumentToken));
            
            if (expiry.HasValue)
            {
                selectCMD.Parameters.AddWithValue("@Expiry", expiry.Value);
            }
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

        public DataSet LoadAlgoInputs(AlgoIndex algoindex, DateTime fromDate, DateTime toDate)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetAlgoInputs", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@AlgoIndex", Convert.ToInt32(algoindex));
            selectCMD.Parameters.AddWithValue("@FromDate", fromDate);
            selectCMD.Parameters.AddWithValue("@ToDate", toDate);

            SqlDataAdapter daInstrument = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            sqlConnection.Open();
            DataSet dsAlgoInputs = new DataSet();
            daInstrument.Fill(dsAlgoInputs);
            sqlConnection.Close();

            return dsAlgoInputs;
        }

        public DataSet GetInstrument(string tradingSymbol)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetInstrumentBySymbol", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@TradingSymbol", tradingSymbol);
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

        public DataSet GetInstrument(DateTime? expiry, uint bToken, decimal strike, string instrumentType)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("FindInstrument", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            selectCMD.Parameters.AddWithValue("@BToken", (Int64)bToken);
            if (expiry.HasValue)
            {
                selectCMD.Parameters.AddWithValue("@Expiry", expiry.Value);
            }
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

        public DataSet GetOTMInstrument(DateTime? expiry, uint bToken, decimal strike, string instrumentType)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetOTMDInstrument", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            
            selectCMD.Parameters.AddWithValue("@BToken", (Int64)bToken);
            if (expiry.HasValue)
            {
                selectCMD.Parameters.AddWithValue("@Expiry", expiry.Value);
            }
            selectCMD.Parameters.AddWithValue("@BPrice", strike);
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetOrders", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetInstruments", sqlConnection)
            {
                CommandTimeout = 3000,
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

        public DataSet LoadSpreadOptions(DateTime? nearExpiry, DateTime? farExpiry,
            uint baseInstrumentToken, string strikeList)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetSpreadOptions", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@NearExpiry", nearExpiry.Value);
            selectCMD.Parameters.AddWithValue("@FarExpiry", farExpiry.Value);
            selectCMD.Parameters.AddWithValue("@BaseInstrumentToken", (Int64)baseInstrumentToken);
            selectCMD.Parameters.AddWithValue("@StrikeList", strikeList);

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
        public DataSet LoadIndexStocks(uint indexToken)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("LoadIndexStocks", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@IndexToken", (Int64)indexToken);

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
        public DataSet LoadOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0,
            decimal maxDistanceFromBInstrument = 500)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetOptions", sqlConnection)
            {
                CommandTimeout = 3000,
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
        public DataSet LoadCloseByOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0, 
            decimal maxDistanceFromBInstrument= 500)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetNearByOptions", sqlConnection)
            {
                CommandTimeout = 3000,
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
        public DataSet LoadCloseByStraddleOptions(DateTime? expiry, uint baseInstrumentToken, decimal baseInstrumentPrice = 0,
            decimal maxDistanceFromBInstrument = 500)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetNearByStraddleOptions", sqlConnection)
            {
                CommandTimeout = 3000,
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
        public DataSet LoadCloseByExpiryOptions(uint baseInstrumentToken, decimal baseInstrumentPrice,
            decimal maxDistanceFromBInstrument, DateTime currentDate)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetNearByExpiryOptions", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@BaseInstrumentToken", (Int64)baseInstrumentToken);
            selectCMD.Parameters.AddWithValue("@BaseInstrumentPrice", baseInstrumentPrice);
            selectCMD.Parameters.AddWithValue("@MaxDistanceFromBInstrument", maxDistanceFromBInstrument);
            selectCMD.Parameters.AddWithValue("@CurrentDate", currentDate);

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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetHistoricalMaxCandleVolume", sqlConnection)
            {
                CommandTimeout = 3000,
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

        public DataSet GetHistoricalCandlePrices(int numberofCandles, DateTime endDateTime, string tokenList, TimeSpan timeFrame, 
            bool isBaseInstrument = false, CandleType candleType = CandleType.Time, uint vThreshold = 0, uint bToken = 256265)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            

            string storedProc;
            SqlCommand selectCMD = null;
            if (candleType == CandleType.Time)
            {
                storedProc = isBaseInstrument ? "GetCandleClosePricesForBInstrument" : "GetCandleClosePrices";

                selectCMD = new SqlCommand(storedProc, sqlConnection)
                {
                    CommandTimeout = 6000,
                    CommandType = CommandType.StoredProcedure
                };

                selectCMD.Parameters.AddWithValue("@NoOfCandles", numberofCandles);
                selectCMD.Parameters.AddWithValue("@EndTime", endDateTime);
                selectCMD.Parameters.AddWithValue("@TimeFrame", timeFrame);
                selectCMD.Parameters.AddWithValue("@InstrumentTokenList", tokenList);
                selectCMD.Parameters.AddWithValue("@CandleType", (int)CandleType.Time);
                selectCMD.Parameters.AddWithValue("@BInstrument", (Int64) bToken);
            }
            else if (candleType == CandleType.Volume)
            {
                storedProc = "GetVCandleClosePrices";

                selectCMD = new SqlCommand(storedProc, sqlConnection)
                {
                    CommandTimeout = 6000,
                    CommandType = CommandType.StoredProcedure
                };

                selectCMD.Parameters.AddWithValue("@NoOfCandles", numberofCandles);
                selectCMD.Parameters.AddWithValue("@EndTime", endDateTime);
                selectCMD.Parameters.AddWithValue("@VThreshold", Convert.ToInt64(vThreshold));
                selectCMD.Parameters.AddWithValue("@InstrumentToken", Convert.ToInt64(tokenList));
                selectCMD.Parameters.AddWithValue("@CandleType", (int)CandleType.Volume);
            }

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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetLatestTickForInstrument", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetInstruments", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetActiveStrangleData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetActivePivotInstruments", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("TestGetActivePivotInstruments", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("SaveCandle", sqlConnection)
            {
                CommandTimeout = 3000,
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
        public DataSet LoadCandles(int numberofCandles, CandleType candleType, DateTime endDateTime, string instrumentTokenList, TimeSpan timeFrame)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetCandles", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@NoOfCandles", numberofCandles);
            selectCMD.Parameters.AddWithValue("@EndTime",  endDateTime);
            selectCMD.Parameters.AddWithValue("@TimeFrame", timeFrame);
            selectCMD.Parameters.AddWithValue("@InstrumentTokenList", instrumentTokenList);
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("SaveCandlePriceLevels", sqlConnection)
            {
                CommandTimeout = 3000,
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

        public void UpdateAlgoPnl(int algoInstance, decimal pnl)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("UpdateAlgoPnl", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@AlgoInstance", algoInstance);
            updateCMD.Parameters.AddWithValue("@PnL", pnl);

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
        public void DeActivateAlgo(int algoInstance)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("DeActivateAlgo", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

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
        public void DeActivateOrderTrio(OrderTrio orderTrio)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("DeActivateOrderTrio", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@OrderTrioID", orderTrio.Id);

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



        public decimal UpdateOrder(Order order, int algoInstance)
        {
            decimal netpnl = 0;
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("UpdateOrder", sqlConnection)
            {
                CommandTimeout = 3000,
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
            if (order.ExchangeTimestamp != null)
            {
                updateCMD.Parameters.AddWithValue("@ExchangeOrderId", (DateTime)order.ExchangeTimestamp.Value);
                updateCMD.Parameters.AddWithValue("@ExchangeTimestamp", (DateTime)order.ExchangeTimestamp.Value);
            }
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
            updateCMD.Parameters.AddWithValue("@Validity", order.Validity == null ? "DAY": order.Validity);
            updateCMD.Parameters.AddWithValue("@Variety", order.Variety == null ? "" : order.Variety);
            updateCMD.Parameters.AddWithValue("@TriggerPrice", order.TriggerPrice);
            updateCMD.Parameters.AddWithValue("@AlgoIndex", (int) order.AlgoIndex);
            updateCMD.Parameters.AddWithValue("@AlgoInstance", algoInstance);

            try
            {
                sqlConnection.Open();
                object pnl = updateCMD.ExecuteScalar();
                if (pnl != null)
                {
                    netpnl = Convert.ToDecimal(pnl);
                }
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
            return netpnl;
        }

        public decimal UpdateTrade(int strategyID, UInt32 instrumentToken, decimal averageTradePrice, DateTime? exchangeTimeStamp, 
            string orderId, int qty, string transactionType, AlgoIndex algoIndex, int tradedLot = 0, int triggerID = 0)
        {
            decimal netPnL = 0;
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("UpdateTrade", sqlConnection)
            {
                CommandTimeout = 3000,
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

        public decimal InsertOptionIV(uint token1, uint token2, decimal _baseInstrumentPrice, decimal? iv1, decimal lastPrice1, DateTime lastTradeTime1,
         decimal? iv2, decimal lastPrice2, DateTime lastTradeTime2)
        {
            decimal netPnL = 0;
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("InsertOptionIV", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            updateCMD.Parameters.AddWithValue("@Token1", (Int64)token1);
            updateCMD.Parameters.AddWithValue("@Token2", (Int64)token2);
            updateCMD.Parameters.AddWithValue("@BasePrice", (Int64)_baseInstrumentPrice);
            updateCMD.Parameters.AddWithValue("@LastPrice1", lastPrice1);
            updateCMD.Parameters.AddWithValue("@LastPrice2", lastPrice2);
            updateCMD.Parameters.AddWithValue("@IV1", iv1);
            updateCMD.Parameters.AddWithValue("@IV2", iv2);
            updateCMD.Parameters.AddWithValue("@LastTradeTime1", lastTradeTime1);
            updateCMD.Parameters.AddWithValue("@LastTradeTime2", lastTradeTime2);

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
            return netPnL;
        }

        public void InsertHistoricals(Historical historical, int interval = 0)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("InsertHistoricals", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            updateCMD.Parameters.AddWithValue("@TimeStamp", historical.TimeStamp);
            updateCMD.Parameters.AddWithValue("@High", historical.High);
            updateCMD.Parameters.AddWithValue("@Low", historical.Low);
            updateCMD.Parameters.AddWithValue("@Close", historical.Close);
            updateCMD.Parameters.AddWithValue("@Open", historical.Open);
            updateCMD.Parameters.AddWithValue("@Volume", (Int64) historical.Volume);
            updateCMD.Parameters.AddWithValue("@InstrumentToken", (Int64) historical.InstrumentToken);
            updateCMD.Parameters.AddWithValue("@Interval", interval);

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

        public int StoreBoxData(int boxID, UInt32 firstCallToken, UInt32 secondCallToken, UInt32 firstPutToken, UInt32 secondPutToken,
           decimal firstCallPrice, decimal secondCallPrice, decimal firstPutPrice, decimal secondPutPrice, decimal boxValue)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("StoreBoxData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetBoxData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetActiveStrangles", sqlConnection)
            {
                CommandTimeout = 3000,
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
        //0: within day, 1: daily
        public List<Historical> GetHistoricals(uint token, DateTime fromDate, DateTime toDate, int interval = 0)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand selectCMD = new SqlCommand("GetHistoricals", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            selectCMD.Parameters.AddWithValue("@InstrumentToken", (Int64)token);
            selectCMD.Parameters.AddWithValue("@FromDate", fromDate);
            selectCMD.Parameters.AddWithValue("@ToDate", toDate);
            selectCMD.Parameters.AddWithValue("@Interval", interval);

            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };

            List<Historical> historicals = new List<Historical>();

            try
            {
                sqlConnection.Open();
                DataSet dsInstruments = new DataSet();
                daInstruments.Fill(dsInstruments);
                sqlConnection.Close();
                

                foreach (DataRow data in dsInstruments.Tables[0].Rows)
                {
                    Historical historical = new Historical(data);
                    historicals.Add(historical);
                }
                return historicals;

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
            return historicals;
        }

        public List<Instrument> RetrieveBaseInstruments()
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetTicks", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

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

            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetNextStrangleNodes", sqlConnection)
            {
                CommandTimeout = 3000,
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

           // mappedTokens = new Dictionary<uint, uint>();
            SortedList<Decimal, Instrument>[] dInstruments = new SortedList<decimal, Instrument>[2];

            dInstruments[0] = new SortedList<Decimal, Instrument>();
            dInstruments[1] = new SortedList<Decimal, Instrument>();

            dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns["Token"] };
            uint kotakToken = 0;
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
                if (instrument.InstrumentType.Trim().ToLower() == "ce")
                {
                    dInstruments[0].Add(instrument.Strike, instrument);
                }
                else
                {
                    dInstruments[1].Add(instrument.Strike, instrument);
                }

                //if (data["KToken"] != DBNull.Value)
                //{
                //    instrument.KToken = Convert.ToUInt32(data["KToken"]);
                //}
                //dsInstruments.Tables[1].PrimaryKey = 
#if market
                instrument.KToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
#elif BACKTEST
                instrument.KToken = 0;
#endif
                //#if market
                //                                kotakToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
                //#elif local
                //                                kotakToken = 0;
                //#endif
                //mappedTokens.Add(instrument.InstrumentToken, kotakToken);

            }
            return dInstruments;
        }

        public SortedList<Decimal, Instrument>[] RetrieveNextStrangleNodes(UInt32 baseInstrumentToken, DateTime expiry,
            decimal callStrikePrice, decimal putStrikePrice, int updownboth, out Dictionary<uint, uint> mappedTokens)
        {

            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetNextStrangleNodes", sqlConnection)
            {
                CommandTimeout = 3000,
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

            mappedTokens = new Dictionary<uint, uint>();
            SortedList<Decimal, Instrument>[] dInstruments = new SortedList<decimal, Instrument>[2];

            dInstruments[0] = new SortedList<Decimal, Instrument>();
            dInstruments[1] = new SortedList<Decimal, Instrument>();
            uint kotakToken = 0;
            dsInstruments.Tables[1].PrimaryKey = new DataColumn[] { dsInstruments.Tables[1].Columns["Token"] };

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
                if (instrument.InstrumentType.Trim().ToLower() == "ce")
                {
                    dInstruments[0].Add(instrument.Strike, instrument);
                }
                else
                {
                    dInstruments[1].Add(instrument.Strike, instrument);
                }

                //kotakToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
                kotakToken = 0;
                //#if market
                //                kotakToken = Convert.ToUInt32(dsInstruments.Tables[1].Rows.Find(instrument.InstrumentToken)[1]);
                //#elif local
                //                kotakToken = 0;
                //#endif
                mappedTokens.Add(instrument.InstrumentToken, kotakToken);

            }
            return dInstruments;
        }


        public DataSet RetrieveNextNodes(UInt32 baseInstrumentToken, string instrumentType,
          decimal currentStrikePrice, DateTime expiry, int updownboth, int noOfRecords = 5)
        {

            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetNextNodes", sqlConnection)
            {
                CommandTimeout = 3000,
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

            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetOptionExpiries", sqlConnection)
            {
                CommandTimeout = 3000,
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

            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetOptionsForBase", sqlConnection)
            {
                CommandTimeout = 3000,
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

            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand insertCMD = new SqlCommand("StoreStrangleInstrumentList", sqlConnection)
            {
                CommandTimeout = 3000,
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
        public Dictionary<UInt32, String> GetInstrumentListToSubscribe(decimal niftyPrice, decimal bankniftyPrice, 
            decimal finniftyPrice, decimal midcpniftyPrice)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetInstrumentListToSubscribe", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@NiftyPrice", niftyPrice);
            selectCMD.Parameters.AddWithValue("@BankNiftyPrice", bankniftyPrice);
            selectCMD.Parameters.AddWithValue("@FINNiftyPrice", finniftyPrice);
            selectCMD.Parameters.AddWithValue("@MIDCPNiftyPrice", midcpniftyPrice);


            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            Dictionary<UInt32, string> instrumentTokenSymbols = new Dictionary<uint, string>();

            //List<UInt32> instrumentTokenList = new List<UInt32>();
            //foreach (DataRow dr in dsInstruments.Tables[0].Rows)
            //{
            //    instrumentTokenList.Add(Convert.ToUInt32(dr["InstrumentToken"]));
            //}

            foreach (DataRow dr in dsInstruments.Tables[0].Rows)
            {
                instrumentTokenSymbols.Add(Convert.ToUInt32(dr["InstrumentToken"]), Convert.ToString(dr["Symbol"]));
            }
            return instrumentTokenSymbols;//.ToArray();
        }
        public List<UInt32> GetInstrumentTokens(uint indexToken, decimal fromStrike, decimal toStrike, DateTime expiry)
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetInstrumentTokens", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };
            selectCMD.Parameters.AddWithValue("@IndexToken", (Int64) indexToken);
            selectCMD.Parameters.AddWithValue("@FromStrike", fromStrike);
            selectCMD.Parameters.AddWithValue("@ToStrike", toStrike);
            selectCMD.Parameters.AddWithValue("@Expiry", expiry);


            SqlDataAdapter daInstruments = new SqlDataAdapter()
            {
                SelectCommand = selectCMD
            };


            sqlConnection.Open();
            DataSet dsInstruments = new DataSet();
            daInstruments.Fill(dsInstruments);
            sqlConnection.Close();

            List<uint> tokens = new List<uint>();
            foreach(DataRow tokenRow in dsInstruments.Tables[0].Rows)
            {
                tokens.Add(Convert.ToUInt32(tokenRow["InstrumentToken"]));
            }

            return tokens;
        }


        public int UpdateStrangleData(UInt32 ceToken, UInt32 peToken, decimal cePrice, decimal pePrice, decimal bInstPrice, AlgoIndex algoIndex,
            decimal safetyMargin = 10, double stopLossPoints = 0, int initialQty = 0, int maxQty = 0, int stepQty=0, string ceOrderId="",string peOrderId="", string transactionType="")
        {
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("StoreStrangleData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("StoreStrategyData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("CreateOptionStrategy", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("StoreStrategyData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("StoreStrangleData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("StoreIndexForMainPain", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("StorePivotInstruments", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("StoreStrangleData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("UpdateStrangle", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("UpdateStrangleData", sqlConnection)
            {
                CommandTimeout = 3000,
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
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("UpdateOptionData", sqlConnection)
            {
                CommandTimeout = 3000,
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
        public int UpdateOrderTrio(uint optionToken, string mainOrderId, string slOrderId, string tpOrderId, decimal targetprofit, decimal stoploss, 
             DateTime entryTradeTime, decimal baseInstrumentSL, bool tpflag, int algoInstance, bool isActive, int orderTrioId = 0, bool? slflag = null, string indicatorsValue = "")
        { 
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand updateCMD = new SqlCommand("UpdateOrderTrio", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            updateCMD.Parameters.AddWithValue("@InstrumentToken", (Int64) optionToken);
            updateCMD.Parameters.AddWithValue("@MainOrderId", mainOrderId);
            updateCMD.Parameters.AddWithValue("@SLOrderId", slOrderId);
            updateCMD.Parameters.AddWithValue("@TPOrderId", tpOrderId);
            updateCMD.Parameters.AddWithValue("@StopLoss", stoploss);
            updateCMD.Parameters.AddWithValue("@TargetProfit", targetprofit);
            updateCMD.Parameters.AddWithValue("@EntryTradeTime", entryTradeTime);
            updateCMD.Parameters.AddWithValue("@BaseInstrumentStopLoss", baseInstrumentSL);
            updateCMD.Parameters.AddWithValue("@TPFlag", tpflag);
            updateCMD.Parameters.AddWithValue("@SLFlag", slflag);
            updateCMD.Parameters.AddWithValue("@AlgoInstance", algoInstance);
            updateCMD.Parameters.AddWithValue("@IsActive", isActive);
            updateCMD.Parameters.AddWithValue("@IndicatorsValue", indicatorsValue);
            updateCMD.Parameters.AddWithValue("@ID", orderTrioId).Direction = ParameterDirection.InputOutput; 

            sqlConnection.Open();
            updateCMD.ExecuteNonQuery();
            sqlConnection.Close();

            return (int)updateCMD.Parameters["@ID"].Value;

        }

        public DataSet GetDailyOHLC(IEnumerable<uint> tokens, DateTime date)
        {
            DataTable dtTokens = new DataTable("Tokens");
            dtTokens.Columns.Add("Token");
            foreach (uint token in tokens)
            {
                dtTokens.Rows.Add(token);
            }

            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand selectCMD = new SqlCommand("GetDailyOHLC", sqlConnection)
            {
                CommandTimeout = 3000,
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
        public async Task WriteTicksAsync(Queue<Tick> liveTicks)
        {
            DataTable dtLiveTicks = ConvertObjectToDataTable(liveTicks);

            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand insertCMD = new SqlCommand("InsertTicks", sqlConnection)
            {
                CommandTimeout = 3000,
                CommandType = CommandType.StoredProcedure
            };

            insertCMD.Parameters.AddWithValue("@Ticks", dtLiveTicks);

            sqlConnection.Open();
            await insertCMD.ExecuteNonQueryAsync();
            sqlConnection.Close();
        }
        public void StoreTestTickData(List<Tick> liveTicks, bool shortenedTick)
        {
            DataTable dtLiveTicks = ConvertObjectToDataTable(new Queue<Tick>(liveTicks), shortenedTick);

            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand insertCMD = new SqlCommand("InsertTestTicks", sqlConnection)
            {
                CommandTimeout = 3000,
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

            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            SqlCommand insertCMD = new SqlCommand("InsertInstruments", sqlConnection)
            {
                CommandTimeout = 3000,
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

            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("InsertTransaction", sqlConnection)
            {
                CommandTimeout = 3000,
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
                       new SqlBulkCopy(_connectionString))
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
            uint VIX_TOKEN = 264969;

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

                if (tick.InstrumentToken == Convert.ToUInt32(Constants.BANK_NIFTY_TOKEN)
                    || tick.InstrumentToken == Convert.ToUInt32(Constants.NIFTY_TOKEN)
                    || tick.InstrumentToken == Convert.ToUInt32(Constants.FINNIFTY_TOKEN)
                    || tick.InstrumentToken == Convert.ToUInt32(Constants.MIDCPNIFTY_TOKEN)
                    || tick.InstrumentToken == VIX_TOKEN)
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
        public Dictionary<uint, List<PriceTime>> GetTickData(uint btoken, DateTime expiry, decimal fromStrike, decimal toStrike, bool optionsData, bool futuresData, DateTime tradeDate)
        {
            object[] data = new object[19];
            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            Dictionary<uint, List<PriceTime>> tokenPrices = new Dictionary<uint, List<PriceTime>>();
            uint token;
            SqlCommand command = new SqlCommand("GetTickDataTest", sqlConnection)
            {
                CommandTimeout = 300000,
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@BToken", Convert.ToInt64(btoken));
            command.Parameters.AddWithValue("@Date", tradeDate);
            command.Parameters.AddWithValue("@Expiry", expiry);
            command.Parameters.AddWithValue("@FromStrike", fromStrike);
            command.Parameters.AddWithValue("@ToStrike", toStrike);
            command.Parameters.AddWithValue("@FuturesData", futuresData);
            command.Parameters.AddWithValue("@OptionsData", optionsData);

            sqlConnection.Open();
            SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
                reader.GetValues(data);
                token = Convert.ToUInt32(data[1]);
                
                if(tokenPrices.ContainsKey(token))
                {
                    //tokenPrices[token] ??= new List<decimal>();

                    tokenPrices[token].Add(new PriceTime() { LastPrice= (decimal)data[2], TradeTime = data[4] != System.DBNull.Value ? Convert.ToDateTime(data[4]) : DateTime.MinValue });
                }
                else
                {
                    tokenPrices.Add(token, new List<PriceTime> { new PriceTime() { LastPrice = (decimal)data[2], TradeTime = data[4] != System.DBNull.Value ? Convert.ToDateTime(data[4]) : DateTime.MinValue } });
                }
            }
            command.Dispose();
            sqlConnection.Close();
            return tokenPrices;
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

            SqlConnection sqlConnection = new SqlConnection(_connectionString);

            SqlCommand selectCMD = new SqlCommand("GetLHSSubMenu", sqlConnection)
            {
                CommandTimeout = 3000,
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
