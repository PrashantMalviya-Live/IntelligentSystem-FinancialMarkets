using System.Collections.Generic;
namespace Algorithm.Algorithm
{

    /// <summary>
    /// This class is for individual critera that is selected for placing alerts.
    /// One alert selector will have multiple trigger criteria in AND or OR condition
    /// </summary>
    public class AlertTriggerCriterion
    {
        public int ID { get; set; }

        public Instrument Instrument { get; set; }

        //user who has set this up
        public User2 User { get; set; }

        public IIndicator LHSIndicator { get; set; }

        public int LHSTimeInMinutes { get; set; }

        public Indicator RHSIndicator { get; set; }

        public int RHSTimeInMinutes { get; set; }

        public MATH_OPERATOR MathOperator { get; set; }

        /// <summary>
        /// This signifies, whether this condition is AND or OR to previous condition. First has this has null.
        /// </summary>
        public LOGICAL_CRITERIA LogicalCriteria { get; set; }

        /// <summary>
        /// If logical criteria is yes, then alert trigger criteria will have values.
        /// </summary>
        public AlertTriggerCriterion nextAlertTriggerCriterion { get; set; }

        public bool Triggered()
        {
            LHSIndicator.
            return false;
        }

    }
    

    public enum LOGICAL_CRITERIA
    {
        AND,
        OR,
        NULL
    }
    public enum MATH_OPERATOR
    {
        GREATER_THAN,
        LESS_THAN,
        EQUAL_TO
    }
}
