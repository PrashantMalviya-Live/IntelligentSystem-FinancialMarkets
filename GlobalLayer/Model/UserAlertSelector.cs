using System;
using System.Collections.Generic;

namespace GlobalLayer
{
    /// <summary>
    /// Selector for user alerts.
    /// </summary>
    /// JSON: {"UserID":"1", "Criteria":
    /// [{"LogicalOperator", "NA", "Criteria" :  [{"LogicalOperator", "AND",  Criteria : [{"LHSTimeFrame":"5", "LHSIndicator":"EMA", "RHSTimeFrame":"10", "RHAIndicator", "EMA", "Operator", "Greater Than"}]}]},
    /// {"LogicalOperator", "AND", "Criteria" :  {"LogicalOperator", "AND",  "LHSTimeFrame":"5", "LHSIndicator":"EMA", "RHSTimeFrame":"10", "RHAIndicator", "EMA", "Operator", "Greater Than"}}]
    public class UserAlertSelector
    {
        public int ID { get; set; }
        public User2 User { get; set; }
        public bool Active { get; set; }

        public AlertStockUniverse AlertStockUniverse { get; set; }

        //Try with Jsonobject for criterai in .net8
        // you can put user if in the alert trigger criteria itself., then this calss will not be required in some much detail.
        public List<AlertTriggerCriterion> alertTriggerCriterias { get; set; }
        public DateTime TrigerredDate { get; set; }

        //Whether User was updated successfully about the alert
        //The reason for not alerting could be low cash balance, user inactive, or technical issue.
        public bool UserUpdated { get; set; }
    }
}
