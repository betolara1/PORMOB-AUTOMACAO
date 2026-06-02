using System.Text.Json;
using System.Text.Json.Serialization;

namespace PromobAutomacao.Network{

    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Tipos possíveis de mensagem trocadas entre servidor e cliente via TCP.
        /// </summary>
    //--------------------------------------------------------------------------------------
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MessageType { Log, Metrics, Command, Heartbeat }

    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Modelo de mensagem serializado em JSON para comunicação via TCP entre
        /// o Servidor (Computador X) e o Cliente (Computador Y).
        /// </summary>
    //--------------------------------------------------------------------------------------
    public class WsMessage{

        private static readonly JsonSerializerOptions _opts = new(){
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        [JsonPropertyName("type")]
        public MessageType Type { get; set; }

        /// <summary>Texto da mensagem de log.</summary>
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        /// <summary>Nível de severidade do log ("Info", "Warn", "Error", "Debug").</summary>
        [JsonPropertyName("level")]
        public string? Level { get; set; }

        /// <summary>Número de arquivos processados com sucesso.</summary>
        [JsonPropertyName("sucessos")]
        public int Sucessos { get; set; }

        /// <summary>Número de erros registrados.</summary>
        [JsonPropertyName("erros")]
        public int Erros { get; set; }

        /// <summary>Status textual da automação ("Monitorando" ou "Parado").</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Ação do comando a ser executado no servidor.</summary>
        [JsonPropertyName("action")]
        public string? Action { get; set; }

        /// <summary>Indica se o processo Promob está em execução no servidor.</summary>
        [JsonPropertyName("promobRunning")]
        public bool PromobRunning { get; set; }

        /// <summary>Indica se há uma atualização em andamento no servidor.</summary>
        [JsonPropertyName("updating")]
        public bool Updating { get; set; }

        /// <summary>Serializa a mensagem para uma linha JSON.</summary>
        public string Serialize() => JsonSerializer.Serialize(this, _opts);

        /// <summary>Desserializa uma linha JSON para WsMessage. Retorna null em caso de falha.</summary>
        public static WsMessage? Deserialize(string json){
            try{ return JsonSerializer.Deserialize<WsMessage>(json, _opts); }
            catch{ return null; }
        }

        // ==========================================
        // --- Factory Methods ---
        // ==========================================

        public static WsMessage CreateLog(string text, string level) => new(){
            Type = MessageType.Log, Text = text, Level = level
        };

        public static WsMessage CreateMetrics(int sucessos, int erros, string status, bool promobRunning, bool updating = false) => new(){
            Type = MessageType.Metrics, Sucessos = sucessos, Erros = erros, Status = status, PromobRunning = promobRunning, Updating = updating
        };

        public static WsMessage CreateCommand(string action) => new(){
            Type = MessageType.Command, Action = action
        };

        public static WsMessage CreateHeartbeat() => new(){ Type = MessageType.Heartbeat };
    }
}
