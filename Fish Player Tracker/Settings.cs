using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fish_Player_Tracker
{
    public class Settings
    {
        public static string WebHookFree = "https://discord.com/api/webhooks/1264705632477384769/KQSz8NQQxyoFkTZgTl_D03NjEvaoJdSPNTr3pPz7i12kTTF_3MXzeOhiVqW4rHszBb45";
        public static string WebHookPro = "https://discord.com/api/webhooks/1264704811408818218/jh0USPbhyjHRvRUpw12iXFhKmaWjzxkxSI84Ojgwim6Vpc5sRJMF3WWvFpoLatTy3tSr";
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
        public static List<string> DontRemoveCodes = new List<string>
        {
            "DEEP", "JUAN", "MELT", "JMAN", "HUNT", "MODS", "GTAG", "VEN1", "VEN2", "RANG", "COMP", "J3VU",
            "PBBV", "ECHO", "1234", "ALEC", "MAXO", "MINI", "ECHO", "FOOT", "GTAG", "IDEN", "ABCD", "AMXR",
            "MOSA", "CODY", "GTC1", "GTC2", "GTC3", "GTC4", "GTC5", "GTC6", "GTC7", "GTC8", "GTC9", "RUN1",
            "FNAF", "ECH0", "BOTS", "DEAD", "MONK", "BODA", "PLAY", "BEES", "NAMO", "HIDE", "RAY2", "RAY1",
            "BARK", "DURF", "ALECVR", "ELLIOT", "QWERTY", "TTTPIG", "MONKEY", "SREN17", "SREN18", "SREN16",
            "555999", "STATUE", "CHIPPD", "123456", "MONKER", "CUBCUB", "STYLED", "JUITAR", "MAJORA", "ANTOCA",
            "STICKS", "IDENVR", "DAPPER", "AUSSIE", "KNINLY", "WIDDOM", "TIKTOK", "THUMBZ", "BANANA", "SMILER",
            "SPIDER", "MODDER", "CREEPY", "SPOOKY", "HELPME", "GRAPES", "JULIAN", "FINGER", "TYLERVR", "ELLIOT1",
            "ELLIOT2", "MINIGAM", "GORILLA", "DAISY09", "DAISY08", "BANSHEE", "1234567", "FAADDUU", "ELLIOT3",
            "SKIBIDI", "BOETHIA", "KISHARK", "UNKNOWN", "MODDERS", "JOLYENE", "CREATOR", "CONTENT", "WARNING",
            "MITTENS", "WEAREVR", "PAINTER", "YOUTUBE", "MODDING", "LEMMING"
        };
    }
}
