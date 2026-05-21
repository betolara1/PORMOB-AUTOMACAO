using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using AutomacaoPromobTeste.Automation;
using AutomacaoPromobTeste.Utils;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace AutomacaoPromobTeste.Promob{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Classe central (Orquestradora) responsável por executar todo o fluxo de automação (Workflow)
        /// do Promob, coordenando as etapas divididas em componentes de responsabilidade única.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobWorkflow{

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Executa a rotina completa de automação para um arquivo específico de projeto 3D.
            /// Coordena as etapas de inicialização, preenchimento de wizard, abertura de projeto, exportação e fechamento.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="caminhoArquivo">O caminho absoluto do arquivo do projeto a ser processado.</param>
        //--------------------------------------------------------------------------------------
        public static void ProcessarArquivo(UIA3Automation automation, string caminhoArquivo){
            Logger.Log("  [1/8] Localizando janela do Promob...");
            var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 60000)
                ?? throw new Exception("Janela do Promob não encontrada. O Promob está aberto?");

            int currentPid = janela.Properties.ProcessId.ValueOrDefault;
            
            // Se o Promob foi reiniciado ou fechado entre execuções, o ID do processo muda.
            // Nesse caso, limpamos as referências do cache da árvore visual para evitar erros de ponteiro antigo.
            if (PromobWindowHelper.CachedProcessIdPromob.HasValue && PromobWindowHelper.CachedProcessIdPromob.Value != currentPid){
                Logger.Log("  [INFO] Novo ProcessId detectado. Invalidando cache de UI.");
                WindowFinder.CachedHost = null;
            }
            PromobWindowHelper.CachedProcessIdPromob = currentPid;

            InteractionHelper.AtivarJanela(janela);

            // Garante que o Promob está na tela inicial antes de importar.
            // Se houver um projeto aberto (sessão anterior não finalizada), fecha primeiro.
            Logger.Log("  [1.5/8] Verificando estado inicial do Promob...");
            Diagnostics.Medir("Verificar e fechar projeto pendente", () => PromobFecharProjeto.FecharProjetoPendenteSeNecessario(automation, janela));

            Logger.Log("  [2/8] Acionando Importar...");
            InteractionHelper.AtivarJanela(janela);
            Diagnostics.Medir("Clicar botão Importar", () => PromobImportador.ClicarBotaoImportar(janela));

            Logger.Log("  [3/8] Abrindo busca de arquivo e preenchendo caminho...");
            var janelaWizard = PromobWindowHelper.EncontrarJanelaWizard(automation, janela) ?? janela;
            Diagnostics.Medir("Selecionar arquivo", () => PromobImportador.AbrirDialogoEPreencher(automation, janelaWizard, caminhoArquivo));

            Logger.Log("  [4/8] Clicando em Avançar no Wizard...");
            InteractionHelper.AtivarJanela(janelaWizard);
            Diagnostics.Medir("Avançar wizard", () => PromobImportador.ClicarAvancarWizard(automation, janelaWizard));

            Logger.Log("  [5/8] Tratando popup de Novo Projeto...");
            Diagnostics.Medir("Tratar popup", () => PromobImportador.CancelarPopupNovoProjeto(automation));

            Logger.Log("  [6/9] Abrindo o projeto recém-importado (primeiro da lista)...");
            Diagnostics.Medir("Abrir projeto", () => PromobCarregadorProjeto.AbrirProjetoSelecionado(janela));

/*
            Logger.Log("  [7/9] Navegando até Ferramentas > Integradores > Promob ERP...");
            Diagnostics.Medir("Abrir Promob ERP", () => PromobExportadorErp.AbrirIntegradorErp(automation, janela));

            Logger.Log("  [8/9] Aguardando exportação XML do Promob ERP...");
            PromobExportException? erroExportacao = null;
            try{
                Diagnostics.Medir("Exportação ERP", () => PromobExportadorErp.AguardarExportacaoErp(automation, janela));
            }
            catch (PromobExportException ex){
                erroExportacao = ex;
                Logger.Log("  [AVISO] Exportação falhou. Fechando o projeto normalmente antes de sinalizar o erro...", LogLevel.Warn);
            }
*/
            Logger.Log("  [9/9] Fechando o projeto atual...");
            Diagnostics.Medir("Fechar projeto", () => PromobFecharProjeto.Fechar(automation, janela));

            // Se houve erro na exportação, relança a exceção APÓS fechar o projeto
            // if (erroExportacao != null){
            //     throw erroExportacao;
            // }

            Logger.Log("  [INFO] Fluxo concluído para este arquivo.");
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Rotina de auto-recuperação (Self-Healing) disparada quando ocorrem timeouts ou falhas inesperadas no fluxo principal.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void TentarRecuperar(UIA3Automation automation){
            PromobRecuperacao.TentarRecuperar(automation);
        }

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Lista os botões do projeto para ajudar a diagnosticar e encontrar os IDs na árvore de automação.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public static void ListarTodosBotoes(Window janela){
            var processId = janela.Properties.ProcessId.ValueOrDefault;
            Logger.Log($"[INFO] Escaneando TODOS os elementos da janela inteira para Processo: {processId}");

            var all = janela.FindAllDescendants()
                .Where(e => !string.IsNullOrEmpty(e.Name) || !string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault))
                .GroupBy(e => e.ControlType.ToString() + "|" + (e.Properties.AutomationId.ValueOrDefault ?? "") + "|" + (e.Name ?? ""))
                .Select(g => g.First())
                .ToList();

            Logger.Log($"[INFO] Foram encontrados {all.Count} elementos únicos com Nome ou ID na tela.");

            foreach (var e in all){
                Logger.Log($"  -> Tipo: {e.ControlType}, Nome: '{e.Name}', Id: '{e.Properties.AutomationId.ValueOrDefault}'");
            }

            Logger.Log("------------------------------------------");
        }
    }
}
