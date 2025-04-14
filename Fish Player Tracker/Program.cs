
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using System.Collections;
using System.Text.RegularExpressions;

namespace Fish_Player_Tracker
{
    internal class Program
    {
        public static readonly HttpClient httpClient = new HttpClient();
        private static readonly Random random = new Random();

        // Constants
        private const string PLAYFAB_TITLE_ID = "63FDD";
        public static readonly string PLAYFAB_API_HOST = $"{PLAYFAB_TITLE_ID}.playfabapi.com";

        // Paths
        private static readonly string BASE_PATH = @"\\?\C:\Program Files (x86)\Steam\steamapps\common\Gorilla Tag\Tracker";
        private static readonly string SESSION_TICKET_PATH = Path.Combine(BASE_PATH, "sessionTicket.txt");
        private static readonly string PLAYFAB_ID_PATH = Path.Combine(BASE_PATH, "playFabId.txt");
        private static readonly string ROOM_CODES_PRV_PATH = Path.Combine(BASE_PATH, "RoomCodesPrv.txt");
        private static readonly string ROOM_CODES_PUB_PATH = Path.Combine(BASE_PATH, "RoomCodesPub.txt");
        private static readonly string LOG_PATH = Path.Combine(BASE_PATH, "Logs");

        // State variables
        public static string playFabId = string.Empty;
        public static string sessionTicket = string.Empty;
        private static Timer scanPrvRoomsTimer;
        private static Timer scanPubRoomsTimer;
        private static Timer roomCodesRefreshTimer;
        private static DateTime nextPrvRunTime = DateTime.MinValue;
        private static DateTime nextPubRunTime = DateTime.MinValue;
        private static string lastTitle = string.Empty;
        private static string lastRoom = string.Empty;
        private static Dictionary<string, DateTime> roomCooldowns = new Dictionary<string, DateTime>();

        // Tracking statistics
        private static int totalRoomsScanned = 0;
        private static int totalPlayersFound = 0;
        private static int totalWebhooksSent = 0;
        public static int totalCosmeticRequestsSent = 0;
        private static DateTime startTime;

        public static int totalRequestsSent = 0;

        static async Task Main(string[] args)
        {
            try
            {
                Console.Title = "Fish Player Tracker v2.0 ~ by notfishvr";
                startTime = DateTime.Now;

                Directory.CreateDirectory(BASE_PATH);
                Directory.CreateDirectory(LOG_PATH);

                if (File.Exists(SESSION_TICKET_PATH))
                {
                    sessionTicket = File.ReadAllText(SESSION_TICKET_PATH);
                    Console.WriteLine("Session ticket loaded");
                }
                else
                {
                    Console.WriteLine("Warning: Session ticket file not found");
                }

                if (File.Exists(PLAYFAB_ID_PATH))
                {
                    playFabId = File.ReadAllText(PLAYFAB_ID_PATH);
                    Console.WriteLine("PlayFab ID loaded");
                }
                else
                {
                    Console.WriteLine("Warning: PlayFab ID file not found");
                }

                if (string.IsNullOrWhiteSpace(sessionTicket))
                {
                    Console.WriteLine("Error: sessionTicket is empty. Please run Gorilla Tag first to generate a valid session ticket.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    Environment.Exit(0);
                }

                LoadRoomCodes();

                scanPrvRoomsTimer = new Timer(300);
                scanPrvRoomsTimer.Elapsed += async (s, e) => await ScanPrivateRooms();
                scanPrvRoomsTimer.Start();

                scanPubRoomsTimer = new Timer(400);
                scanPubRoomsTimer.Elapsed += async (s, e) => await ScanPublicRooms();
                scanPubRoomsTimer.Start();

                roomCodesRefreshTimer = new Timer(300000);
                roomCodesRefreshTimer.Elapsed += (s, e) => LoadRoomCodes();
                roomCodesRefreshTimer.Start();
                //await Program2.CheckCosmeticsForItem("LHAAC.");

                /*
                string targetPlayerId = "705F8FE3C09BDC77"; // A39B4F11EE490C34 // 35F9FF99509565B // 

                string itemId = "LHAAC.";  // The PlayFab item ID
                int cost = 1;                   // The price in virtual currency
                string currencyName = "SR";       // The virtual currency code

                await Program2.TryPurchaseItem(itemId, success => {
                    if (success)
                    {
                        Log("Purchase successful! Item added to inventory.");
                        // Update UI or notify the user
                    }
                    else
                    {
                        Log("Purchase failed. Please try again or check your balance.");
                        // Show error message to user
                    }
                });

                await Program2.GetPlayerRoomInfo(targetPlayerId);
                await Program2.QueryPlayerSharedData(targetPlayerId);
                */
                Console.WriteLine("Timers initialized");

                //DisplayMenu();
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                LogError("Main", ex);
                Console.WriteLine($"Critical error: {ex.Message}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
        private static void LoadRoomCodes()
        {
            Settings.roomsPrv.Clear();
            Settings.roomsPub.Clear();

            if (File.Exists(ROOM_CODES_PRV_PATH))
            {
                string content = File.ReadAllText(ROOM_CODES_PRV_PATH);
                Settings.roomsPrv = content.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                Console.WriteLine($"Loaded {Settings.roomsPrv.Count} private room codes");
            }
            else
            {
                Console.WriteLine("Warning: Private room codes file not found");
            }

            if (File.Exists(ROOM_CODES_PUB_PATH))
            {
                string content = File.ReadAllText(ROOM_CODES_PUB_PATH);
                Settings.roomsPub = content.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                Console.WriteLine($"Loaded {Settings.roomsPub.Count} public room codes");
            }
            else
            {
                Console.WriteLine("Warning: Public room codes file not found");
            }
        }
        private static void DisplayMenu()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    Console.Clear();
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("╔════════════════════════════════════════════════════╗");
                    Console.WriteLine("║          Fish Player Tracker v2.0                  ║");
                    Console.WriteLine("╚════════════════════════════════════════════════════╝");
                    Console.ResetColor();

                    Console.WriteLine($"Run time: {(DateTime.Now - startTime).ToString(@"hh\:mm\:ss")}");
                    Console.WriteLine($"Total rooms scanned: {totalRoomsScanned}");
                    Console.WriteLine($"Total players found: {totalPlayersFound}");
                    Console.WriteLine($"Total webhooks sent: {totalWebhooksSent}");
                    Console.WriteLine($"Total cosmetics requests: {totalCosmeticRequestsSent}");
                    Console.WriteLine($"Private rooms: {Settings.roomsPrv.Count} | Current index: {Settings.index}");
                    Console.WriteLine($"Public rooms: {Settings.roomsPub.Count} | Current index: {Settings.index2}");
                    Console.WriteLine();

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[Last 10 Activities]");
                    Console.ResetColor();

                    Console.WriteLine();
                    Console.WriteLine("Press 'R' to reload room codes, 'L' to view logs, 'C' to check cosmetics, 'Q' to quit");

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.R)
                        {
                            LoadRoomCodes();
                            Console.WriteLine("Room codes reloaded!");
                            await Task.Delay(1000);
                        }
                        else if (key == ConsoleKey.L)
                        {
                            ViewLogs();
                            await Task.Delay(1000);
                        }
                        else if (key == ConsoleKey.C)
                        {
                            await Task.Delay(1000);
                        }
                        else if (key == ConsoleKey.Q)
                        {
                            Environment.Exit(0);
                        }
                    }

                    await Task.Delay(1000);
                }
            });
        }
        private static void ViewLogs()
        {
            Console.Clear();
            Console.WriteLine("Recent logs:");

            try
            {
                var logFile = Path.Combine(LOG_PATH, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
                if (File.Exists(logFile))
                {
                    var lastLines = File.ReadLines(logFile).Take(20);
                    foreach (var line in lastLines)
                    {
                        Console.WriteLine(line);
                    }
                }
                else
                {
                    Console.WriteLine("No logs for today");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading logs: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to return to main menu...");
            Console.ReadKey();
        }
        private static async Task ScanPrivateRooms()
        {
            //Log($"[DEBUG] ScanPrivateRooms triggered at {DateTime.UtcNow}. Next run time: {nextPrvRunTime}");
            if (Settings.roomsPrv.Count == 0 || DateTime.UtcNow < nextPrvRunTime || string.IsNullOrEmpty(sessionTicket))
            {
                return;
            }

            try
            {
                await ScanRoom("prv");
            }
            catch (Exception ex)
            {
                LogError("ScanPrivateRooms", ex);
            }
        }
        private static async Task ScanPublicRooms()
        {
            if (Settings.roomsPub.Count == 0 || DateTime.UtcNow < nextPubRunTime || string.IsNullOrEmpty(sessionTicket))
            {
                return;
            }

            try
            {
                await ScanRoom("pub");
            }
            catch (Exception ex)
            {
                LogError("ScanPublicRooms", ex);
            }
        }
        private static async Task ScanRoom(string roomType)
        {
            string roomCode = GetNextRoomCode(roomType);
            string region = Settings.regions[random.Next(0, Settings.regions.Length)];
            string combinedCode = roomCode + region;

            if (roomCooldowns.TryGetValue(combinedCode, out DateTime cooldownTime) && DateTime.UtcNow < cooldownTime)
            {
                if (roomType == "prv")
                {
                    Settings.index = (Settings.index + 1) % Math.Max(1, Settings.roomsPrv.Count);
                }
                else
                {
                    Settings.index2 = (Settings.index2 + 1) % Math.Max(1, Settings.roomsPub.Count);
                }
                return;
            }

            string requestUrl = $"https://{PLAYFAB_API_HOST}/Client/GetSharedGroupData";
            var requestPayload = new
            {
                SharedGroupId = combinedCode
            };

            string requestJson = JsonSerializer.Serialize(requestPayload);
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.Add("X-Authorization", sessionTicket);

            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
            string responseContent = await response.Content.ReadAsStringAsync();
            totalRoomsScanned++;

            if (response.IsSuccessStatusCode)
            {
                await ProcessRoomResponse(responseContent, roomCode, region, roomType);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            {
                var responseJson = JsonDocument.Parse(responseContent);
                if (responseJson.RootElement.TryGetProperty("retryAfterSeconds", out JsonElement retryAfterElement))
                {
                    int retrySeconds = retryAfterElement.GetInt32();
                    nextPrvRunTime = DateTime.UtcNow.AddSeconds(retrySeconds);
                    nextPubRunTime = DateTime.UtcNow.AddSeconds(retrySeconds);
                    Log($"Rate limited. Retrying after {retrySeconds} seconds");
                }
                else
                {
                    nextPrvRunTime = DateTime.UtcNow.AddSeconds(5);
                    nextPubRunTime = DateTime.UtcNow.AddSeconds(5);
                    Log("Rate limited. Using default backoff of 5 seconds");
                }
            }
            else if (response.StatusCode == (System.Net.HttpStatusCode)429)
            {
                int waittime = 11; // 11 is the best tine for it to wait

                Log($"Error scanning room {roomCode}: {response.StatusCode} - waiting {waittime} seconds");

                nextPrvRunTime = DateTime.UtcNow.AddSeconds(waittime);
                nextPubRunTime = DateTime.UtcNow.AddSeconds(waittime);
                roomCooldowns[combinedCode] = DateTime.UtcNow.AddSeconds(waittime);

                await Task.Delay(3000);
            }
            else
            {
                Log($"Error scanning room {roomCode}: {response.StatusCode}");
            }

            if (roomType == "prv")
            {
                Settings.index = (Settings.index + 1) % Math.Max(1, Settings.roomsPrv.Count);
            }
            else
            {
                Settings.index2 = (Settings.index2 + 1) % Math.Max(1, Settings.roomsPub.Count);
            }
        }
        private static string GetNextRoomCode(string roomType)
        {
            if (roomType == "prv")
            {
                if (Settings.index >= Settings.roomsPrv.Count)
                {
                    Settings.index = 0;
                }

                return Settings.index < Settings.roomsPrv.Count ? Settings.roomsPrv[Settings.index] : string.Empty;
            }
            else
            {
                if (Settings.index2 >= Settings.roomsPub.Count)
                {
                    Settings.index2 = 0;
                }

                return Settings.index2 < Settings.roomsPub.Count ? Settings.roomsPub[Settings.index2] : string.Empty;
            }
        }
        private static async Task ProcessRoomResponse(string responseContent, string roomCode, string region, string roomType)
        {
            try
            {
                var responseJson = JsonDocument.Parse(responseContent);

                if (responseJson.RootElement.TryGetProperty("data", out JsonElement dataElement))
                {
                    if (dataElement.TryGetProperty("Data", out JsonElement dataItems))
                    {
                        if (dataItems.GetRawText() == "{}")
                        {
                            if (!Settings.DontRemoveCodes.Contains(roomCode))
                            {
                                if (Settings.roomsPrv.Contains(roomCode))
                                {
                                    Settings.roomsPrv.Remove(roomCode);
                                }

                                if (Settings.roomsPub.Contains(roomCode))
                                {
                                    Settings.roomsPub.Remove(roomCode);
                                }

                                roomCooldowns[roomCode] = DateTime.UtcNow.AddMinutes(2);
                                Log($"Room {roomCode} temporarily removed (empty)");
                            }
                            return;
                        }

                        int playerCount = CountPlayersInRoom(dataItems);
                        foreach (JsonProperty item in dataItems.EnumerateObject())
                        {
                            JsonElement properties = item.Value;
                            if (properties.TryGetProperty("Value", out JsonElement valueElement))
                            {
                                string value = valueElement.GetString();
                                await CheckForSpecialCosmetics(value, roomCode, playerCount, region);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("ProcessRoomResponse", ex);
            }
        }
        private static int CountPlayersInRoom(JsonElement dataItems)
        {
            int count = 0;
            try
            {
                foreach (JsonProperty _ in dataItems.EnumerateObject())
                {
                    count++;
                }
            }
            catch (Exception)
            {
            }
            return count;
        }
        private static async Task CheckForSpecialCosmetics(string value, string roomCode, int playerCount, string region)
        {
            for (int i = 0; i < Settings.cosmetics.Length; i++)
            {
                if (value.Contains(Settings.cosmetics[i]))
                {
                    string title = "";
                    string thumbnailUrl = "";
                    string webhookType = "";

                    switch (i)
                    {
                        case 0:
                            title = "Player Found - Finger Painter Badge";
                            thumbnailUrl = "https://static.wikia.nocookie.net/gorillatag/images/b/b7/Fingerpaint.png/revision/latest/thumbnail/width/360/height/360?cb=20231114024321";
                            webhookType = "pro";
                            break;
                        case 1:
                            title = "Player Found - Illustrator Badge";
                            thumbnailUrl = "https://static.wikia.nocookie.net/gorillatag/images/2/22/IllustratorbadgeTransparent.png/revision/latest/thumbnail/width/360/height/360?cb=20240612230801";
                            webhookType = "pro";
                            break;
                        case 2:
                            title = "Player Found - Administrator Badge";
                            thumbnailUrl = "https://static.wikia.nocookie.net/gorillatag/images/4/40/Adminbadge.png/revision/latest/thumbnail/width/360/height/360?cb=20220223233745";
                            webhookType = "pro";
                            break;
                        case 3:
                            title = "Player Found - Moderator Stick";
                            thumbnailUrl = "https://static.wikia.nocookie.net/gorillatag/images/a/aa/Stick.png/revision/latest?cb=20231102195128";
                            webhookType = "pro";
                            break;
                        case 4:
                            title = "Player Found - Cold Monke Sweater";
                            thumbnailUrl = "https://static.wikia.nocookie.net/gorillatag/images/9/9d/SweaterWinter23GTSprite.png/revision/latest/thumbnail/width/360/height/360?cb=20230127222427";
                            webhookType = "pro";
                            break;
                        case 5:
                            title = "Player Found - 2022 Glasses";
                            thumbnailUrl = "https://encrypted-tbn0.gstatic.com/images?q=tbn:ANd9GcTysnxY8_8v5HAgUhZOMAz6iz9liIXcFxZR-kpO0VXERLP2I9wcMdomy6SHZ1Ir_b-CMIA&usqp=CAU";
                            webhookType = "free";
                            break;
                        case 6:
                            title = "Player Found - GT1 Badge";
                            thumbnailUrl = "https://static.wikia.nocookie.net/gorillatag/images/8/88/Gt1.png/revision/latest/thumbnail/width/360/height/360?cb=20220223233019";
                            webhookType = "free";
                            break;
                    }

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(thumbnailUrl) && !string.IsNullOrEmpty(webhookType))
                    {
                        await SendDiscordWebhook(title, roomCode, playerCount, region, thumbnailUrl, webhookType);
                        totalPlayersFound++;
                    }
                }
            }
        }
        private static async Task SendDiscordWebhook(string title, string roomCode, int playerCount, string region, string thumbnailUrl, string webhookType)
        {
            if (title == lastTitle && roomCode == lastRoom)
            {
                return;
            }

            string webhookUrl;
            string content = "";

            if (webhookType == "free")
            {
                webhookUrl = Settings.WebHookFree;
            }
            else if (webhookType == "pro")
            {
                webhookUrl = Settings.WebHookPro;
            }
            else
            {
                return;
            }

            if (string.IsNullOrEmpty(webhookUrl))
            {
                Log($"No webhook URL configured for {webhookType}");
                return;
            }

            switch (title)
            {
                case "Player Found - Finger Painter Badge":
                    content = "||<@&1265507746535575623>||";
                    break;
                case "Player Found - Illustrator Badge":
                    content = "||<@&1266147098513113168>||";
                    break;
                case "Player Found - Administrator Badge":
                    content = "||<@&1265511219872137321>||";
                    break;
                case "Player Found - Moderator Stick":
                    content = "||<@&1265511376605020160>||";
                    break;
            }

            string time = DateTime.Now.ToString("h:mm tt") + " Central Time";
            var payload = new
            {
                content = content,
                embeds = new[]
                {
                    new
                    {
                        title = title,
                        description = $"Room: **{roomCode}**\nPlayers: **{playerCount}/10**\nTime: **{time}**\nRegion: **{region}**",
                        color = 0xFF0000,
                        thumbnail = new { url = thumbnailUrl }
                    }
                }
            };

            string payloadJson = JsonSerializer.Serialize(payload);
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, webhookUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };

            try
            {
                HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    Log($"Webhook sent: {title} in room {roomCode}");
                    totalWebhooksSent++;

                    lastTitle = title;
                    lastRoom = roomCode;
                }
                else
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    LogError("SendWebhook", new Exception($"Failed to send webhook: {response.StatusCode}\n{responseContent}"));
                }
            }
            catch (Exception ex)
            {
                LogError("SendWebhook", ex);
            }
        }
        public static void Log(string message)
        {
            try
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                Console.WriteLine(logMessage);

                string logFile = Path.Combine(LOG_PATH, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
                File.AppendAllText(logFile, logMessage + Environment.NewLine);
            }
            catch
            {
            }
        }
        public static void LogError(string context, Exception ex)
        {
            try
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in {context}: {ex.Message}\n{ex.StackTrace}";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR in {context}: {ex.Message}");
                Console.ResetColor();

                string logFile = Path.Combine(LOG_PATH, $"errors_{DateTime.Now:yyyy-MM-dd}.txt");
                File.AppendAllText(logFile, logMessage + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}