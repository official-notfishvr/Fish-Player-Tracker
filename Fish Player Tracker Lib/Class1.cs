using BepInEx;
using GorillaLocomotion;
using GorillaNetworking;
using GorillaTag;
using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;
using Valve.Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Fish_Player_Tracker_Lib
{
    [BepInPlugin("Fish.Tracker.lol", "Tracker", "1.0.0")]
    public class Class1 : BaseUnityPlugin
    {
        public static string Path = @"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\Tracker";
        public static string steamAuthTicketPath = $@"{Path}\SteamAuthTicket.txt";
        public static string sessionTicketPath = $@"{Path}\sessionTicket.txt";
        public static string playFabIdPath = $@"{Path}\playFabId.txt";
        public static string playerEntityPath = $@"{Path}\playerEntity.txt";
        public static string RoomCodesPrvPath = $@"{Path}\RoomCodesPrv.txt";
        public static string RoomCodesPubPath = $@"{Path}\RoomCodesPub.txt";
        public static string UserIdsPath = $@"{Path}\UserIds.txt";
        public static string IndexPath = $@"{Path}\Index.txt";
        public static int index = 0;
        private Dictionary<string, string> userIdDict = new Dictionary<string, string>();
        private static readonly HttpClient httpClient = new HttpClient();

        public Class1()
        {
        }

        public void Update()
        {
            //string steamAuthTicket = PlayFabAuthenticator.instance.GetSteamAuthTicket().ToString();
            //SteamAuthTicket steamAuthTicket = Traverse.Create(PlayFabAuthenticator.instance).Field("steamAuthTicketForPlayFab").GetValue();
            string sessionTicket = Traverse.Create(PlayFabAuthenticator.instance).Field("_sessionTicket").GetValue()?.ToString();
            string playFabId = Traverse.Create(PlayFabAuthenticator.instance).Field("_playFabId").GetValue()?.ToString();
            string playerEntity = Traverse.Create(GorillaNetworking.GorillaServer.Instance).Field("get_playerEntity").GetValue()?.ToString();

            try
            {
                if (!Directory.Exists(Path)) { Directory.CreateDirectory(Path); }
                //UpdateFileIfChanged(steamAuthTicketPath, steamAuthTicket);
                UpdateFileIfChanged(sessionTicketPath, sessionTicket);
                UpdateFileIfChanged(playFabIdPath, playFabId);
                UpdateFileIfChanged(playerEntityPath, playerEntity);
                UpdateFileIfChanged(RoomCodesPrvPath, string.Join(", ", roomsPrv));
                //UpdateFileIfChanged(UserIdsPath, string.Empty);

                LoadIndex();
                LoadUserIds();

                if (!PhotonNetwork.InRoom)
                {
                    if (Time.time >= Settings.hopCooldown)
                    {
                        GameObject gameObject;
                        if (!Settings.hasAddedCity)
                        {
                            gameObject = GameObject.Find("JoinPublicRoom - Forest, Tree Exit");
                            gameObject.GetComponent<GorillaNetworkJoinTrigger>().OnBoxTriggered();
                            Settings.hasAddedCity = true;
                        }
                        else
                        {
                            gameObject = GameObject.Find("JoinPublicRoom - City Front");
                            gameObject.GetComponent<GorillaNetworkJoinTrigger>().OnBoxTriggered();
                            Settings.hasAddedCity = false;
                        }
                        Player.Instance.headCollider.gameObject.transform.position = gameObject.gameObject.transform.position;
                        Player.Instance.bodyCollider.gameObject.transform.position = gameObject.gameObject.transform.position;
                        foreach (MeshCollider meshCollider in UnityEngine.Object.FindObjectsOfType<MeshCollider>())
                        {
                            if (meshCollider.gameObject.activeSelf) { meshCollider.enabled = false; }
                        }
                        PhotonNetworkController.Instance.AttemptToJoinPublicRoom(gameObject.GetComponent<GorillaNetworkJoinTrigger>(), JoinType.Solo);
                        Settings.hopCooldown = Time.time + 20f;
                    }
                    else
                    {
                        foreach (MeshCollider meshCollider2 in UnityEngine.Object.FindObjectsOfType<MeshCollider>())
                        {
                            if (meshCollider2.gameObject.activeSelf) { meshCollider2.enabled = true; }
                        }
                        Player.Instance.GetComponent<Rigidbody>().velocity = Vector3.zero;
                    }
                }
                else
                {
                    foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
                    {
                        string userId = player.UserId;
                        if (userIdDict.ContainsKey(userId))
                        {
                            Debug.Log($"User ID {userId} found with name {userIdDict[userId]}.");
                            Task.Run(() => SendDiscordWebhook(userIdDict[userId], userId, player));
                        }
                        else { }
                    }
                    if (!roomsPub.Contains(PhotonNetwork.CurrentRoom.Name)) { roomsPub.Add(PhotonNetwork.CurrentRoom.Name); }
                    PhotonNetwork.Disconnect();
                    string formattedRoomsPub = string.Join(", ", roomsPub.Select(code => $"\"{code}\""));
                    File.WriteAllText(RoomCodesPubPath, formattedRoomsPub);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to write to files: {ex.Message}");
            }
        }
        private void LoadIndex()
        {
            if (File.Exists(IndexPath))
            {
                string indexContent = File.ReadAllText(IndexPath);
                if (int.TryParse(indexContent, out int loadedIndex))
                {
                    index = loadedIndex;
                }
                else
                {
                    Debug.LogError($"Invalid index value in {IndexPath}");
                }
            }
        }
        private void LoadUserIds()
        {
            if (File.Exists(UserIdsPath))
            {
                string[] lines = File.ReadAllLines(UserIdsPath);
                foreach (var line in lines)
                {
                    var parts = line.Split(';');
                    if (parts.Length == 2)
                    {
                        userIdDict[parts[0]] = parts[1];
                        Debug.Log("userIdDict" + userIdDict);
                        Debug.Log("parts 0:" + parts[0]);
                        Debug.Log("parts 1:" + parts[1]);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"User IDs file not found at {UserIdsPath}");
            }
        }
        private void UpdateFileIfChanged(string filePath, string newContent)
        {
            if (File.Exists(filePath))
            {
                string existingContent = File.ReadAllText(filePath);
                if (existingContent != newContent)
                {
                    File.WriteAllText(filePath, newContent);
                }
            }
            else
            {
                File.WriteAllText(filePath, newContent);
            }
        }
        private static async Task SendDiscordWebhook(string UserName, string UserID, Photon.Realtime.Player player)
        {
            string webhookUrl = "";
            webhookUrl = Settings.WebHook;
            string time = DateTime.Now.ToString("h:mm tt") + " Central Time";
            var payload = new
            {
                // = content,
                embeds = new[]
                {
                    new
                    {
                        title = "Player Found " + UserName,
                        description = $"Player Name: **{player.NickName}**\nPlayer ID: **{player.UserId}**\nTime: **{time}**",
                        color = 0xFF0000,
                    }
                }
            };
            string payloadJson = JsonSerializer.Serialize(payload);
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
            if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to send webhook: {response.StatusCode}");
                Console.WriteLine(responseContent);
            }
        }
        public static List<string> roomsPrv = new List<string>
        {
            // prv codes
            "TYLERVR", "ALECVR", "LUCIO", "DEEP", "JUAN", "JUANGTAG", "MELT", "JMAN", "JMANCURLY", "ELLIOT",
            "ELLIOT1", "ELLIOT2", "VMT", "K9", "HUNT", "MODS", "MOD", "MEET2", "MEET3", "GTAG",
            "MEET4", "MEET5", "MEET6", "MEET7", "MEET8", "QWERTY", "QWERTYUIOP", "SILLY", "TILLY", "VEN1",
            "VEN2", "RANG", "GTC", "DYL", "TAG", "TTT", "TTTPIG", "PIG", "MINIGAM", "MINIGAME",
            "MINIGAMES", "MALLRUSH", "COLORRUSH", "SHELF", "ROLEPLAY", "GORILLA", "MONKE", "MONKEY", "BOT", "GHOST",
            "MIRRORMAN", "ERROR", "RUN", "RUN555999", "SREN17", "SREN18", "SREN16", "COMP", "J3VU",
            "PBBV", "ECHO", "555999", "STATUE", "DAISYDAISY", "DAISY", "DAISY09", "DAISY08", "CHIPPD",
            "BANSHEE", "123", "1234", "12345", "123456", "1234567", "12345678", "123456789", "1234567890",
            "ALEC", "MAXO", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "ECT", "MINI", "ECHO",
            "VEN", "FOOT", "MONKER", "FAADDUU", "FAADDUUVR", "CUBCUB", "CUBCUB11", "BUBBLESVR", "ELLIOTVR", "ELLIOT3",
            "STYLED", "SNAIL", "STYLEDSNAIL", "JUITAR", "FIIZY", "ITSFIIZY", "CHRISNADO",
            "MAJORA", "ANTOCA", "STICK", "STICKS", "GTAG", "SKIBIDI",  "IDEN", "IDENVR", "GAY",
            "ABC", "ABCD", "A", "B", "C", "D", "E", "F", "G",
            "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q",
            "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "AMXR",
            "ZERDY", "DAPPER", "TURBO", "MOSA", "RAKZZ", "AUSSIE", "H4KPY",
            "DAPPERSLUG", "CODY", "QUINN", "LEOVR", "LEO", "PARTYMONKEY", "BOETHIA",
            "CHIVI", "HEADCHEF", "HEADCHEFVR", "KNINLY", "JAWCLAMPS", "KISHARK", "WIDDOM", "TIKTOK", "YOUTUBER", "GTC1", "GTC2", "GTC3",
            "GTC4", "GTC5", "GTC6", "GTC7", "GTC8", "GTC9", "GTC10", "AA", "THUMBZ",
            "JMAN1", "K8", "TIMMY", "JMAN2", "JMAN3", "GT", "CGT", "RUN1", "666", "DAISY099",
            "ENDISHERE", "BANJO", "CHIPPDBANJO", "GH0ST", "END", "DEATH", "FNAF", "ECH0", "BANANA",
            "SMILER", "UNKNOWN", "BOTS", "DEAD", "MORSE", "SPIDER", "MONK", "MODDER", "MODDERS", "MODERATOR",
            "BODA", "JOLYENE", "ELECTRONIC", "OWNER", "DEV", "CREATOR", "11", "12", "13", "14", "15", "16",
            "17", "18", "19", "20", "CREEP", "CREEPY", "SCARY", "SPOOKY", "SPOOK", "GAMES", "PLAY", "FINGERPAINTER",
            "CONTENTCREATOR", "CONTENT", "HELPME", "BEES", "NAMO", "WARNING", "HIDE", "WOW", "MITTENS", "RAY2", "RAY1",
            "GRAPES", "MICROPHONE", "BARK", "DURF", "JULIAN", "HAVEN", "VR", "WEAREVR", "FINGER",
            "PAINTER", "ADMIN", "STAFF", "CRASH", "YOUTUBE", "MODDING", "LEMMING"
        };
        public static List<string> roomsPub = new List<string>
        {
            // Pub codes
        };
    }
}
