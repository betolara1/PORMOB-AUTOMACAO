using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PromobAutomacao.Network{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Gerenciador de configurações de rede persistidas no arquivo "network.json".
        /// Carregado automaticamente no startup via inicializador estático.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class NetworkSettings{

        private static readonly string _path =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "network.json");

        /// <summary>Porta TCP do servidor (padrão: 8085).</summary>
        public static int Port { get; set; } = 8085;

        /// <summary>Token do Bot do Telegram para alertas de falha.</summary>
        public static string TelegramBotToken { get; private set; } = "";

        /// <summary>Chat ID do Telegram que receberá as notificações.</summary>
        public static string TelegramChatId { get; private set; } = "";

        /// <summary>Lista dos IPs de servidor usados recentemente (máximo 5).</summary>
        public static string[] RecentServerIps { get; private set; } = Array.Empty<string>();

        static NetworkSettings() => Load();

        //--------------------------------------------------------------------------------------
            /// <summary>Carrega as configurações do arquivo network.json.</summary>
        //--------------------------------------------------------------------------------------
        public static void Load(){
            try{
                if (!File.Exists(_path)){ Save(); return; }

                using var doc = JsonDocument.Parse(File.ReadAllText(_path));
                var root = doc.RootElement;

                if (root.TryGetProperty("port", out var p)) Port = p.GetInt32();
                if (root.TryGetProperty("telegramBotToken", out var t)) TelegramBotToken = t.GetString() ?? "";
                if (root.TryGetProperty("telegramChatId", out var c)) TelegramChatId = c.GetString() ?? "";

                if (root.TryGetProperty("recentServerIps", out var ips) && ips.ValueKind == JsonValueKind.Array){
                    RecentServerIps = ips.EnumerateArray()
                        .Select(x => x.GetString() ?? "")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();
                }
            }
            catch{ }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>Adiciona um IP à lista de recentes e persiste.</summary>
        //--------------------------------------------------------------------------------------
        public static void AddRecentIp(string ip){
            var list = new List<string>(RecentServerIps);
            list.Remove(ip);
            list.Insert(0, ip);
            RecentServerIps = list.Take(5).ToArray();
            Save();
        }

        //--------------------------------------------------------------------------------------
            /// <summary>Persiste as configurações atuais no arquivo network.json.</summary>
        //--------------------------------------------------------------------------------------
        public static void Save(){
            try{
                var data = new{
                    port = Port,
                    telegramBotToken = TelegramBotToken,
                    telegramChatId = TelegramChatId,
                    recentServerIps = RecentServerIps
                };
                File.WriteAllText(_path, JsonSerializer.Serialize(data, new JsonSerializerOptions{ WriteIndented = true }));
            }
            catch{ }
        }
    }
}
