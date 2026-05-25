using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AutomacaoPromobTeste.Network{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Serviço de notificações externas via Telegram Bot.
        /// As credenciais são configuradas no network.json (telegramBotToken + telegramChatId).
        /// As notificações são enviadas de forma assíncrona (fire-and-forget) para não bloquear a automação.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class NotificationService{

        private static readonly HttpClient _http = new(){ Timeout = TimeSpan.FromSeconds(10) };

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Envia um alerta de falha ao Telegram. Não faz nada se as credenciais não estiverem configuradas.
            /// </summary>
            /// <param name="nomeArquivo">Nome do arquivo que falhou no processamento.</param>
            /// <param name="mensagemErro">Descrição do erro ocorrido.</param>
        //--------------------------------------------------------------------------------------
        public static void EnviarAlertaFalha(string nomeArquivo, string mensagemErro){
            var token = NetworkSettings.TelegramBotToken;
            var chatId = NetworkSettings.TelegramChatId;

            // Sem credenciais configuradas, notificação é silenciosamente ignorada
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId)) return;

            // Fire-and-forget: não bloqueia a thread da automação
            _ = EnviarTelegramAsync(token, chatId, nomeArquivo, mensagemErro);
        }

        private static async Task EnviarTelegramAsync(string token, string chatId, string nomeArquivo, string erro){
            try{
                var texto =
                    $"⚠️ *ERRO - Automação Promob*\n\n" +
                    $"📁 Arquivo: `{nomeArquivo}`\n" +
                    $"❌ Erro: {erro}\n" +
                    $"🕐 {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

                var encoded = Uri.EscapeDataString(texto);
                var url = $"https://api.telegram.org/bot{token}/sendMessage?chat_id={chatId}&text={encoded}&parse_mode=Markdown";

                await _http.GetAsync(url);
            }
            catch{
                // Programação Defensiva: falha na notificação jamais deve crashar a automação
            }
        }
    }
}
