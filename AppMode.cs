namespace PromobAutomacao{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Define o modo de operação da aplicação ao ser iniciada.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public enum AppRunMode { Local, Server, Client }

    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Configurações globais do modo de execução selecionado na tela de inicialização.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class AppMode{
        /// <summary>Modo de operação ativo (Local, Servidor ou Cliente).</summary>
        public static AppRunMode Mode { get; set; } = AppRunMode.Local;

        /// <summary>Endereço IP do servidor ao qual o cliente deve se conectar.</summary>
        public static string ServerHost { get; set; } = "localhost";

        /// <summary>Porta TCP usada pelo servidor e pelo cliente.</summary>
        public static int Port { get; set; } = 8085;

        /// <summary>Indica se o cliente está conectado em modo de apenas visualização (espectador).</summary>
        public static bool IsSpectator { get; set; } = true;
    }
}
