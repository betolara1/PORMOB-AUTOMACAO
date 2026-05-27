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

            // Reseta sinalizadores de estado para o novo arquivo
            AutomacaoEstado.FechouProjetoAtual = false;

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


            // Logger.Log("[INFO] Procurando janela do Promob para listar botões...");
            // var janelaInicial = PromobWindowHelper.AguardarJanelaPromob(automation, 5000);
            // if (janelaInicial != null){
            //     Diagnostics.ListarBotoesProject(janelaInicial, PromobConfig.AutomationIdHost);
            // }

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

                var popupAviso = PromobWindowHelper.EncontrarPopupAtencao(automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob);
                if (popupAviso != null) {
                    var textoPopup = popupAviso.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text))?.Properties.Name.ValueOrDefault ?? "";
                    if (textoPopup.Contains("Operação não autorizada", StringComparison.OrdinalIgnoreCase)) {
                        Logger.Log($"  [AVISO] Mensagem 'Operação não autorizada' detectada. Fechando popup e reiniciando a rotina de importação...", LogLevel.Warn);
                        
                        var btnOk = popupAviso.FindFirstDescendant(cf => 
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnOk).Or(cf.ByName(PromobConfig.BtnOkAlt)).Or(cf.ByName(PromobConfig.BtnConcluir))));
                        
                        if (btnOk != null) InteractionHelper.ClicarComFallback(btnOk);
                        else {
                            InteractionHelper.AtivarJanela(popupAviso);
                            Keyboard.Type(VirtualKeyShort.RETURN);
                        }
                        
                        InteractionHelper.EsperarUiRespirar(1500);
                        
                        // Navega Clientes → Projetos para desbloquear o botão Importar e retenta
                        NavegarClientesEProjetos(janela);
                        WindowFinder.CachedHost = null;
                        continue;
                    }
                }

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

            // Loop de retry para falhas de rede durante o preenchimento / avanço do wizard.
            // Se o Promob emitir erros de rede ao avançar, descartamos o wizard e recomeçamos do Importar.
            const int maxTentativasImportacao = 5;
            bool importacaoConcluida = false;

            for (int tentativaImp = 1; tentativaImp <= maxTentativasImportacao; tentativaImp++){
                token.ThrowIfCancellationRequested();

                // Se não é a primeira tentativa, precisamos reabrir o wizard antes de preencher
                if (tentativaImp > 1){
                    Logger.Log($"  [RETRY] Tentativa {tentativaImp}/{maxTentativasImportacao}: Reabrindo wizard de importação...");
                    InteractionHelper.AtivarJanela(janela);
                    Diagnostics.Medir("Clicar botão Importar (retry)", () => PromobImportador.ClicarBotaoImportar(janela));
                    InteractionHelper.EsperarUiRespirar(1500);
                    var wizardRetry = PromobWindowHelper.EncontrarJanelaWizard(automation, janela) ?? janela;
                    if (PromobImportador.VerificarBotaoBrowseNoWizard(wizardRetry)){
                        janelaWizard = wizardRetry;
                    } else {
                        Logger.Log("  [AVISO] Wizard incorreto no retry. Fechando e aguardando...", LogLevel.Warn);
                        PromobImportador.FecharWizardAtual(wizardRetry);
                        InteractionHelper.EsperarUiRespirar(1500);
                        WindowFinder.CachedHost = null;
                        continue;
                    }
                }

                try {
                    Logger.Log($"  [3/8] Preenchendo caminho do arquivo no wizard (tentativa {tentativaImp})...");
                    Diagnostics.Medir("Selecionar arquivo", () => PromobImportador.AbrirDialogoEPreencher(automation, janelaWizard, caminhoArquivo));

                    token.ThrowIfCancellationRequested();

                    Logger.Log($"  [4/8] Clicando em Avançar no Wizard (tentativa {tentativaImp})...");
                    InteractionHelper.AtivarJanela(janelaWizard);
                    Diagnostics.Medir("Avançar wizard", () => PromobImportador.ClicarAvancarWizard(automation, janelaWizard));

                    token.ThrowIfCancellationRequested();

                    // Após avançar, verifica se surgiram erros de rede (popup de erro antes da importação concluir)
                    InteractionHelper.EsperarUiRespirar(1000);
                    bool erroRedeDetectado = false;
                    for (int erroPop = 0; erroPop < 2; erroPop++) {
                        var desktop = automation.GetDesktop();
                        var popupErro = PromobWindowHelper.EncontrarPopupAtencao(desktop, PromobWindowHelper.CachedProcessIdPromob);
                        if (popupErro == null) break;

                        var textoErro = popupErro.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text))?.Properties.Name.ValueOrDefault ?? "";
                        Logger.Log($"  [AVISO] Popup de erro detectado após Avançar: '{textoErro}'. Clicando OK...", LogLevel.Warn);

                        var btnOkErro = popupErro.FindFirstDescendant(cf =>
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(
                                cf.ByName(PromobConfig.BtnOk).Or(cf.ByName(PromobConfig.BtnOkAlt)).Or(cf.ByName(PromobConfig.BtnConcluir))));

                        if (btnOkErro != null) InteractionHelper.ClicarComFallback(btnOkErro);
                        else {
                            InteractionHelper.AtivarJanela(popupErro);
                            Keyboard.Type(VirtualKeyShort.RETURN);
                        }
                        InteractionHelper.EsperarUiRespirar(1000);
                        erroRedeDetectado = true;
                    }

                    if (erroRedeDetectado) {
                        Logger.Log("  [AVISO] Erros de rede detectados. Cancelando o wizard e retentando importação...", LogLevel.Warn);

                        // Clica em "Cancelar" no wizard
                        var wizardAtual = PromobWindowHelper.EncontrarJanelaWizard(automation, janela) ?? janelaWizard;
                        var btnCancelar = wizardAtual.FindFirstDescendant(cf =>
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnCancelar)));

                        if (btnCancelar != null) {
                            Logger.Log("  [ACTION] Clicando em 'Cancelar' no wizard...");
                            InteractionHelper.ClicarComFallback(btnCancelar);
                        } else {
                            Logger.Log("  [AVISO] Botão 'Cancelar' não encontrado. Usando ESC...", LogLevel.Warn);
                            Keyboard.Type(VirtualKeyShort.ESCAPE);
                        }
                        InteractionHelper.EsperarUiRespirar(1000);

                        // Confirma o cancelamento clicando em "Sim"
                        var desktop2 = automation.GetDesktop();
                        var popupConfirm = PromobWindowHelper.EncontrarPopupAtencao(desktop2, PromobWindowHelper.CachedProcessIdPromob);
                        if (popupConfirm != null) {
                            var btnSim = popupConfirm.FindFirstDescendant(cf =>
                                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnSim)));
                            if (btnSim != null) {
                                Logger.Log("  [ACTION] Clicando em 'Sim' para confirmar o cancelamento...");
                                InteractionHelper.ClicarComFallback(btnSim);
                            } else {
                                Keyboard.Type(VirtualKeyShort.RETURN);
                            }
                            InteractionHelper.EsperarUiRespirar(1000);
                        }

                        // Navega Clientes → Projetos para desbloquear e retenta
                        NavegarClientesEProjetos(janela);
                        WindowFinder.CachedHost = null;
                        continue; // Próxima tentativa do loop de importação
                    }

                    Logger.Log($"  [5/8] Aguardando conclusão da importação (tentativa {tentativaImp})...");
                    Diagnostics.Medir("Aguardar importação", () => PromobImportador.AguardarImportacaoETratarPopups(automation, janelaWizard));
                    importacaoConcluida = true;
                    break; // Importação OK — sai do loop
                }
                catch (Exception exImp) when (!token.IsCancellationRequested) {
                    Logger.Log($"  [AVISO] Falha na tentativa {tentativaImp} de importação: {exImp.Message}. Retentando...", LogLevel.Warn);
                    WindowFinder.CachedHost = null;
                }
            }

            if (!importacaoConcluida)
                throw new Exception($"Falha na importação após {maxTentativasImportacao} tentativas consecutivas.");

            token.ThrowIfCancellationRequested();

            Logger.Log("  [6/9] Abrindo o projeto recém-importado (primeiro da lista)...");
            Diagnostics.Medir("Abrir projeto", () => PromobCarregadorProjeto.AbrirProjetoSelecionado(janela));

            token.ThrowIfCancellationRequested();

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

            // Sinaliza ao monitor de atualização que o projeto foi fechado (janela Update pode prosseguir)
            AutomacaoEstado.FechouProjetoAtual = true;
            Logger.Log("  [INFO] Sinal FechouProjetoAtual emitido para o monitor de atualização.");

            token.ThrowIfCancellationRequested();

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
            /// Clica na aba 'Clientes' e depois em 'Projetos' na janela principal do Promob
            /// para forçar a atualização de estado da interface e desbloquear o botão de Importar.
            /// </summary>
            /// <param name="janela">A janela principal do Promob.</param>
        //--------------------------------------------------------------------------------------
        private static void NavegarClientesEProjetos(Window janela){
            Logger.Log("  [ACTION] Navegando: clicando na aba 'Clientes'...");
            var abaClientes = janela.FindFirstDescendant(cf => cf.ByName("Clientes").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))))
                           ?? janela.FindFirstDescendant(cf => cf.ByName("Clientes"))
                           ?? janela.FindAllDescendants().FirstOrDefault(e => (e.Name ?? "").Equals("Clientes", StringComparison.OrdinalIgnoreCase));

            if (abaClientes != null) {
                InteractionHelper.ClicarComFallback(abaClientes);
                InteractionHelper.EsperarUiRespirar(1500);
            } else {
                Logger.Log("  [AVISO] Aba 'Clientes' não encontrada.", LogLevel.Warn);
            }

            Logger.Log("  [ACTION] Navegando: clicando na aba 'Projetos'...");
            var abaProjetos = janela.FindFirstDescendant(cf => cf.ByName("Projetos").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem).Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))))
                           ?? janela.FindFirstDescendant(cf => cf.ByName("Projetos"))
                           ?? janela.FindAllDescendants().FirstOrDefault(e => (e.Name ?? "").Equals("Projetos", StringComparison.OrdinalIgnoreCase));

            if (abaProjetos != null) {
                InteractionHelper.ClicarComFallback(abaProjetos);
                InteractionHelper.EsperarUiRespirar(1500);
            } else {
                Logger.Log("  [AVISO] Aba 'Projetos' não encontrada.", LogLevel.Warn);
            }
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
