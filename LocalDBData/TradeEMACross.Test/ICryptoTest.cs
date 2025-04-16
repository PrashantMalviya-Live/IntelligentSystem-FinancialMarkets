using System;
using System.Collections.Generic;
using System.Text;
using GlobalLayer;

namespace LocalDBData.Test
{
    internal interface ICryptoTest
    {
        void OnNext(string channel, string data) { }
    }
}
