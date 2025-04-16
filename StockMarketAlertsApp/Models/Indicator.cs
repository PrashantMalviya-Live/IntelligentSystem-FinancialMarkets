using System.Collections;

namespace StockMarketAlertsApp.Models
{

    public interface IEquationComponent
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public Dictionary<string, string> SelectedItem { get; set; }
    }
    /// <summary>
    /// {Id:EMAid; Name: EMA; Properties: [property1: {id: "lengthid"; name:"length"; values:["1";"2";"3"];selected:"2"}, property2: {id: "subindicatorid"; name:"candle"; property2:{..} values:["1";"2";"3"];selected:"2"}]}
    /// </summary>
    public class IndicatorOperator ///: IEquationComponent
    {
        //this is indicator id or operator id
        public int Id { get; set; }
        public required string Name { get; set; }

        public required IndicatorOperatorType Type { get; set; }
        public Dictionary<string, IndicatorOperator>? ChildIndicators { get; set; } = new Dictionary<string, IndicatorOperator>();

        public List<TextComponentProperty>? TextComponentProperties { get; set; } = new List<TextComponentProperty>();

        public List<DropDownComponentProperty>? DropDownComponentProperties { get; set; } = new List<DropDownComponentProperty>();

        //public int Index { get; set; }
        //These will be used to display additional paramters for indicator such as length for EMA etc.
        //Arg1 is display name of property "Length".
        //Arg2 will carry the value.
        //public KeyValuePair<string, string>? Arg1 { get; set; }
        //public KeyValuePair<string, string>? Arg2 { get; set; }

        // String format should be  "id:name:timeinveral;propertyname:value1, value2, value3, value4; propertyname2:value1,value2,value2" 
        //Key is name of the property such as lenght, and values are options values.
        //public Dictionary<string, Dictionary<string, string>> PropertyNameAndValues { get; set; }

        //public Dictionary<string, string> SelectedItem { get; set; }
    }

    public class TextComponentProperty
    {
        public string Id { get; set; } //eg LengthId
        public string Name { get; set; } //"Length"
        public string Value { get; set; } //property should be simple text such as in case of length, user can enter any length; or it can be selectable with id and values
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
    }


    public class Indicator
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        //These will be used to display additional paramters for indicator such as length for EMA etc.
        //Arg1 is display name of property "Length".
        //Arg2 will carry the value.
        //public KeyValuePair<string, string>? Arg1 { get; set; }
        //public KeyValuePair<string, string>? Arg2 { get; set; }

        // String format should be  "id:name:timeinveral;propertyname:value1, value2, value3, value4; propertyname2:value1,value2,value2" 
        //Key is name of the property such as lenght, and values are options values.
        public Dictionary<string, Dictionary<string, string>> PropertyNameAndValues { get; set; }

    }
    public class IndicatorPropertyOptions
    {
        public int Id { get; set; }
        public int IndicatorId { get; set; }
        public string DisplayName { get; set; }
        public string PropertyName {  get; set; }

        public List<IndicatorPropertiesOptionsValues> IndicatorPropertiesOptionsValues { get; set; }
    }
    public class IndicatorPropertiesOptionsValues
    {
        public int Id { get; set; }
        public int IndicatorId { get; set; }
        public int PropertyId { get; set; }
        public string PropertyOptionDisplayName { get; set; }
        public string PropertyOptionValue { get; set; }

    }
    public class IndicatorPropertiesAndOptionValues
    {
        public int Id { get; set; }
        public int IndicatorId { get; set; }
        public string PropertyOptionDisplayName { get; set; }
        public string PropertyOptionValue { get; set; }

    }
    public enum IndicatorOperatorType
    {
        Indicator = 1,
        MathOperator = 2,
        LogicalOperator = 3,
        Bracket = 4
    }

   
    //public class Operator : IEquationComponent
    //{
    //    public int Id { get; set; }
    //    public required string Name { get; set; }

    //    //These will be used to display additional paramters for indicator such as length for EMA etc.
    //    //Arg1 is display name of property "Length".
    //    //Arg2 will carry the value.
    //    //public KeyValuePair<string, string>? Arg1 { get; set; }
    //    //public KeyValuePair<string, string>? Arg2 { get; set; }

    //    // String format should be  "id:name;operatortype:value1, value2, value3, value4" 
    //    //Key is name of the property such as lenght, and values are options values.
    //    public List<DropDownComponentProperty>? DropDownComponentProperties { get; set; }
    //}
}
