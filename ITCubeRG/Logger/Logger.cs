using log4net;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ITCubeRG.Logger
{
    internal static class Logger
    {
        public static ILog Log { get; } = LogManager.GetLogger(typeof(Logger));

        static Logger()
        { 
        
        }
    }
}
