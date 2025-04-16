using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Algorithms.Indicators;
using Algos.Utilities.Views.ModelViews;
using Flee.PublicTypes;
using GlobalLayer;
namespace Algorithm.Algorithm
{
    public class AlertTriggerView
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
        public required string UserId { get; set; }

        //Custom message while setting up this alert
        public string Message { get; set; }

        public bool Active { get; set; } = true;

        //Alert criteria will be "indicator:{indicator:properties};operator:[operatorid];indicator:{indicator:properties}"
        public required Dictionary<string, IndicatorOperator> Criteria { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="criteria"></param>
        /// <param name="ioPropertyOptions">Dictionary< PropertyId, Dictionary<propertyoptionsid, propertydisplayvalue>></param>
        /// <returns></returns>
        public Dictionary<string, IndicatorOperator> Unlean(Dictionary<int, Dictionary<string, string>> ioPropertyOptions)
        {
            foreach (var item in Criteria)
            {
                var io = item.Value;

                if (io.DropDownComponentProperties != null)
                {
                    io.DropDownComponentProperties.ForEach(item =>
                    {
                        item.Values = ioPropertyOptions[Convert.ToInt32(item.Id)];
                    });
                }
                if (io.ChildIndicators != null)
                {
                    foreach (var i in io.ChildIndicators)
                    {
                        if (i.Value.DropDownComponentProperties != null)
                        {
                            i.Value.DropDownComponentProperties.ForEach(item =>
                            {
                                item.Values = ioPropertyOptions[Convert.ToInt32(item.Id)];
                            });
                        }
                    }
                }
            }
            return Criteria;
        }
        public Dictionary<string, IndicatorOperator> LeanCriteria
        {
            get
            {
                foreach (var item in Criteria)
                {
                    var io = item.Value;

                    //if (io.Type == IndicatorOperatorType.MathOperator || io.Type == IndicatorOperatorType.Bracket ||
                    //    io.Type == IndicatorOperatorType.LogicalOperator)
                    //{
                    //    //Just using the display value. as this is only used in alert generaton expression
                    //    io.DropDownComponentProperties[0].SelectedValue =
                    //        io.DropDownComponentProperties[0].Values[io.DropDownComponentProperties[0].SelectedValue];
                    //}

                    if (io.DropDownComponentProperties != null)
                    {
                        io.DropDownComponentProperties.ForEach(item =>
                        {
                            item.Values = null;
                        });
                    }
                    if(io.ChildIndicators != null)
                    {
                        foreach(var i in  io.ChildIndicators)
                        {
                            if (i.Value.DropDownComponentProperties != null)
                            {
                                i.Value.DropDownComponentProperties.ForEach(item =>
                                {
                                    item.Values = null;
                                });
                            }
                        }
                    }
                }
                return Criteria;
            }
        }
    }
    public enum IndicatorOperatorType : Byte
    {
        Indicator = 1,
        MathOperator = 2,
        LogicalOperator = 3,
        Bracket = 4
    }
    public enum PropertyType : Byte
    {
        MultiSelect = 1,
        Input = 2
    }
    public class IndicatorOperatorView
    {
        //this is indicator id or operator id
        public int Id { get; set; }
        public required string Name { get; set; }

        public required IndicatorOperatorType Type { get; set; }
        public Dictionary<string, IndicatorOperatorView>? ChildIndicators { get; set; } = new Dictionary<string, IndicatorOperatorView>();

        public List<TextComponentProperty>? TextComponentProperties { get; set; } = new List<TextComponentProperty>();

        public List<DropDownComponentProperty>? DropDownComponentProperties { get; set; } = new List<DropDownComponentProperty>();
    }
    public class IndicatorOperator
    {
        //this is indicator id or operator id
        public int Id { get; set; }
        public required string Name { get; set; }

        public required IndicatorOperatorType Type { get; set; }
        public Dictionary<string, IndicatorOperator>? ChildIndicators { get; set; } = new Dictionary<string, IndicatorOperator>();

        public List<TextComponentProperty>? TextComponentProperties { get; set; } = new List<TextComponentProperty>();

        public List<DropDownComponentProperty>? DropDownComponentProperties { get; set; } = new List<DropDownComponentProperty>();
    }
    public class IndicatorOperationPropertyOptions
    {
        public int Id { get; set; }
        public int IndicatorId { get; set; }
        public int PropertyId { get; set; }
        
        Dictionary<string, string> PropertyOptions; // PropertyDisplayValue, PropertyValue
    }

    public class TextComponentProperty
    {
        public string Id { get; set; } //eg LengthId
        public string Name { get; set; } //"Length"

        public string Value { get; set; } //property should be simple text such as in case of length, user can enter any length; or it can be selectable with id and values

        public PropertyType Type { get; } = PropertyType.Input;
    }
    public class DropDownComponentProperty
    {
        public string Id { get; set; } //eg Timeperiodid
        public string Name { get; set; } //"Length" / PROPERTY DISPLAY NAME
                                         //public Dictionary<string, KeyValuePair<string, bool>> Values { get; set; } //property should be simple text such as in case of length, user can enter any length; or it can be selectable with id and values


        public string SelectedValue { get; set; } //PROPERTY VALUE ID // THis will be the NAME of selected property. THe group of properties is in a seperate collection that is pulled in the labeldropdown
        public Dictionary<string, string> Values { get; set; } //property should be simple text such as in case of length, user can enter any length; or it can be selectable with id and values
                                                               //THis key value pair will have id of the properties and bool will show selected value
                                                               //Lets say if length were described as dropdown component property, the [{"1,{"5min", true}},{"2,{"15min", false}},{"3,{"20min", false}}]

        public PropertyType Type { get; } = PropertyType.MultiSelect;
    }
    public class AlertCriterion2
    {
        public int Id { get; set; }
        public string LHSIndicator { get; set; }

        public string LHSTimeInMinutes { get; set; }

        public string RHSIndicator { get; set; }

        public string RHSTimeInMinutes { get; set; }

        public string MathOperator { get; set; }

        /// <summary>
        /// This signifies, whether this condition is AND or OR to previous condition. First has this has null.
        /// </summary>
        public string LogicalCriteria { get; set; } = "1";

    }

    public class AlertTriggerData
    {
        public int ID { get; set; }
        public uint InstrumentToken { get; set; }
        public string TradingSymbol { get; set; }

        public DateTime SetupDate { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public byte TriggerFrequency { get; set; }
        public byte NumberOfTriggersPerInterval { get; set; }
        public byte TotalNumberOfTriggers { get; set; }


        //user who has set this up
        //public User2 User { get; set; }
        public string UserId { get; set; }

        //public Dictionary<string, AlertCriterion> AlertCriteria { get; set; }
        public List<AlertCriterion> AlertCriteria { get; set; }

        public Flee.PublicTypes.IDynamicExpression DynamicExpression { get; set; }

        public bool Triggered()
        {
            bool triggered = false;


            foreach (AlertCriterion c in AlertCriteria)
            {
                //triggered = triggered && c.Triggered();

                triggered = (bool)c.AlertExpression.Evaluate();
            }

            //foreach (var alertCriterion in AlertCriteria)
            //{
            //    if (alertCriterion.LogicalCriteria == LOGICAL_CRITERIA.OR || alertCriterion.LogicalCriteria == LOGICAL_CRITERIA.NULL)
            //    {
            //        triggered = alertCriterion.Triggered();
            //    }
            //    else if (triggered && alertCriterion.LogicalCriteria == LOGICAL_CRITERIA.AND)
            //    {
            //        triggered = triggered && alertCriterion.Triggered();
            //    }

            //}

            return triggered;
        }
    }

    //public class AlertTrigger
    //{
    //    public int ID { get; set; }
    //    public uint InstrumentToken { get; set; }
    //    public string TradingSymbol { get; set; }

    //    //user who has set this up
    //    public User2 User { get; set; }

    //    //
    //    //public List<AlertCriterion> AlertCriteria { get; set; }

    //    //Json string to store alerts
    //    public string AlertCriteria { get; set; }


    //    public bool Triggered()
    //    {
    //        bool triggered = false;
    //        foreach (var criterion in JsonArray.Parse(AlertCriteria).AsArray())
    //        {
    //            var alertCriterion = criterion.Deserialize<AlertCriterion>();

    //            if (alertCriterion.LogicalCriteria == LOGICAL_CRITERIA.OR || alertCriterion.LogicalCriteria == LOGICAL_CRITERIA.NULL)
    //            {
    //                triggered = alertCriterion.Triggered();
    //            }
    //            else if (triggered && alertCriterion.LogicalCriteria == LOGICAL_CRITERIA.AND)
    //            {
    //                triggered = triggered && alertCriterion.Triggered();
    //            }

    //        }

    //        return triggered;
    //    }
    //}

    public class AlertCriterion
    {
        public int Id { get; set; }

        //Indicator can now access another indicator as IIndicator has virtual method called child indicator in it.
        //All indicator and operators are here
        public List<IEquationComponent> Components { get; set; }

        public Flee.PublicTypes.IDynamicExpression AlertExpression { get; set; }

        //public IIndicator LHSIndicator { get; set; }

        //public int LHSTimeInMinutes { get; set; }

        //public IIndicator RHSIndicator { get; set; }

        //public int RHSTimeInMinutes { get; set; }

        //public MATH_OPERATOR MathOperator { get; set; }

        ///// <summary>
        ///// This signifies, whether this condition is AND or OR to previous condition. First has this has null.
        ///// </summary>
        //public LOGICAL_CRITERIA LogicalCriteria { get; set; }
        //public bool Triggered1()
        //{
        //    bool triggered = false;

        //    ExpressionContext context = new ExpressionContext();
        //    context.Variables["a"]  = 

        //    return triggered;
        //}

        public bool Triggered()
        {
            bool triggered = false;

            ExpressionParser p = new ExpressionParser();
            var vars = new Dictionary<String, Double>();
            List<char> expression = new List<char>();


            char[] variableCharArray = "abcdefghijklmnopqrstuvwxyz".ToCharArray();

            if (Components != null && Components.Count > 20)
            {
                throw new InvalidOperationException("Too many variables");
            }
            for (int i = 0; i < Components.Count; i++)
            {
                IEquationComponent c = Components[i];
                if (c.GetType().IsAssignableTo(typeof(IIndicator)))
                {
                    expression.Add(variableCharArray[i]);

                    //vars.Add("x", );
                }
                if (c.GetType().IsAssignableTo(typeof(MathOperator)))
                {
                    switch (((MathOperator)c).Name)
                    {
                        case nameof(MathOperator.GREATER_THAN):
                            {
                                break;
                            }
                            //case MathOperator.GREATER_THAN:
                            //    {
                            //        break;
                            //    }
                            //case MathOperator.GREATER_THAN:
                            //    {
                            //        break;
                            //    }
                            //case MathOperator.GREATER_THAN:
                            //    {
                            //        break;
                            //    }
                    }

                    vars.Add("x", 2.50);
                }





            }
            //System.out.println(p.eval(" 5 + 6 * x - 1", vars));

            //    if (LHSIndicator.IsFormed && RHSIndicator.IsFormed)
            //    {
            //        switch (MathOperator)
            //        {
            //            case MATH_OPERATOR.GREATER_THAN:
            //                {
            //                    //decimal lhsIndicatorValue = 0;
            //                    //decimal rhsIndicatorValue = 0;
            //                    //if(LHSIndicator.GetType() ==  typeof(RangeBreakoutRetraceIndicator))
            //                    //{

            //                    //}
            //                    if (LHSIndicator.GetCurrentValue<decimal>() > RHSIndicator.GetCurrentValue<decimal>())
            //                    {
            //                        //if (LogicalCriteria == LOGICAL_CRITERIA.AND)
            //                        //{
            //                        //    nextAlertTriggerCriterion.Triggered();
            //                        //}
            //                        triggered = true;
            //                    }
            //                    break;

            //                }
            //            case MATH_OPERATOR.LESS_THAN:
            //                {
            //                    if (LHSIndicator.GetCurrentValue<decimal>() < RHSIndicator.GetCurrentValue<decimal>())
            //                    {
            //                        //if (LogicalCriteria == LOGICAL_CRITERIA.AND)
            //                        //{
            //                        //    nextAlertTriggerCriterion.Triggered();
            //                        //}
            //                        triggered = true;
            //                    }
            //                    break;
            //                }
            //            case MATH_OPERATOR.EQUAL_TO:
            //                {
            //                    if (LHSIndicator.GetCurrentValue<decimal>() == RHSIndicator.GetCurrentValue<decimal>())
            //                    {
            //                        //if (LogicalCriteria == LOGICAL_CRITERIA.AND)
            //                        //{
            //                        //    nextAlertTriggerCriterion.Triggered();
            //                        //}
            //                        triggered = true;
            //                    }
            //                    break;
            //                }
            //        }
            //    }

            return triggered;
        }
    }
    public class ExpressionParser
    {
        public double Eval(String exp, Dictionary<String, Double> vars)
        {
            int bracketCounter = 0;
            int operatorIndex = -1;
            var cexp = exp.ToCharArray();
            for (int i = 0; i < exp.Length; i++)
            {
                char c = cexp[i];
                if (c == '(') bracketCounter++;
                else if (c == ')') bracketCounter--;
                else if ((c == '+' || c == '-') && bracketCounter == 0)
                {
                    operatorIndex = i;
                    break;
                }
                else if ((c == '*' || c == '/') && bracketCounter == 0 && operatorIndex < 0)
                {
                    operatorIndex = i;
                }
            }
            if (operatorIndex < 0)
            {
                exp = exp.Trim();
                cexp = exp.ToCharArray();
                if (cexp[0] == '(' && cexp[exp.Length - 1] == ')')
                    return Eval(exp.Substring(1, exp.Length - 1), vars);
                else if (vars.ContainsKey(exp))
                    return vars[exp];
                else
                    return Double.Parse(exp);
            }
            else
            {
                switch (exp.ToCharArray()[operatorIndex])
                {
                    case '+':
                        return Eval(exp.Substring(0, operatorIndex), vars) + Eval(exp.Substring(operatorIndex + 1), vars);
                    case '-':
                        return Eval(exp.Substring(0, operatorIndex), vars) - Eval(exp.Substring(operatorIndex + 1), vars);
                    case '*':
                        return Eval(exp.Substring(0, operatorIndex), vars) * Eval(exp.Substring(operatorIndex + 1), vars);
                    case '/':
                        return Eval(exp.Substring(0, operatorIndex), vars) / Eval(exp.Substring(operatorIndex + 1), vars);
                }
            }
            return 0;
        }
    }


    //public class AlertCriterion
    //{
    //    public int Id { get; set; }

    //    //Indicator can now access another indicator as IIndicator has virtual method called child indicator in it.
    //    public IIndicator LHSIndicator { get; set; }

    //    public int LHSTimeInMinutes { get; set; }

    //    public IIndicator RHSIndicator { get; set; }

    //    public int RHSTimeInMinutes { get; set; }

    //    public MATH_OPERATOR MathOperator { get; set; }

    //    /// <summary>
    //    /// This signifies, whether this condition is AND or OR to previous condition. First has this has null.
    //    /// </summary>
    //    public LOGICAL_CRITERIA LogicalCriteria { get; set; }


    //    public bool Triggered()
    //    {
    //        bool triggered = false;
    //        if (LHSIndicator.IsFormed && RHSIndicator.IsFormed)
    //        {
    //            switch (MathOperator)
    //            {
    //                case MATH_OPERATOR.GREATER_THAN:
    //                    {
    //                        //decimal lhsIndicatorValue = 0;
    //                        //decimal rhsIndicatorValue = 0;
    //                        //if(LHSIndicator.GetType() ==  typeof(RangeBreakoutRetraceIndicator))
    //                        //{

    //                        //}
    //                        if (LHSIndicator.GetCurrentValue<decimal>() > RHSIndicator.GetCurrentValue<decimal>())
    //                        {
    //                            //if (LogicalCriteria == LOGICAL_CRITERIA.AND)
    //                            //{
    //                            //    nextAlertTriggerCriterion.Triggered();
    //                            //}
    //                            triggered = true;
    //                        }
    //                        break;

    //                    }
    //                case MATH_OPERATOR.LESS_THAN:
    //                    {
    //                        if (LHSIndicator.GetCurrentValue<decimal>() < RHSIndicator.GetCurrentValue<decimal>())
    //                        {
    //                            //if (LogicalCriteria == LOGICAL_CRITERIA.AND)
    //                            //{
    //                            //    nextAlertTriggerCriterion.Triggered();
    //                            //}
    //                            triggered = true;
    //                        }
    //                        break;
    //                    }
    //                case MATH_OPERATOR.EQUAL_TO:
    //                    {
    //                        if (LHSIndicator.GetCurrentValue<decimal>() == RHSIndicator.GetCurrentValue<decimal>())
    //                        {
    //                            //if (LogicalCriteria == LOGICAL_CRITERIA.AND)
    //                            //{
    //                            //    nextAlertTriggerCriterion.Triggered();
    //                            //}
    //                            triggered = true;
    //                        }
    //                        break;
    //                    }
    //            }
    //        }

    //        return triggered;
    //    }
    //}

    /// <summary>
    /// This class is for individual critera that is selected for placing alerts.
    /// One alert selector will have multiple trigger criteria in AND or OR condition
    /// </summary>
    //public class AlertTriggerCriterion
    //{
    //    public int ID { get; set; }

    //    //public Instrument Instrument { get; set; }
    //    public uint InstrumentToken{ get; set; }

    //    //user who has set this up
    //    public User2 User { get; set; }

    //    public IIndicator LHSIndicator { get; set; }

    //    public int LHSTimeInMinutes { get; set; }

    //    public IIndicator RHSIndicator { get; set; }

    //    public int RHSTimeInMinutes { get; set; }

    //    public MATH_OPERATOR MathOperator { get; set; }

    //    /// <summary>
    //    /// This signifies, whether this condition is AND or OR to previous condition. First has this has null.
    //    /// </summary>
    //    public LOGICAL_CRITERIA LogicalCriteria { get; set; }

    //    /// <summary>
    //    /// If logical criteria is yes, then alert trigger criteria will have values.
    //    /// </summary>
    //    public AlertTriggerCriterion nextAlertTriggerCriterion { get; set; }

    //    public bool Triggered()
    //    {
    //        bool triggered = false;
    //        if (LHSIndicator.IsFormed && RHSIndicator.IsFormed)
    //        {
    //            switch (MathOperator)
    //            {
    //                case MATH_OPERATOR.GREATER_THAN:
    //                    {
    //                        if (LHSIndicator.GetCurrentValue<decimal>() > RHSIndicator.GetCurrentValue<decimal>())
    //                        {
    //                            if (LogicalCriteria == LOGICAL_CRITERIA.AND)
    //                            {
    //                                nextAlertTriggerCriterion.Triggered();
    //                            }
    //                            triggered = true;
    //                        }
    //                        break;

    //                    }
    //                case MATH_OPERATOR.LESS_THAN:
    //                    {
    //                        if (LHSIndicator.GetCurrentValue<decimal>() < RHSIndicator.GetCurrentValue<decimal>())
    //                        {
    //                            if (LogicalCriteria == LOGICAL_CRITERIA.AND)
    //                            {
    //                                nextAlertTriggerCriterion.Triggered();
    //                            }
    //                            triggered = true;
    //                        }
    //                        break;
    //                    }
    //                case MATH_OPERATOR.EQUAL_TO:
    //                    {
    //                        if (LHSIndicator.GetCurrentValue<decimal>() == RHSIndicator.GetCurrentValue<decimal>())
    //                        {
    //                            if (LogicalCriteria == LOGICAL_CRITERIA.AND)
    //                            {
    //                                nextAlertTriggerCriterion.Triggered();
    //                            }
    //                            triggered = true;
    //                        }
    //                        break;
    //                    }
    //            }
    //        }

    //        return triggered;
    //    }

    //}

    public abstract class Enumeration : IComparable, IEquationComponent
    {
        public string Name { get; private set; }

        public int Id { get; private set; }

        protected Enumeration(int id, string name) => (Id, Name) = (id, name);

        public override string ToString() => Name;

        public static IEnumerable<T> GetAll<T>() where T : Enumeration =>
            typeof(T).GetFields(BindingFlags.Public |
                                BindingFlags.Static |
                                BindingFlags.DeclaredOnly)
                     .Select(f => f.GetValue(null))
                     .Cast<T>();

        public override bool Equals(object obj)
        {
            if (obj is not Enumeration otherValue)
            {
                return false;
            }

            var typeMatches = GetType().Equals(obj.GetType());
            var valueMatches = Id.Equals(otherValue.Id);

            return typeMatches && valueMatches;
        }

        public int CompareTo(object other) => Id.CompareTo(((Enumeration)other).Id);

    }
    public class MathOperator
    : Enumeration
    {
        public static MathOperator GREATER_THAN = new(1, nameof(GREATER_THAN));
        public static MathOperator GREATER_THAN_OR_EQUAL_TO = new(2, nameof(GREATER_THAN_OR_EQUAL_TO));
        public static MathOperator LESS_THAN = new(3, nameof(LESS_THAN));
        public static MathOperator LESS_THAN_OR_EQUAL_TO = new(4, nameof(LESS_THAN_OR_EQUAL_TO));
        public static MathOperator EQUAL_TO = new(5, nameof(EQUAL_TO));

        public MathOperator(int id, string name)
            : base(id, name)
        {
        }
    }
    public class LogicalOperator
    : Enumeration
    {
        public static LogicalOperator AND = new(1, nameof(AND));
        public static LogicalOperator OR = new(2, nameof(OR));

        public LogicalOperator(int id, string name)
            : base(id, name)
        {
        }
    }
    public class BracketOperator
    : Enumeration
    {
        public static BracketOperator OpeningBracket = new(1, nameof(OpeningBracket));
        public static BracketOperator ClosingBracket = new(2, nameof(ClosingBracket));

        public BracketOperator(int id, string name)
            : base(id, name)
        {
        }
    }

    //public enum LOGICAL_CRITERIA
    //{
    //    AND,
    //    OR
    //}
    //public enum MATH_OPERATOR
    //{
    //    GREATER_THAN,
    //    LESS_THAN,
    //    EQUAL_TO
    //}
}
