using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

using KiteConnect;

namespace AdvanceAlgos.Utilities
{
    public class zSessionState
    {
       
        public zSessionState()
        {
            currentContext = HttpContext.Current;
            OrderedBox = null;
            OrderedBoxValue = 0;
        }
        public static zSessionState Current
        {
            get
            {
                zSessionState session;

                if (currentContext == null || currentContext.Session == null)
                {
                    session = new zSessionState();
                    currentContext.Session["__zSession__"] = session;
                }
                else
                {
                    session = (zSessionState)currentContext.Session["__zSession__"];
                    if (session == null)
                    {
                        session = new zSessionState();
                        currentContext.Session["__zSession__"] = session;
                    }
                }
                return session;
            }
            //set
            //{
            //    HttpContext.Current.Session["__zSession__"] = value;
            //}
        }

        public Dictionary<UInt32, Instrument> OrderedBox { get; set; }
        public decimal OrderedBoxValue;
        private static HttpContext currentContext;
    }
}