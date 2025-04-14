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
using System.Threading.Tasks;
using UnityEngine;
using Valve.Newtonsoft.Json;

namespace Fish_Player_Tracker_Lib
{
    [BepInPlugin("Fish.Tracker.lol", "Tracker", "2.0.0")]
    public class Class1 : BaseUnityPlugin
    {
        #region Constants and Static Fields
        private static readonly string BasePath = @"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\Tracker";
        private static readonly string ConfigPath = Path.Combine(BasePath, "config.json");
        private static readonly string SessionTicketPath = Path.Combine(BasePath, "sessionTicket.txt");
        private static readonly string PlayFabIdPath = Path.Combine(BasePath, "playFabId.txt");
        private static readonly string RoomCodesPrvPath = Path.Combine(BasePath, "RoomCodesPrv.txt");
        private static readonly string RoomCodesPubPath = Path.Combine(BasePath, "RoomCodesPub.txt");
        private static readonly string UserIdsPath = Path.Combine(BasePath, "UserIds.json");
        private static readonly string LogPath = Path.Combine(BasePath, "log.txt");

        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly HashSet<string> ProcessedPlayers = new HashSet<string>();
        private static float _nextHopTime = 0f;
        private static bool _isHopping = false;
        private static float _lastTrackerUpdate = 0f;
        private static readonly float TrackerUpdateInterval = 10f;
        private static readonly float RoomHopCooldown = 20f;
        #endregion

        #region Configuration Classes
        [Serializable]
        private class TrackerConfig
        {
            public string WebhookUrl { get; set; } = Settings.WebHook;
            public bool EnableRoomHopping { get; set; } = false;
            public bool LogFoundPlayers { get; set; } = true;
            public int HopCooldownSeconds { get; set; } = 20;
            public List<string> TargetCosmeticIds { get; set; } = new List<string>
            {
                "LBADE", "LBAGS", "LBAAD", "LBAAK", "LBACP", "LFAAZ", "LBAAZ"
            };
            public List<string> PriorityRoomCodes { get; set; } = new List<string>();
        }

        [Serializable]
        private class RoomData
        {
            public List<string> PrivateRooms { get; set; } = new List<string>();
            public List<string> PublicRooms { get; set; } = new List<string>();
            public Dictionary<string, DateTime> LastSeen { get; set; } = new Dictionary<string, DateTime>();
        }

        [Serializable]
        private class PlayerData
        {
            public string PlayerId { get; set; }
            public string PlayerName { get; set; }
            public List<string> KnownCosmeticIds { get; set; } = new List<string>();
            public DateTime LastSeen { get; set; }
            public string LastRoom { get; set; }
        }
        #endregion

        #region Private Fields
        private TrackerConfig _config = new TrackerConfig();
        private RoomData _roomData = new RoomData();
        private Dictionary<string, PlayerData> _playerDataDict = new Dictionary<string, PlayerData>();
        private DateTime _lastWebhookSent = DateTime.MinValue;
        #endregion

        private void Awake()
        {
            try
            {
                if (!Directory.Exists(BasePath))
                {
                    Directory.CreateDirectory(BasePath);
                }

                if (!File.Exists(SessionTicketPath)) { File.WriteAllText(SessionTicketPath, string.Empty); }
                if (!File.Exists(PlayFabIdPath))     { File.WriteAllText(PlayFabIdPath, string.Empty); }
                if (!File.Exists(LogPath))           { File.WriteAllText(LogPath, string.Empty); }

                if (!File.Exists(RoomCodesPrvPath))
                {
                    PopulateDefaultRoomCodes();
                    SaveRoomData();
                }

                HttpClient.Timeout = TimeSpan.FromSeconds(10);

                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        string json = File.ReadAllText(ConfigPath);
                        _config = JsonConvert.DeserializeObject<TrackerConfig>(json);
                    }
                    else
                    {
                        SaveConfiguration();
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Failed to load configuration: {ex.Message}");
                    _config = new TrackerConfig();
                    SaveConfiguration();
                }

                UpdateFileIfChanged(RoomCodesPrvPath, string.Join(", ", roomsPrv));

                _roomData.PrivateRooms = roomsPrv;
                // Load player data
                try
                {
                    if (File.Exists(UserIdsPath))
                    {
                        string json = File.ReadAllText(UserIdsPath);
                        _playerDataDict = JsonConvert.DeserializeObject<Dictionary<string, PlayerData>>(json);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Failed to load player data: {ex.Message}");
                    _playerDataDict = new Dictionary<string, PlayerData>();
                }

                SaveData();
                InvokeRepeating("SaveData", 60f, 60f);

                Log("Tracker initialized successfully");
            }
            catch (Exception ex)
            {
                LogError($"Failed to initialize tracker: {ex.Message}");
            }
        }
        private void Update()
        {
            try
            {
                if (Time.time > _lastTrackerUpdate + TrackerUpdateInterval)
                {
                    try
                    {
                        string sessionTicket = Traverse.Create(PlayFabAuthenticator.instance).Field("_sessionTicket").GetValue()?.ToString();
                        string playFabId = Traverse.Create(PlayFabAuthenticator.instance).Field("_playFabId").GetValue()?.ToString();

                        UpdateFileIfChanged(SessionTicketPath, sessionTicket);
                        UpdateFileIfChanged(PlayFabIdPath, playFabId);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Failed to update session data: {ex.Message}");
                    }
                    _lastTrackerUpdate = Time.time;
                }

                if (_config.EnableRoomHopping)
                {
                   // HandleRoomHopping();
                }

                if (PhotonNetwork.InRoom)
                {
                    ScanPlayersInRoom();
                }
            }
            catch (Exception ex)
            {
                LogError($"Error in Update: {ex.Message}");
            }
        }
        private void PopulateDefaultRoomCodes()
        {
            //_roomData.PrivateRooms = new List<string>
            //{
            //};
        }
        private void ScanPlayersInRoom()
        {
            if (!PhotonNetwork.InRoom) { return; }

            try
            {
                string currentRoom = PhotonNetwork.CurrentRoom.Name;

                if (!_roomData.PublicRooms.Contains(currentRoom))
                {
                    _roomData.PublicRooms.Add(currentRoom);
                }

                _roomData.LastSeen[currentRoom] = DateTime.Now;

                foreach (Photon.Realtime.Player player in PhotonNetwork.PlayerList)
                {
                    string userId = player.UserId;

                    if (ProcessedPlayers.Contains(userId)) { continue; }
                    if (_playerDataDict.TryGetValue(userId, out PlayerData playerData))
                    {
                        playerData.PlayerName = player.NickName;
                        playerData.LastSeen = DateTime.Now;
                        playerData.LastRoom = currentRoom;

                        if ((DateTime.Now - _lastWebhookSent).TotalSeconds > 5)
                        {
                            Task.Run(() => SendPlayerFoundWebhook(playerData, currentRoom));
                            _lastWebhookSent = DateTime.Now;
                        }

                        Log($"Found tracked player: {playerData.PlayerName} ({userId}) in room {currentRoom}");
                    }
                    ProcessedPlayers.Add(userId);
                }

                if (_config.EnableRoomHopping)
                {
                    PhotonNetwork.Disconnect();
                }
            }
            catch (Exception ex)
            {
                LogError($"Error scanning players: {ex.Message}");
            }
        }
        private async Task SendPlayerFoundWebhook(PlayerData playerData, string room)
        {
            if (string.IsNullOrEmpty(_config.WebhookUrl)) { return; }

            try
            {
                string time = DateTime.Now.ToString("h:mm tt") + " Local Time";

                var payload = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title = $"Player Found: {playerData.PlayerName}",
                            description = $"Player ID: **{playerData.PlayerId}**\nRoom: **{room}**\nTime: **{time}**",
                            color = 0xFF0000
                        }
                    }
                };

                string payloadJson = JsonConvert.SerializeObject(payload);
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, _config.WebhookUrl)
                {
                    Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                };

                HttpResponseMessage response = await HttpClient.SendAsync(requestMessage);
                if (response.StatusCode != System.Net.HttpStatusCode.NoContent)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    LogError($"Failed to send webhook: {response.StatusCode}\n{responseContent}");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error sending webhook: {ex.Message}");
            }
        }
        #region Data Management
        private void SaveConfiguration()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                LogError($"Failed to save configuration: {ex.Message}");
            }
        }
        private void SaveRoomData()
        {
            try
            {
               // string json = JsonConvert.SerializeObject(_roomData, Formatting.Indented);
                //File.WriteAllText(RoomCodesPubPath, json);
            }
            catch (Exception ex)
            {
                LogError($"Failed to save room data: {ex.Message}");
            }
        }
        private void SaveData()
        {
            SaveConfiguration();
            SaveRoomData();
            try
            {
                string json = JsonConvert.SerializeObject(_playerDataDict, Formatting.Indented);
                File.WriteAllText(UserIdsPath, json);
            }
            catch (Exception ex)
            {
                LogError($"Failed to save player data: {ex.Message}");
            }
        }
        #endregion
        #region Room Hopping
        private void HandleRoomHopping()
        {
            if (_isHopping) { return; }

            if (!PhotonNetwork.InRoom && Time.time >= _nextHopTime)
            {
                _isHopping = true;

                try
                {
                    string roomCode = GetNextRoomToCheck();
                    GameObject joinTrigger = FindJoinTrigger(_isHopping);

                    if (joinTrigger != null)
                    {
                        if (GTPlayer.Instance != null && joinTrigger != null)
                        {
                            GTPlayer.Instance.headCollider.gameObject.transform.position = joinTrigger.transform.position;
                            GTPlayer.Instance.bodyCollider.gameObject.transform.position = joinTrigger.transform.position;

                            if (GTPlayer.Instance.TryGetComponent<Rigidbody>(out var rb))
                            {
                                rb.velocity = Vector3.zero;
                            }
                        }
                        ToggleMeshColliders(false);

                        if (joinTrigger.TryGetComponent<GorillaNetworkJoinTrigger>(out var trigger))
                        {
                            trigger.OnBoxTriggered();
                            PhotonNetworkController.Instance.AttemptToJoinPublicRoom(trigger, JoinType.Solo);
                            Log($"Attempting to join room via trigger {joinTrigger.name}");
                        }

                        _nextHopTime = Time.time + RoomHopCooldown;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error during room hopping: {ex.Message}");
                }

                _isHopping = false;
            }
            else if (PhotonNetwork.InRoom)
            {
                ToggleMeshColliders(true);
            }
        }
        private string GetNextRoomToCheck()
        {
            if (_config.PriorityRoomCodes.Count > 0)
            {
                return _config.PriorityRoomCodes[UnityEngine.Random.Range(0, _config.PriorityRoomCodes.Count)];
            }

            if (_roomData.PrivateRooms.Count > 0)
            {
                return _roomData.PrivateRooms[UnityEngine.Random.Range(0, _roomData.PrivateRooms.Count)];
            }

            return "MODS";
        }
        private GameObject FindJoinTrigger(bool useCity)
        {
            GameObject trigger = null;

            if (useCity)
            {
                trigger = GameObject.Find("JoinPublicRoom - City Front");
            }

            if (trigger == null)
            {
                trigger = GameObject.Find("JoinPublicRoom - Forest, Tree Exit");
            }

            return trigger;
        }
        private void ToggleMeshColliders(bool enabled)
        {
            try
            {
                foreach (MeshCollider meshCollider in UnityEngine.Object.FindObjectsOfType<MeshCollider>())
                {
                    if (meshCollider != null && meshCollider.gameObject.activeSelf)
                    {
                        meshCollider.enabled = enabled;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error toggling mesh colliders: {ex.Message}");
            }
        }
        #endregion
        #region Logging
        private void Log(string message)
        {
            try
            {
                if (_config.LogFoundPlayers)
                {
                    Debug.Log($"[Tracker] {message}");
                    File.AppendAllText(LogPath, $"[{DateTime.Now}] {message}\n");
                }
            }
            catch
            {
            }
        }
        private void LogError(string message)
        {
            try
            {
                Debug.LogError($"[Tracker] {message}");
                File.AppendAllText(LogPath, $"[{DateTime.Now}] ERROR: {message}\n");
            }
            catch
            {
            }
        }
        private void UpdateFileIfChanged(string filePath, string newContent)
        {
            if (string.IsNullOrEmpty(newContent)) { return; }

            try
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
            catch (Exception ex)
            {
                LogError($"Failed to update file {filePath}: {ex.Message}");
            }
        }
        #endregion

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
    }
}