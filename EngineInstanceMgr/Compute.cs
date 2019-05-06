using System;
using System.Collections.Generic;
using System.Text;

namespace EngineInstanceMgr
{
    public class Compute
    {
        public string Key { get; set; }

        public string IPAddress { get; set; }

        public string Port { get; set; }

        public string Status { get; set; }

        public string LastErrorMessage { get; set; }
    }
}
