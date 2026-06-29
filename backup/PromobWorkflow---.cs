using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using PromobAutomacao.Automation;
using PromobAutomacao.Utils;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace PromobAutomacao.Promob{
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

            // Reseta sinalizadores de estado para o novo arquivo
            AutomacaoEstado.FechouProjetoAtual = false;

            AppLogs.LogWorkflowLocalizandoJanelaPromob();
            var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 300000)
                ?? throw new Exception("Janela do Promob não encontrada. O Promob está aberto?");

            token.ThrowIfCancellationRequested();

            int currentPid = janela.Properties.ProcessId.ValueOrDefault;
            
            // Se o Promob foi reiniciado ou fechado entre execuções, o ID do processo muda.
            // Nesse caso, limpamos as referências do cache da árvore visual para evitar erros de ponteiro antigo.
            if (PromobWindowHelper.CachedProcessIdPromob.HasValue && PromobWindowHelper.CachedProcessIdPromob.Value != currentPid){
                AppLogs.LogWorkflowNovoProcessIdDetectado();
                WindowFinder.CachedHost = null;
            }

            PromobWindowHelper.CachedProcessIdPromob = currentPid;

            InteractionHelper.AtivarJanela(janela);

            token.ThrowIfCancellationRequested();

            // Verifica e fecha o popup de conclusão do update ('PromobUpdate') se estiver aberto
            try {
                PromobUpdater.VerificarEFecharPopupSucessoSeAberto(automation);
            }
            catch (Exception ex) {
                AppLogs.LogWorkflowFalhaVerificarPopupProcessamento(ex.Message);
            }

            token.ThrowIfCancellationRequested();

            token.ThrowIfCancellationRequested();

            // Garante que o Promob está na tela inicial antes de importar.
            // Se houver um projeto aberto (sessão anterior não finalizada), fecha primeiro.
            AppLogs.LogWorkflowVerificandoEstadoInicial();
            Diagnostics.Medir("Verificar e fechar projeto pendente", () => PromobFecharProjeto.FecharProjetoPendenteSeNecessario(automation, janela));

            token.ThrowIfCancellationRequested();

            // Loop de retry para garantir que o wizard correto foi aberto (com o botão "...").
            // O Promob pode abrir um wizard de importação via sistema cloud/ERP (sem o botão "...").
            // Nesse caso, fechamos e tentamos novamente até obter a tela correta com "Caminho" + "...".
            Window? janelaWizard = null;
            int tentativaWizard = 0;
            const int maxTentativasWizard = 10;


            // AppLogs.LogWorkflowProcurandoJanelaBotoes();
            // var janelaInicial = PromobWindowHelper.AguardarJanelaPromob(automation, 5000);
            // if (janelaInicial != null){
            //     Diagnostics.ListarBotoesProject(janelaInicial, PromobConfig.AutomationIdHost);
            // }

            while (tentativaWizard < maxTentativasWizard){
                token.ThrowIfCancellationRequested();
                tentativaWizard++;
                AppLogs.LogWorkflowPasso1AbrindoAssistente(tentativaWizard, maxTentativasWizard);
                InteractionHelper.AtivarJanela(janela);
                Diagnostics.Medir("Clicar botão Importar", () => PromobImportador.ClicarBotaoImportar(janela));

                token.ThrowIfCancellationRequested();

                AppLogs.LogWorkflowAguardandoWizard();
                // Espera um momento para o wizard abrir
                InteractionHelper.EsperarUiRespirar(1500);

                var popupAviso = PromobWindowHelper.EncontrarPopupAtencao(automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob);
                if (popupAviso != null) {
                    var textoPopup = popupAviso.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text))?.Properties.Name.ValueOrDefault ?? "";
                    if (textoPopup.Contains("Operação não autorizada", StringComparison.OrdinalIgnoreCase)) {
                        AppLogs.LogWorkflowOperacaoNaoAutorizada();
                        
                        var btnOk = popupAviso.FindFirstDescendant(cf => 
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnOk).Or(cf.ByName(PromobConfig.BtnOkAlt)).Or(cf.ByName(PromobConfig.BtnConcluir))));
                        
                        if (btnOk != null) InteractionHelper.ClicarComFallback(btnOk);
                        else {
                            InteractionHelper.AtivarJanela(popupAviso);
                            Keyboard.Type(VirtualKeyShort.RETURN);
                        }
                        
                        InteractionHelper.EsperarUiRespirar(1500);
                        
                        // Atualiza a interface clicando nas abas Clientes e Projetos para forçar o desbloqueio
                        AppLogs.LogWorkflowForcarClientes();
                        
                        var abaClientes = janela.FindFirstDescendant(cf => cf.ByName("Clientes").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))))
                                       ?? janela.FindFirstDescendant(cf => cf.ByName("Clientes"))
                                       ?? janela.FindAllDescendants().FirstOrDefault(e => (e.Name ?? "").Equals("Clientes", StringComparison.OrdinalIgnoreCase));
                        
                        if (abaClientes != null) {
                            InteractionHelper.ClicarComFallback(abaClientes);
                            InteractionHelper.EsperarUiRespirar(1500);
                        } else {
                            AppLogs.LogWorkflowAbaClientesNaoEncontrada();
                        }

                        AppLogs.LogWorkflowRetornandoProjetos();
                        var abaProjetos = janela.FindFirstDescendant(cf => cf.ByName("Projetos").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))))
                                       ?? janela.FindFirstDescendant(cf => cf.ByName("Projetos"))
                                       ?? janela.FindAllDescendants().FirstOrDefault(e => (e.Name ?? "").Equals("Projetos", StringComparison.OrdinalIgnoreCase));
                        
                        if (abaProjetos != null) {
                            InteractionHelper.ClicarComFallback(abaProjetos);
                            InteractionHelper.EsperarUiRespirar(1500);
                        } else {
                            AppLogs.LogWorkflowAbaProjetosNaoEncontrada();
                        }

                        WindowFinder.CachedHost = null; // reseta cache para buscar botões novamente
                        continue; // Recomeça a etapa de clicar em Importar
                    }
                }

                var wizardEncontrado = PromobWindowHelper.EncontrarJanelaWizard(automation, janela) ?? janela;

                token.ThrowIfCancellationRequested();

                // Verifica se o wizard correto foi aberto (aquele com o botão "...")
                if (PromobImportador.VerificarBotaoBrowseNoWizard(wizardEncontrado)){
                    janelaWizard = wizardEncontrado;
                    AppLogs.LogWorkflowWizardConfirmado(tentativaWizard);
                    break;
                }

                token.ThrowIfCancellationRequested();

                // Wizard errado: fecha e tenta de novo
                AppLogs.LogWorkflowWizardIncorreto(tentativaWizard);
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

            AppLogs.LogWorkflowPasso2SelecionandoArquivo();
            Diagnostics.Medir("Selecionar arquivo", () => PromobImportador.AbrirDialogoEPreencher(automation, janelaWizard, caminhoArquivo));

            token.ThrowIfCancellationRequested();

            AppLogs.LogWorkflowClicandoAvancar();
            InteractionHelper.AtivarJanela(janelaWizard);
            Diagnostics.Medir("Avançar wizard", () => PromobImportador.ClicarAvancarWizard(automation, janelaWizard));

            token.ThrowIfCancellationRequested();

            AppLogs.LogWorkflowPasso3Importando();
            Diagnostics.Medir("Aguardar importação", () => PromobImportador.AguardarImportacaoETratarPopups(automation, janelaWizard));

            token.ThrowIfCancellationRequested();

            AppLogs.LogWorkflowPasso4Abrindo();
            Diagnostics.Medir("Abrir projeto", () => PromobCarregadorProjeto.AbrirProjetoSelecionado(janela));

            token.ThrowIfCancellationRequested();

            
            Diagnostics.Medir("Abrir Promob ERP", () => PromobExportadorErp.AbrirIntegradorErp(automation, janela));

            PromobExportException? erroExportacao = null;
            try{
                Diagnostics.Medir("Exportação ERP", () => PromobExportadorErp.AguardarExportacaoErp(automation, janela));
            }
            catch (PromobExportException ex){
                erroExportacao = ex;
            }
            

            AppLogs.LogWorkflowFechandoProjeto();
            Diagnostics.Medir("Fechar projeto", () => PromobFecharProjeto.Fechar(automation, janela));

            // Sinaliza ao monitor de atualização que o projeto foi fechado (janela Update pode prosseguir)
            AutomacaoEstado.FechouProjetoAtual = true;
            AppLogs.LogWorkflowSinalFechouProjetoEmitido();

            token.ThrowIfCancellationRequested();

            // Se houve erro na exportação, relança a exceção APÓS fechar o projeto
            if (erroExportacao != null){
                throw erroExportacao;
            }

            AppLogs.LogWorkflowFluxoConcluido();
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
            AppLogs.LogWorkflowEscaneandoElementos(processId);

            var all = janela.FindAllDescendants()
                .Where(e => !string.IsNullOrEmpty(e.Name) || !string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault))
                .GroupBy(e => e.ControlType.ToString() + "|" + (e.Properties.AutomationId.ValueOrDefault ?? "") + "|" + (e.Name ?? ""))
                .Select(g => g.First())
                .ToList();

            AppLogs.LogWorkflowElementosEncontrados(all.Count);

            foreach (var e in all){
                AppLogs.LogDetalheElementoUI(e.ControlType.ToString(), e.Name, e.Properties.AutomationId.ValueOrDefault);
            }

            AppLogs.LogDivisor();
        }
    }
}
