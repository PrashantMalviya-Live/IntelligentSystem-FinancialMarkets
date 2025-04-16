using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobalLayer
{
    public class Constants
    {
        // NCache
        public const string NCACHE_CACHENAME = "DSLocalCache";
        public const string IGNITE_CACHENAME = "DSCache";
        public const string IGNITE_MARKETDATA_CACHE = "DSTickCache";

        // Products
        public const string PRODUCT_MIS = "MIS";
        public const string PRODUCT_CNC = "CNC";
        public const string PRODUCT_NRML = "NRML";
        public const string PRODUCT_SM = "SUPERMULTIPLE";

        public const string KPRODUCT_NRML = "NORMAL";

        // Broker
        public const int ZERODHA = 0;
        public const int KOTAK = 1;

        //Market Alerts
        public const string MARKET_ALERTS = "NSEIndexAlerts";

        // Order types
        public const string ORDER_TYPE_MARKET = "MARKET";
        public const string ORDER_TYPE_LIMIT = "LIMIT";
        public const string ORDER_TYPE_SLM = "SL-M";
        public const string ORDER_TYPE_SL = "SL";


        // Delta Exchange Order types
        public const string DELTAEXCHANGE_ORDER_TYPE_MARKET = "market_order";
        public const string DELTAEXCHANGE_ORDER_TYPE_LIMIT = "limit_order";
        public const string DELTAEXCHANGE_STOP_ORDER_TYPE_SL = "stop_loss_order";
        public const string DELTAEXCHANGE_STOP_ORDER_TYPE_TP = "take_profit_order";
        

        // Order status
        public const string ORDER_STATUS_COMPLETE = "COMPLETE";
        public const string ORDER_STATUS_CANCELLED = "CANCELLED";
        public const string ORDER_STATUS_REJECTED = "REJECTED";
        public const string ORDER_STATUS_OPEN = "OPEN";
        public const string ORDER_STATUS_VALIDATION_PENDING = "VALIDATION PENDING";
        public const string ORDER_STATUS_MODIFY_PENDING = "MODIFY PENDING";
        public const string ORDER_STATUS_MODIFY_VALIDATION_PENDING = "MODIFY VALIDATION PENDING";
        public const string ORDER_STATUS_AMQ_REQ_RECEIVED = "AMO REQ RECEIVED";
        public const string ORDER_STATUS_TRIGGER_PENDING = "TRIGGER PENDING";

        // Kotak Order status
        public const string KORDER_STATUS_TRADED = "complete";
        public const string KORDER_STATUS_CANCELLED = "cancelled";
        public const string KORDER_STATUS_MODIFIED = "modified";
        public const string KORDER_STATUS_REJECTED = "rejected";//"REJ";
        public const string KORDER_STATUS_OPEN = "open";
        public const string KORDER_STATUS_SLM = "slo";

        // Varities
        public const string VARIETY_REGULAR = "regular";
        public const string VARIETY_BO = "bo";
        public const string VARIETY_CO = "co";
        public const string VARIETY_AMO = "amo";

        // Transaction type
        public const string TRANSACTION_TYPE_BUY = "BUY";
        public const string TRANSACTION_TYPE_SELL = "SELL";

        // Validity
        public const string VALIDITY_DAY = "DAY";
        public const string VALIDITY_IOC = "IOC";

        // Exchanges
        public const string EXCHANGE_NSE = "NSE";
        public const string EXCHANGE_BSE = "BSE";
        public const string EXCHANGE_NFO = "NFO";
        public const string EXCHANGE_CDS = "CDS";
        public const string EXCHANGE_BFO = "BFO";
        public const string EXCHANGE_MCX = "MCX";

        // Margins segments
        public const string MARGIN_EQUITY = "equity";
        public const string MARGIN_COMMODITY = "commodity";

        // Ticker modes
        public const string MODE_FULL = "full";
        public const string MODE_QUOTE = "quote";
        public const string MODE_LTP = "ltp";

        //Positions
        public const string POSITION_DAY = "day";
        public const string POSITION_OVERNIGHT = "overnight";

        //Historical intervals
        public const string INTERVAL_MINUTE = "minute";
        public const string INTERVAL_3MINUTE = "3minute";
        public const string INTERVAL_5MINUTE = "5minute";
        public const string INTERVAL_10MINUTE = "10minute";
        public const string INTERVAL_15MINUTE = "15minute";
        public const string INTERVAL_30MINUTE = "30minute";
        public const string INTERVAL_60MINUTE = "60minute";
        public const string INTERVAL_DAY = "day";

        // GTT status
        public const string GTT_ACTIVE = "active";
        public const string GTT_TRIGGERED = "triggered";
        public const string GTT_DISABLED = "disabled";
        public const string GTT_EXPIRED = "expired";
        public const string GTT_CANCELLED = "cancelled";
        public const string GTT_REJECTED = "rejected";
        public const string GTT_DELETED = "deleted";

        // GTT trigger type
        public const string GTT_TRIGGER_OCO = "two-leg";
        public const string GTT_TRIGGER_SINGLE = "single";

        //Kafka
        public const string TOPIC_NAME = "Market_Ticks";
        public const string CONSUMER_GROUP = "Ticks_Consumer";
        public const string BOOTSTRAP_SERVER = "localhost:9092";

        //Price Type
        public const int CURRENT = 0;
        public const int LOW = 1;
        public const int HIGH = 2;

        //app instance for market data service
        public const int MARKET_DATA_SERVICE_INSTANCE = -99;
        public const string HEALTH_CHECK_LOG_LEVEL = "STOPPED";

        public const string NIFTY_TOKEN = "256265";
        public const string BANK_NIFTY_TOKEN = "260105";
        public const string FINNIFTY_TOKEN = "257801";
        public const string MIDCPNIFTY_TOKEN = "288009";


        //Instrument Type
        public const int CE = 0;
        public const int PE = 1;
        public const int FUT = 2;

        //CANDLE INDICATOR
        public const int CANDLE_INDICATOR_ID = 9;
        public class TIBCO
        {
            public const string FTLRealmServer = "http://localhost:8585";
        }

        public static int GetStrikePriceIncrement(uint token)
        {
            int strikePriceIncrement = 100;
            switch (token.ToString())
            {
                case Constants.NIFTY_TOKEN:
                    strikePriceIncrement = 50;
                    break;
                case Constants.BANK_NIFTY_TOKEN:
                    strikePriceIncrement = 100;
                    break;
                case Constants.FINNIFTY_TOKEN:
                    strikePriceIncrement = 50;
                    break;
                case Constants.MIDCPNIFTY_TOKEN:
                    strikePriceIncrement = 25;
                    break;
            }
            return strikePriceIncrement;
        }
        public static int GetLotSize(uint token)
        {
            int lotSize = 100;
            switch (token.ToString())
            {
                case Constants.NIFTY_TOKEN:
                    lotSize = 50;
                    break;
                case Constants.BANK_NIFTY_TOKEN:
                    lotSize = 15;
                    break;
                case Constants.FINNIFTY_TOKEN:
                    lotSize = 40;
                    break;
                case Constants.MIDCPNIFTY_TOKEN:
                    lotSize = 75;
                    break;
            }
            return lotSize;
        }
    }
    
    public class APIPORT
    {
        public const string ExpiryStrangle = "http://*:8080";
        public const string EMACross = "http://*:8081";
        public const string DeltaManager = "http://*:8083";
        public const string StangleValueManager = "http://*:8084";
        public const string OptionOptimizer = "http://*:8085";
        public const string StoreData = "http://*:8086";
        public const string MarketView = "http://*:8087";
        public const string RSICross = "http://*:8090";
        public const string SellOnRSI = "http://*:8089";
        public const string MOMENTUMSTRADDLE = "http://*:8089";
        public const string CryptoAlgos = "http://*:8091";
    }

   

    
}
