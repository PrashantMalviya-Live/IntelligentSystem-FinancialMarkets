using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algos.Utilities.Views.ModelViews
{
    //public class IndcatorListView
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }

    //    //These will be used to display additional paramters for indicator such as length for EMA etc.
    //    //Arg1 is display name of property "Length".
    //    //Arg2 will carry the value.
    //    //public KeyValuePair<string, string>? Arg1 { get; set; }
    //    //public KeyValuePair<string, string>? Arg2 { get; set; }

    //    // String format should be  "id:name:timeinveral;propertyname:value1, value2, value3, value4; propertyname2:value1,value2,value2" 
    //    //Key is name of the property such as lenght, and values are options values.
    //    public Dictionary<string, Dictionary<string, string>> PropertyNameAndValues { get; set; }
    //}
    //public enum IndicatorOperatorType : Byte
    //{
    //    Indicator = 1,
    //    MathOperator = 2,
    //    LogicalOperator = 3,
    //    Bracket = 4
    //}
    //public enum PropertyType : Byte
    //{
    //    MultiSelect = 1,
    //    Input = 2
    //}
    //public class IndicatorOperatorView 
    //{
    //    //this is indicator id or operator id
    //    public string Id { get; set; }
    //    public required string Name { get; set; }

    //    public required IndicatorOperatorType Type { get; set; }
    //    public Dictionary<string, IndicatorOperatorView>? ChildIndicators { get; set; } = new Dictionary<string, IndicatorOperatorView>();

    //    public List<TextComponentProperty>? TextComponentProperties { get; set; } = new List<TextComponentProperty>();

    //    public List<DropDownComponentProperty>? DropDownComponentProperties { get; set; } = new List<DropDownComponentProperty>();
    //}

    //public class TextComponentProperty
    //{
    //    public string Id { get; set; } //eg LengthId
    //    public string Name { get; set; } //"Length"
    //    public string Value { get; set; } //property should be simple text such as in case of length, user can enter any length; or it can be selectable with id and values

    //    public PropertyType Type { get; } = PropertyType.Input;
    //}
    //public class DropDownComponentProperty
    //{
    //    public string Id { get; set; } //eg Timeperiodid
    //    public string Name { get; set; } //"Length" / PROPERTY DISPLAY NAME
    //    //public Dictionary<string, KeyValuePair<string, bool>> Values { get; set; } //property should be simple text such as in case of length, user can enter any length; or it can be selectable with id and values

    //    public string SelectedValue { get; set; } //PROPERTY VALUE ID // THis will be the NAME of selected property. THe group of properties is in a seperate collection that is pulled in the labeldropdown
    //    public Dictionary<string, string> Values { get; set; } //property should be simple text such as in case of length, user can enter any length; or it can be selectable with id and values
    //    //THis key value pair will have id of the properties and bool will show selected value
    //    //Lets say if length were described as dropdown component property, the [{"1,{"5min", true}},{"2,{"15min", false}},{"3,{"20min", false}}]

    //    public PropertyType Type { get; } = PropertyType.MultiSelect;
    //}

}
