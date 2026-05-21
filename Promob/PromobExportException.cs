using System;

namespace AutomacaoPromobTeste.Promob{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Exceção específica lançada quando a exportação ERP do Promob falha com "Abortado com erro!".
        /// Sinaliza ao fluxo principal que o arquivo deve ser movido para a pasta "promob erro".
        /// </summary>
    //--------------------------------------------------------------------------------------
    public class PromobExportException : Exception{
        public PromobExportException(string message) : base(message) { }
        public PromobExportException(string message, Exception innerException) : base(message, innerException) { }
    }
}
