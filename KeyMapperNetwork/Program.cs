﻿using System;
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
        static UdpClient udpClient = new UdpClient();
        static bool isHost = false;

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
            isHost = true;
            udpClient = new UdpClient(11000);
            Console.WriteLine("Hosting server on port 11000...");

            udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            Console.WriteLine("Press 'q' to stop hosting.");

            while (true)
            {
                if (Console.ReadKey(true).KeyChar == 'q')
                {
                    Console.WriteLine("Stopping host.");
                    udpClient.Close();
                    break;
                }
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
            Console.Write("Enter the server IP address: ");
            string ipAddress = Console.ReadLine();

            if (IPAddress.TryParse(ipAddress, out IPAddress ip))
            {
                settings.LastUsedIPAddress = ipAddress;
                SaveSettings(settingsFilePath, settings);

                udpClient.Connect(ip, 11000);
                udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);

                // Notify host of the new connection
                string localIp = GetLocalIPAddress();
                byte[] message = Encoding.UTF8.GetBytes($"new:{localIp}");
                udpClient.Send(message, message.Length);

                Console.WriteLine("Connected to server. Press 'q' to quit.");
                while (true)
                {
                    if (Console.ReadKey(true).KeyChar == 'q')
                    {
                        Console.WriteLine("Disconnecting from server.");
                        udpClient.Close();
                        break;
                    }
                }
            }
            else
            {
                Console.WriteLine("Invalid IP address.");
            }
        }

        static void ReceiveCallback(IAsyncResult ar)
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 11000);
            byte[] receivedData = udpClient.EndReceive(ar, ref remoteEndPoint);

            if (receivedData.Length > 0)
            {
                string message = Encoding.UTF8.GetString(receivedData);
                if (message.StartsWith("new:"))
                {
                    string newClientIp = message.Split(':')[1];
                    Console.WriteLine($"New client joined: {newClientIp}");
                }
                else
                {
                    char receivedKey = message[0];
                    Console.WriteLine($"Received key: {receivedKey}");

                    // Simulate the key press on the client machine
                    if (keyMappings.ContainsValue(receivedKey))
                    {
                        Console.WriteLine($"Simulating key press: {receivedKey}");
                        SimulateKeyPress(receivedKey);
                    }
                }
            }

            if (isHost)
            {
                udpClient.BeginReceive(new AsyncCallback(ReceiveCallback), null);
            }
        }

        static void SimulateKeyPress(char key)
        {
            var sim = new InputSimulator();
            sim.Keyboard.TextEntry(key);
            Console.WriteLine($"Simulated key press: {key}");
        }

        static void SaveKeyMappings()
        {
            settings.KeyMappings = keyMappings.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.ToString());
            SaveSettings(settingsFilePath, settings);
        }

        static void LoadKeyMappings()
        {
            if (settings.KeyMappings != null)
            {
                keyMappings = settings.KeyMappings.ToDictionary(kvp => kvp.Key[0], kvp => kvp.Value[0]);
            }
        }

        static void SaveSettings(string filePath, Settings settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        static Settings LoadSettings(string filePath)
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<Settings>(json);
            }
            return null;
        }

        static string GetLocalIPAddress()
        {
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
