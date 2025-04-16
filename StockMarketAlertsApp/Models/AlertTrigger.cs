namespace StockMarketAlertsApp.Models
{
    public class AlertTrigger
    {
        public int ID { get; set; }
        public required uint InstrumentToken { get; set; }
        public required string TradingSymbol { get; set; }
        public DateTime SetupDate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public byte TriggerFrequency { get; set; }
        public byte NumberOfTriggersPerInterval { get; set; }
        public byte TotalNumberOfTriggers { get; set; }


        //user who has set this up
        public required string UserId { get; set; }
        public bool Active { get; set; } = true;

        //Alert criteria will be "indicator:{indicator:properties};operator:[operatorid];indicator:{indicator:properties}"
        public required Dictionary<string,IndicatorOperator> Criteria { get; set; } 
        
        //public AlertTrigger()
        //{
        //    ID = 1;
        //    InstrumentToken = "260105";
        //    TradingSymbol = "NIFTY50";
        //    User = string.Empty;
        //    Criteria = new List<AlertCriterion>();
        //}
    }


    //public class AlertCriterion
    //{
    //    public int Id { get; set; }

    //    public Queue<>

    //    //value in the indicator comes as propertyname:propertyvaluevalue;propertyname:propertyvalue;propertyname:propertyvaluevalue;propertyname:propertyvalue
    //    //time of the indicator will also come within indicator and there is no need to LHStimeinminutes.
    //    //name, id interval, length, up/down all these are properties
    //    public required string Indicator { get; set; }

    //    public required string Operator { get; set; }

    //    //indicator string format is  name:"name";id:"id";property1name:"property1value";"property2name:property2value"
    //    //in the servercode use reflection to assign property value. This is not very frequent exercise anyways


    //    public void Update(AlertCriterion newAlertCriterion)
    //    {
    //        Indicator = newAlertCriterion.LHSIndicator;
    //        RHSIndicator = newAlertCriterion.RHSIndicator;
    //        LHSTimeInMinutes = newAlertCriterion.LHSTimeInMinutes;
    //        RHSTimeInMinutes = newAlertCriterion.RHSTimeInMinutes;
    //        MathOperator = newAlertCriterion.MathOperator;
    //        LogicalCriteria = newAlertCriterion.LogicalCriteria;
    //    }
    //}
    //public class AlertCriterion
    //{
    //    public int Id { get; set; }

    //    //value in the indicator comes as propertyname:propertyvaluevalue;propertyname:propertyvalue;propertyname:propertyvaluevalue;propertyname:propertyvalue
    //    //time of the indicator will also come within indicator and there is no need to LHStimeinminutes.
    //    //name, id interval, length, up/down all these are properties
    //    public required string LHSIndicator { get; set; }

    //    //indicator string format is  name:"name";id:"id";property1name:"property1value";"property2name:property2value"
    //    //in the servercode use reflection to assign property value. This is not very frequent exercise anyways

    //    public required string LHSTimeInMinutes { get; set; }

    //    //value in the indicator comes as ID;propertyname:propertyvaluevalue;propertyname:propertyvalue;propertyname:propertyvaluevalue;propertyname:propertyvalue
    //    public required string RHSIndicator { get; set; }

    //    public required string RHSTimeInMinutes { get; set; }

    //    public required string MathOperator { get; set; }

    //    /// <summary>
    //    /// This signifies, whether this condition is AND or OR to previous condition. First has this has null.
    //    /// </summary>
    //    public required string LogicalCriteria { get; set; } = "1";

    //    public void Update(AlertCriterion newAlertCriterion)
    //    {
    //        LHSIndicator = newAlertCriterion.LHSIndicator;
    //        RHSIndicator = newAlertCriterion.RHSIndicator;
    //        LHSTimeInMinutes = newAlertCriterion.LHSTimeInMinutes;
    //        RHSTimeInMinutes = newAlertCriterion.RHSTimeInMinutes;
    //        MathOperator = newAlertCriterion.MathOperator;
    //        LogicalCriteria = newAlertCriterion.LogicalCriteria;
    //    }
    //}
}
