using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Algorithms.Utilities
{
    public class Node
    {
        public Node(UInt32 instrumentToken, decimal value)
        {
            InstrumentToken = instrumentToken;
            Value = value;
            PrevNode = null;
            NextNode = null;
        }
        UInt32 InstrumentToken { get; set; }
        decimal Value { get; set; }
        Node PrevNode { get; set; }
        Node NextNode { get; set; }
    }
    public class NodeList
    {
        public NodeList(Node currentNode)
        {
            CurrentNode = currentNode;
        }
        Node CurrentNode { get; set; }
    }
}
