using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITCubeRG
{
    public struct AccessToken
    {
        public string Cookies { get; set; }
        public string SessionId { get; set; }

        public AccessToken(string cookies, string sessionId)
        {
            Cookies = cookies;
            SessionId = sessionId;
        }
    }
}
