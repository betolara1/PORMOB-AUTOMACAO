namespace PromobAutomacao{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Estado compartilhado entre o loop de automação e o monitor de atualização do Promob.
        /// Usado para coordenar o momento correto de executar a atualização sem interromper
        /// um processamento de arquivo em andamento.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class AutomacaoEstado{

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Indica se há um arquivo sendo processado pelo workflow de automação no momento.
            /// Setado para <c>true</c> antes de <see cref="Promob.PromobWorkflow.ProcessarArquivo"/> e
            /// para <c>false</c> logo após (no bloco finally do loop principal).
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static volatile bool ArquivoEmProcessamento = false;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Setado para <c>true</c> assim que o passo 9/9 (fechar projeto) for concluído
            /// dentro de <see cref="Promob.PromobWorkflow.ProcessarArquivo"/>.
            /// Resetado para <c>false</c> no início de cada novo arquivo.
            /// O monitor de atualização aguarda esse sinal antes de acionar o Promob Update.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static volatile bool FechouProjetoAtual = false;

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Indica se há uma atualização automática do Promob em execução no momento.
        /// O loop principal de automação de arquivos aguarda esse sinal ficar <c>false</c>
        /// antes de carregar ou processar qualquer novo arquivo.
        /// </summary>
        //--------------------------------------------------------------------------------------
        public static volatile bool AtualizacaoEmAndamento = false;
    }
}
