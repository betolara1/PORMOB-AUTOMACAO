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
            /// <param name="token">O token de cancelamento para interrupção imediata.</param>
        //--------------------------------------------------------------------------------------
        public static void ProcessarArquivo(UIA3Automation automation, string caminhoArquivo, CancellationToken token = default){
            token.ThrowIfCancellationRequested();

            Logger.Log("  [1/8] Localizando janela do Promob...");
            var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 300000)
                ?? throw new Exception("Janela do Promob não encontrada. O Promob está aberto?");

            token.ThrowIfCancellationRequested();

            int currentPid = janela.Properties.ProcessId.ValueOrDefault;
            
            // Se o Promob foi reiniciado ou fechado entre execuções, o ID do processo muda.
            // Nesse caso, limpamos as referências do cache da árvore visual para evitar erros de ponteiro antigo.
            if (PromobWindowHelper.CachedProcessIdPromob.HasValue && PromobWindowHelper.CachedProcessIdPromob.Value != currentPid){
                Logger.Log("  [INFO] Novo ProcessId detectado. Invalidando cache de UI.");
                WindowFinder.CachedHost = null;
            }

            PromobWindowHelper.CachedProcessIdPromob = currentPid;

            InteractionHelper.AtivarJanela(janela);

            token.ThrowIfCancellationRequested();

            // Garante que o Promob está na tela inicial antes de importar.
            // Se houver um projeto aberto (sessão anterior não finalizada), fecha primeiro.
            Logger.Log("  [1.5/8] Verificando estado inicial do Promob...");
            Diagnostics.Medir("Verificar e fechar projeto pendente", () => PromobFecharProjeto.FecharProjetoPendenteSeNecessario(automation, janela));

            token.ThrowIfCancellationRequested();

            // Loop de retry para garantir que o wizard correto foi aberto (com o botão "...").
            // O Promob pode abrir um wizard de importação via sistema cloud/ERP (sem o botão "...").
            // Nesse caso, fechamos e tentamos novamente até obter a tela correta com "Caminho" + "...".
            Window? janelaWizard = null;
            int tentativaWizard = 0;
            const int maxTentativasWizard = 10;

            while (tentativaWizard < maxTentativasWizard){
                token.ThrowIfCancellationRequested();
                tentativaWizard++;
                Logger.Log($"  [2/8] Acionando Importar... (Tentativa {tentativaWizard}/{maxTentativasWizard})");
                InteractionHelper.AtivarJanela(janela);
                Diagnostics.Medir("Clicar botão Importar", () => PromobImportador.ClicarBotaoImportar(janela));

                token.ThrowIfCancellationRequested();

                Logger.Log("  [3/8] Aguardando e verificando wizard de importação...");
                // Espera um momento para o wizard abrir
                InteractionHelper.EsperarUiRespirar(1500);
                var wizardEncontrado = PromobWindowHelper.EncontrarJanelaWizard(automation, janela) ?? janela;

                token.ThrowIfCancellationRequested();

                // Verifica se o wizard correto foi aberto (aquele com o botão "...")
                if (PromobImportador.VerificarBotaoBrowseNoWizard(wizardEncontrado)){
                    janelaWizard = wizardEncontrado;
                    Logger.Log($"  [OK] Wizard correto confirmado na tentativa {tentativaWizard}.");
                    break;
                }

                token.ThrowIfCancellationRequested();

                // Wizard errado: fecha e tenta de novo
                Logger.Log($"  [AVISO] Wizard incorreto na tentativa {tentativaWizard}. Fechando e tentando novamente...", LogLevel.Warn);
                PromobImportador.FecharWizardAtual(wizardEncontrado);
                // Pequena pausa para o Promob retornar à tela inicial antes de tentar novamente
                InteractionHelper.EsperarUiRespirar(1000);
                // Invalida cache para garantir nova busca limpa do botão Importar
                WindowFinder.CachedHost = null;
            }

            token.ThrowIfCancellationRequested();

            if (janelaWizard == null){
                throw new Exception($"Não foi possível abrir o wizard correto de importação após {maxTentativasWizard} tentativas. O botão '...' (Caminho) não apareceu.");
            }

            Logger.Log("  [3/8] Preenchendo caminho do arquivo no wizard...");
            Diagnostics.Medir("Selecionar arquivo", () => PromobImportador.AbrirDialogoEPreencher(automation, janelaWizard, caminhoArquivo));

            token.ThrowIfCancellationRequested();

            Logger.Log("  [4/8] Clicando em Avançar no Wizard...");
            InteractionHelper.AtivarJanela(janelaWizard);
            Diagnostics.Medir("Avançar wizard", () => PromobImportador.ClicarAvancarWizard(automation, janelaWizard));

            token.ThrowIfCancellationRequested();

            Logger.Log("  [5/8] Aguardando conclusão da importação...");
            Diagnostics.Medir("Aguardar importação", () => PromobImportador.AguardarImportacaoETratarPopups(automation, janelaWizard));

            token.ThrowIfCancellationRequested();

            Logger.Log("  [6/9] Abrindo o projeto recém-importado (primeiro da lista)...");
            Diagnostics.Medir("Abrir projeto", () => PromobCarregadorProjeto.AbrirProjetoSelecionado(janela));

            token.ThrowIfCancellationRequested();

            Logger.Log("  [9/9] Fechando o projeto atual...");
            Diagnostics.Medir("Fechar projeto", () => PromobFecharProjeto.Fechar(automation, janela));

            token.ThrowIfCancellationRequested();

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
