using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fish_Player_Tracker
{
    public class Settings
    {
        public static string WebHookFree = "";
        public static string WebHookPro = "";
        public static int index = 0;
        public static int index2 = 0;
        public static float cooldown = 0f;
        public static float cooldown2 = 0f;

        public static string last = string.Empty;
        public static string lastLite = string.Empty;
        public static string lastFree = string.Empty;
        public static string[] regions = new string[] { "US", "USW", "EU" };
        public static string[] cosmetics = new string[] { "LBADE", "LBAGS", "LBAAD", "LBAAK", "LBACP", "LFAAZ", "LBAAZ" };
        public static List<string> roomsPrv = new List<string>
        {

        };
        public static List<string> roomsPub = new List<string>
        {

        };
    }
}
