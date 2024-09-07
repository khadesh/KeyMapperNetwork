using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using WindowsInput;

namespace NetworkConsoleApp
{
    class Program
    {
        static string settingsFilePath = "program.settings";
        static Settings settings;
        static Dictionary<char, char> keyMappings = new Dictionary<char, char>();
        static UdpClient udpClient;
        static bool isHost = false;
        static List<IPEndPoint> clients = new List<IPEndPoint>();

        static void Main(string[] args)
        {
            settings = LoadSettings(settingsFilePath) ?? new Settings();
            LoadKeyMappings();

            while (true)
            {
                Console.Write("Enter command (h - host, k - map keys, j - join, q - quit): ");
                string input = Console.ReadLine();

                if (input == "h")
                {
                    StartHost();
                }
                else if (input == "k")
                {
                    MapKeys();
                }
                else if (input == "j")
                {
                    JoinServer();
                }
                else if (input == "q")
                {
                    Console.WriteLine("Exiting application.");
                    break;
                }
                else
                {
                    Console.WriteLine("Invalid command.");
                }
            }
        }

        static void StartHost()
        {
            Console.WriteLine("[LOG] Starting host...");
            isHost = true;
            udpClient = new UdpClient(11000);
            Console.WriteLine("Hosting server on port 11000...");

            udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            Console.WriteLine("Press any key to send, 'q' to stop hosting.");

            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                char key = keyInfo.KeyChar;

                if (key == 'q')
                {
                    Console.WriteLine("[LOG] Stopping host.");
                    udpClient.Close();
                    break;
                }

                if (keyMappings.ContainsKey(key))
                {
                    SendKeyToClients(keyMappings[key]);
                }
            }
        }

        static void SendKeyToClients(char key)
        {
            Console.WriteLine($"[LOG] Sending key: {key}");
            byte[] message = Encoding.UTF8.GetBytes(key.ToString());
            foreach (var client in clients)
            {
                udpClient.Send(message, message.Length, client);
                Console.WriteLine($"[LOG] Sent key: {key} to {client.Address}");
            }
        }

        static void MapKeys()
        {
            Console.WriteLine("Enter key mappings (format: a=b,c=d, etc.): ");
            string mappings = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(mappings))
            {
                Console.WriteLine("Invalid input.");
                return;
            }

            foreach (var mapping in mappings.Split(','))
            {
                var keyValue = mapping.Split('=');
                if (keyValue.Length == 2 && keyValue[0].Length == 1 && keyValue[1].Length == 1)
                {
                    keyMappings[keyValue[0][0]] = keyValue[1][0];
                }
            }

            SaveKeyMappings();
            Console.WriteLine("Key mappings saved.");
        }

        static void JoinServer()
        {
            Console.WriteLine("[LOG] Attempting to join server...");
            Console.Write("Enter the server IP address: ");
            string ipAddress = Console.ReadLine();

            if (IPAddress.TryParse(ipAddress, out IPAddress ip))
            {
                settings.LastUsedIPAddress = ipAddress;
                SaveSettings(settingsFilePath, settings);

                udpClient = new UdpClient();
                udpClient.Connect(ip, 11000);

                // Notify host of the new connection
                string localIp = GetLocalIPAddress();
                byte[] message = Encoding.UTF8.GetBytes($"new:{localIp}");
                udpClient.Send(message, message.Length);
                Console.WriteLine($"[LOG] Notified host of new connection from IP: {localIp}");

                Console.WriteLine("Connected to server. Waiting for key events...");

                udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);

                while (true)
                {
                    // Client just waits for incoming key events
                    if (Console.ReadKey(true).KeyChar == 'q')
                    {
                        Console.WriteLine("[LOG] Disconnecting from server.");
                        udpClient.Close();
                        break;
                    }
                }
            }
            else
            {
                Console.WriteLine("[LOG] Invalid IP address.");
            }
        }

        static void ReceiveCallback(IAsyncResult ar)
        {
            Console.WriteLine("[LOG] Receiving data...");
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 11000);
            byte[] receivedData = udpClient.EndReceive(ar, ref remoteEndPoint);

            if (receivedData.Length > 0)
            {
                string message = Encoding.UTF8.GetString(receivedData);
                if (message.StartsWith("new:"))
                {
                    string newClientIp = message.Split(':')[1];
                    Console.WriteLine($"[LOG] New client joined from IP: {newClientIp}");
                    clients.Add(new IPEndPoint(IPAddress.Parse(newClientIp), 11000));
                }
                else
                {
                    char receivedKey = message[0];
                    Console.WriteLine($"[LOG] Received key: {receivedKey}");

                    // Simulate the key press on the client machine
                    SimulateKeyPress(receivedKey);
                }
            }

            udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
        }

        static void SimulateKeyPress(char key)
        {
            Console.WriteLine($"[LOG] Simulating key press: {key}");
            var sim = new InputSimulator();
            sim.Keyboard.TextEntry(key);
            Console.WriteLine($"Simulated key press: {key}");
        }

        static void SaveKeyMappings()
        {
            settings.KeyMappings = keyMappings.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.ToString());
            SaveSettings(settingsFilePath, settings);
            Console.WriteLine("[LOG] Key mappings saved.");
        }

        static void LoadKeyMappings()
        {
            Console.WriteLine("[LOG] Loading key mappings...");
            if (settings.KeyMappings != null)
            {
                keyMappings = settings.KeyMappings.ToDictionary(kvp => kvp.Key[0], kvp => kvp.Value[0]);
            }
        }

        static void SaveSettings(string filePath, Settings settings)
        {
            Console.WriteLine("[LOG] Saving settings...");
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        static Settings LoadSettings(string filePath)
        {
            Console.WriteLine("[LOG] Loading settings...");
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<Settings>(json);
            }
            return null;
        }

        static string GetLocalIPAddress()
        {
            Console.WriteLine("[LOG] Getting local IP address...");
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }

    class Settings
    {
        public string LastUsedIPAddress { get; set; }
        public Dictionary<string, string> KeyMappings { get; set; } = new Dictionary<string, string>();
    }
}
