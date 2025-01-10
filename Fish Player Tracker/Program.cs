using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Random = System.Random;
using Timer = System.Timers.Timer;

namespace Fish_Player_Tracker
{
    internal class Program
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly Random random = new Random();
        public static string playFabTitleID = "63FDD";
        public static string steamTicket = string.Empty, playFabId = string.Empty, sessionTicket = string.Empty;
        public static string playFabApiHost = $"{playFabTitleID}.playfabapi.com";
        public static string Path = @"C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\Tracker";
        public static string steamAuthTicketPath = $@"{Path}\SteamAuthTicket.txt";
        public static string sessionTicketPath = $@"{Path}\sessionTicket.txt";
        public static string playFabIdPath = $@"{Path}\playFabId.txt";
        public static string RoomCodesPrvPath = $@"{Path}\RoomCodesPrv.txt";
        public static string RoomCodesPubPath = $@"{Path}\RoomCodesPub.txt";
        public static int PlayerList;
        public static int PlayerList2;
        private static Timer cooldownTimer, roomCodesTimer;
        private static DateTime nextRunTime = DateTime.MinValue;
        private static DateTime nextRunTime2 = DateTime.MinValue;
        private static string lastTitle = "", lastRoom = "";
        private static Dictionary<string, DateTime> removedRooms = new Dictionary<string, DateTime>();

        static async Task Main(string[] args)
        {

            try
            {
                Console.Title = "Fish Player Tracker Prv ~ by notfishvr";

                if (File.Exists(steamAuthTicketPath)) { steamTicket = File.ReadAllText(steamAuthTicketPath); }
                if (File.Exists(sessionTicketPath)) { sessionTicket = File.ReadAllText(sessionTicketPath); }
                if (File.Exists(playFabIdPath)) { playFabId = File.ReadAllText(playFabIdPath); }
                if (File.Exists(RoomCodesPrvPath)) { Settings.roomsPrv = File.ReadAllText(RoomCodesPrvPath).Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
                if (File.Exists(RoomCodesPubPath)) { Settings.roomsPub = File.ReadAllText(RoomCodesPubPath).Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList(); }


                var loginRequest = new { TitleId = playFabTitleID, SteamTicket = steamTicket, InfoRequestParameters = new { GetUserAccountInfo = true } };

                string loginRequestJson = JsonSerializer.Serialize(loginRequest);
                var content = new StringContent(loginRequestJson, Encoding.UTF8, "application/json");

                await LoginWithSteam(loginRequestJson);

                cooldownTimer = new Timer(100);
                cooldownTimer.Elapsed += async (sender, e) => await OnCooldownElapsed();
                cooldownTimer.Start();

                //roomCodesTimer = new Timer(65000); 
                //roomCodesTimer.Elapsed += (sender, e) => LoadRoomCodes();
                //roomCodesTimer.Start();

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                File.WriteAllText(@"C:\Users\notfishvr\source\repos\Fish Player Tracker\error.txt", ex.ToString());
            }
        }
        private static void LoadRoomCodes()
        {
            if (File.Exists(RoomCodesPrvPath))
            {
                Settings.roomsPrv = File.ReadAllText(RoomCodesPrvPath)
                    .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(room => !removedRooms.ContainsKey(room))  // Skip removed rooms
                    .ToList();

                Settings.roomsPub = File.ReadAllText(RoomCodesPubPath)
                    .Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(room => !removedRooms.ContainsKey(room))  // Skip removed rooms
                    .ToList();
            }
        }
        private static async Task OnCooldownElapsed()
        {
            if (DateTime.UtcNow >= nextRunTime)
            {
                //GetSharedGroupDataPub();
                nextRunTime = DateTime.UtcNow.AddMilliseconds(310);
            }
            if (DateTime.UtcNow >= nextRunTime2)
            {
                GetSharedGroupDataPrv();
                nextRunTime2 = DateTime.UtcNow.AddMilliseconds(450);
            }

            List<string> roomsToReadd = removedRooms.Where(r => DateTime.UtcNow >= r.Value).Select(r => r.Key).ToList();

            foreach (string room in roomsToReadd)
            {
                Settings.roomsPub.Add(room);
                Settings.roomsPrv.Add(room);
                removedRooms.Remove(room);
                //Console.WriteLine($"Room {room} has been re-added after 10 minutes.");
            }
        }
        private static async Task GetSharedGroupDataPrv()
        {
            await GetSharedGroupData("prv");
        }
        private static async Task GetSharedGroupDataPub()
        {
            await GetSharedGroupData("pub");
        }
        private static async Task GetSharedGroupData(string groupType)
        {
            string getAccountInfoEndpoint = $"https://{playFabApiHost}/Client/GetSharedGroupData";
            string room = (groupType == "prv") ? Settings.roomsPrv[Settings.index] : Settings.roomsPub[Settings.index2];
            string region = Settings.regions[random.Next(0, Settings.regions.Length)];
            string combinedCode = room + region;

            var requestPayload = new
            {
                SharedGroupId = combinedCode
            };

            string requestJson = JsonSerializer.Serialize(requestPayload);
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, getAccountInfoEndpoint)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.Add("X-Authorization", sessionTicket);

            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
            string responseContent = await response.Content.ReadAsStringAsync();

            string filePath = (groupType == "prv") ? "Data/GetSharedGroupData.txt" : "Data/GetSharedGroupData2.txt";
            using (StreamWriter outputFile = new StreamWriter(filePath))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    outputFile.WriteLine($"Failed {response.StatusCode}");
                    outputFile.WriteLine(responseContent);
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    outputFile.WriteLine("Unauthorized");
                }

                var responseJson = JsonDocument.Parse(responseContent);
                outputFile.WriteLine(JsonSerializer.Serialize(responseJson, new JsonSerializerOptions { WriteIndented = true }));

                if (responseJson.RootElement.TryGetProperty("data", out JsonElement dataElement))
                {
                    if (dataElement.TryGetProperty("Data", out JsonElement dataItems))
                    {
                        if (dataItems.GetRawText() == "{}")
                        {
                            RemoveRoomFor10Minutes(room);
                        }
                        else
                        {
                            foreach (JsonProperty item in dataItems.EnumerateObject())
                            {
                                JsonElement properties = item.Value;
                                string value = properties.GetProperty("Value").GetString();
                                int playerList = 0;
                                if (dataElement.TryGetProperty("Data", out JsonElement actorsElement))
                                {
                                    foreach (JsonProperty property in actorsElement.EnumerateObject())
                                    {
                                        playerList++;
                                    }
                                }

                                await CheckAndSendWebhook(value, room, playerList, region);

                                if (value.Contains("TooManyRequests"))
                                {
                                    if (responseJson.RootElement.TryGetProperty("retryAfterSeconds", out JsonElement retryAfterElement))
                                    {
                                        nextRunTime = DateTime.UtcNow.AddSeconds(retryAfterElement.GetInt32());
                                        nextRunTime2 = DateTime.UtcNow.AddSeconds(retryAfterElement.GetInt32());
                                    }
                                    else
                                    {
                                        nextRunTime = DateTime.UtcNow.AddSeconds(25);
                                        nextRunTime2 = DateTime.UtcNow.AddSeconds(25);
                                    }
                                }
                            }
                        }
                    }
                }

                if (responseJson.RootElement.TryGetProperty("errorCode", out JsonElement errorCodeElement))
                {
                    if (errorCodeElement.GetInt32() == 1199)
                    {
                        nextRunTime = DateTime.UtcNow.AddSeconds(25);
                    }
                }
            }
            if (groupType == "prv")
            {
                Console.WriteLine(room);
                CycleRoomCode(ref Settings.index, Settings.roomsPrv.Count);
            }
            else
            {
                Console.WriteLine(room);
                CycleRoomCode(ref Settings.index2, Settings.roomsPub.Count);
            }

            nextRunTime = DateTime.UtcNow.AddMilliseconds(450);
            nextRunTime2 = DateTime.UtcNow.AddMilliseconds(450);
        }
        private static void CycleRoomCode(ref int index, int roomListCount)
        {
            if (index + 1 == roomListCount)
            {
                index = 0;
            }
            else
            {
                index++;
            }
        }
        private static async Task CheckAndSendWebhook(string value, string room, int playerList, string region)
        {
            if (value.Contains(Settings.cosmetics[0]))
            {
                await SendDiscordWebhook("Player Found - Finger Painter Badge", room, playerList, region, "https://static.wikia.nocookie.net/gorillatag/images/b/b7/Fingerpaint.png/revision/latest/thumbnail/width/360/height/360?cb=20231114024321", "pro");
            }
            if (value.Contains(Settings.cosmetics[1]))
            {
                await SendDiscordWebhook("Player Found - Illustrator Badge", room, playerList, region, "https://static.wikia.nocookie.net/gorillatag/images/2/22/IllustratorbadgeTransparent.png/revision/latest/thumbnail/width/360/height/360?cb=20240612230801", "pro");
            }
            if (value.Contains(Settings.cosmetics[2]))
            {
                await SendDiscordWebhook("Player Found - Administrator Badge", room, playerList, region, "https://static.wikia.nocookie.net/gorillatag/images/4/40/Adminbadge.png/revision/latest/thumbnail/width/360/height/360?cb=20220223233745", "pro");
            }
            if (value.Contains(Settings.cosmetics[3]))
            {
                await SendDiscordWebhook("Player Found - Moderator Stick", room, playerList, region, "https://static.wikia.nocookie.net/gorillatag/images/a/aa/Stick.png/revision/latest?cb=20231102195128", "pro");
            }
            if (value.Contains(Settings.cosmetics[4]))
            {
                await SendDiscordWebhook("Player Found - Cold Monke Sweater", room, playerList, region, "https://static.wikia.nocookie.net/gorillatag/images/9/9d/SweaterWinter23GTSprite.png/revision/latest/thumbnail/width/360/height/360?cb=20230127222427", "pro");
            }
            if (value.Contains(Settings.cosmetics[5]))
            {
                await SendDiscordWebhook("Player Found - 2022 Glasses", room, playerList, region, "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTysnxY8_8v5HAgUhZOMAz6iz9liIXcFxZR-kpO0VXERLP2I9wcMdomy6SHZ1Ir_b-CMIA&usqp=CAU", "free");
            }
            if (value.Contains(Settings.cosmetics[6]))
            {
                await SendDiscordWebhook("Player Found - GT1 Badge", room, playerList, region, "https://static.wikia.nocookie.net/gorillatag/images/8/88/Gt1.png/revision/latest/thumbnail/width/360/height/360?cb=20220223233019", "free");
            }
        }
        private static void RemoveRoomFor10Minutes(string room)
        {
            if (Settings.roomsPrv.Contains(room))
            {
                Settings.roomsPrv.Remove(room);
                removedRooms[room] = DateTime.UtcNow.AddMinutes(10);
                //Console.WriteLine($"Room {room} removed and will be re-added in 10 minutes.");
            }
            if (Settings.roomsPub.Contains(room))
            {
                Settings.roomsPub.Remove(room);
                removedRooms[room] = DateTime.UtcNow.AddMinutes(10);
                //Console.WriteLine($"Room {room} removed and will be re-added in 10 minutes.");
            }
        }
        private static async Task SendDiscordWebhook(string title, string room, int PlayerList, string region, string thumbnail, string thing)
        {
            if (title == lastTitle && room == lastRoom) { return; }

            string webhookUrl = "";
            string content = "";
            if (thing == "free") { webhookUrl = ""; }
            if (thing == "pro") { webhookUrl = ""; }
            if (title == "Player Found - Finger Painter Badge") { content = "||<@&1265507746535575623>||"; }
            if (title == "Player Found - Illustrator Badge") { content = "||<@&1266147098513113168>||"; }
            if (title == "Player Found - Administrator Badge") { content = "||<@&1265511219872137321>||"; }
            if (title == "Player Found - Moderator Stick") { content = "||<@&1265511376605020160>||"; }
            string time = DateTime.Now.ToString("h:mm tt") + " Central Time";
            var payload = new
            {
                content = content,
                embeds = new[]
                {
                    new
                    {
                        title = title,
                        description = $"Room: **{room}**\nPlayers: **{PlayerList}/10**\nTime: **{time}**\nRegion: **{region}**",
                        color = 0xFF0000,
                        thumbnail = new { url = thumbnail }
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

            lastTitle = title;
            lastRoom = room;
        }
        private static async Task LoginWithSteam(string loginRequestJson)
        {
            var response = await httpClient.PostAsync($"https://{playFabApiHost}/Client/LoginWithSteam", new StringContent(loginRequestJson, Encoding.UTF8, "application/json"));
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Your login");
            }
            else
            {
                Console.WriteLine("Something went wrong with your first API call.  :(");
                string errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Here's some debug information:\n{errorContent}");
            }
        }
        #region Code
        private static async Task GetAccountInfo()
        {
            string getAccountInfoEndpoint = $"https://{playFabApiHost}/Client/GetAccountInfo";
            var requestContent = new
            {
                PlayFabId = playFabId
            };
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, getAccountInfoEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.Add("X-PlayFabSDK", "PlayFabSDK/2.94.210118");
            requestMessage.Headers.Add("X-Authorization", sessionTicket);

            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
            string responseContent = await response.Content.ReadAsStringAsync();

            using (StreamWriter outputFile = new StreamWriter("Data/AccountInfo.txt"))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    outputFile.WriteLine($"Failed {response.StatusCode}");
                    outputFile.WriteLine(responseContent);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    outputFile.WriteLine("Unauthorized");
                }
                else
                {
                    var responseJson = JsonDocument.Parse(responseContent);
                    var data = responseJson.RootElement.GetProperty("data");

                    var accountInfo = data.GetProperty("AccountInfo");

                    var creationDate = accountInfo.TryGetProperty("Created", out var createdProp) ? createdProp.GetString() : "N/A";
                    var lastLoginDate = accountInfo.TryGetProperty("LastLogin", out var lastLoginProp) ? lastLoginProp.GetString() : "N/A";
                    var playerId = accountInfo.TryGetProperty("PlayFabId", out var playerIdProp) ? playerIdProp.GetString() : "N/A";
                    var isBanned = accountInfo.TryGetProperty("BannedUntil", out var bannedUntilProp) && bannedUntilProp.ValueKind != JsonValueKind.Null;

                    outputFile.WriteLine($"CreationDate: {creationDate}");
                    outputFile.WriteLine($"LastLoginDate: {lastLoginDate}");
                    outputFile.WriteLine($"PlayerId: {playerId}");
                    outputFile.WriteLine($"IsBanned: {isBanned}");
                }
            }
        }
        private static async Task GetInventory()
        {
            string getAccountInfoEndpoint = $"https://{playFabApiHost}/Client/GetUserInventory";
            var requestContent = new
            {
                PlayFabId = "A6FFC7318E1301AF"
            };
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, getAccountInfoEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json")
            };
            //requestMessage.Headers.Add("X-PlayFabSDK", "PlayFabSDK/2.94.210118");
            requestMessage.Headers.Add("X-Authorization", sessionTicket);

            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
            string responseContent = await response.Content.ReadAsStringAsync();

            using (StreamWriter outputFile = new StreamWriter("Data/Inventory-A6FFC7318E1301AF.txt"))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    outputFile.WriteLine($"Failed {response.StatusCode}");
                    outputFile.WriteLine(responseContent);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    outputFile.WriteLine("Unauthorized");
                }
                else
                {
                    var responseJson = JsonDocument.Parse(responseContent);
                    var data = responseJson.RootElement.GetProperty("data");
                    outputFile.Write(data);

                }
            }
        }
        private static async Task GetCatalogItems()
        {
            string getAccountInfoEndpoint = $"https://{playFabApiHost}/Client/GetCatalogItems";
            var requestContent = new
            {
                PlayFabId = playFabId
            };
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, getAccountInfoEndpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(requestContent), Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.Add("X-PlayFabSDK", "PlayFabSDK/2.94.210118");
            requestMessage.Headers.Add("X-Authorization", sessionTicket);

            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
            string responseContent = await response.Content.ReadAsStringAsync();

            using (StreamWriter outputFile = new StreamWriter("Data/CatalogItems.txt"))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    outputFile.WriteLine($"Failed {response.StatusCode}");
                    outputFile.WriteLine(responseContent);
                }
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    outputFile.WriteLine("Unauthorized");
                }
                var responseJson = JsonDocument.Parse(responseContent);
                outputFile.WriteLine(JsonSerializer.Serialize(responseJson, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        #endregion
    }
}