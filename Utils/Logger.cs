using System;
using System.IO;

namespace AutomacaoPromobTeste.Utils{
    public enum LogLevel { Error = 0, Warn = 1, Info = 2, Debug = 3 }

    public static class Logger{
        public static string LogPath { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "erros.log");
        public static LogLevel NivelAtual { get; set; } = LogLevel.Info;

        public static void Log(string mensagem, LogLevel nivel = LogLevel.Info){
            if (nivel <= NivelAtual){
                Console.WriteLine(mensagem);
            }
        }

        public static void RegistrarErro(string nomeArquivo, Exception ex){
            try{
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Arquivo: {nomeArquivo}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}"
                );
            }
            catch { }
        }
    }
}
