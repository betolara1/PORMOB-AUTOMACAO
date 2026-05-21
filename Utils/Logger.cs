using System;
using System.IO;

namespace AutomacaoPromobTeste.Utils{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Define os níveis de severidade hierárquicos para o sistema de registros de logs.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public enum LogLevel { Error = 0, Warn = 1, Info = 2, Debug = 3 }

    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Gerenciador de logs simples responsável por emitir mensagens informativas no console
        /// e gravar exceções críticas de execução em arquivos físicos locais de forma resiliente.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class Logger{
        
        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Caminho absoluto do arquivo em disco ("erros.log") onde as falhas críticas serão persistidas.
            /// </summary>
        //--------------------------------------------------------------------------------------
        // Usa AppDomain.CurrentDomain.BaseDirectory para garantir que o arquivo seja gerado exatamente na mesma
        // pasta onde o executável do robô está rodando, ideal para automações que rodam sob Agendador de Tarefas.
        public static string LogPath { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "erros.log");

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Nível atual de verbosidade configurado para a emissão de logs em tela.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static LogLevel NivelAtual { get; set; } = LogLevel.Info;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Registra uma mensagem na tela do console se o nível especificado for menor ou igual à verbosidade atual de logs.
            /// </summary>
            /// <param name="mensagem">A mensagem explicativa ou informativa a ser exibida.</param>
            /// <param name="nivel">O nível de severidade atribuído a esta mensagem (padrão é Info).</param>
        //--------------------------------------------------------------------------------------
        public static void Log(string mensagem, LogLevel nivel = LogLevel.Info){
            // Hierarquia de logs: se a mensagem for mais severa ou igual ao NívelAtual configurado,
            // ela é impressa no console (ex: se o nível atual for Info (2), exibe Info, Warn e Error).
            if (nivel <= NivelAtual){
                Console.WriteLine(mensagem);
            }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Grava uma exceção crítica com carimbo de data, hora e o contexto do arquivo no arquivo "erros.log" local.
            /// </summary>
            /// <param name="nomeArquivo">O nome do arquivo/projeto que estava em processamento quando o erro ocorreu.</param>
            /// <param name="ex">A exceção capturada (Exception) contendo stack trace e detalhes técnicos.</param>
        //--------------------------------------------------------------------------------------
        public static void RegistrarErro(string nomeArquivo, Exception ex){
            try{
                // Abre (ou cria) o arquivo erros.log e concatena o log de erro sem travar a escrita concorrente
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Arquivo: {nomeArquivo}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}"
                );
            }
            catch {
                // Programação Defensiva: o bloco catch está vazio intencionalmente para garantir que, 
                // se a gravação física falhar (ex: disco cheio, permissão negada), o robô de automação
                // NÃO crashe por conta do log, permitindo que a aplicação continue tentando outras ações.
            }
        }
    }
}
