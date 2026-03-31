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
    public static class PromobWorkflow{

        public static void ProcessarArquivo(UIA3Automation automation, string caminhoArquivo){
            Logger.Log("  [1/8] Localizando janela do Promob...");
            var janela = PromobWindowHelper.AguardarJanelaPromob(automation, PromobConfig.TimeoutLongo)
                ?? throw new Exception("Janela do Promob não encontrada. O Promob está aberto?");

            int currentPid = janela.Properties.ProcessId.ValueOrDefault;
            if (PromobWindowHelper.CachedProcessIdPromob.HasValue && PromobWindowHelper.CachedProcessIdPromob.Value != currentPid){
                Logger.Log("  [INFO] Novo ProcessId detectado. Invalidando cache de UI.");
                InvalidarCacheUi();
            }
            PromobWindowHelper.CachedProcessIdPromob = currentPid;

            InteractionHelper.AtivarJanela(janela);

            Logger.Log("  [2/8] Acionando Importar...");
            InteractionHelper.AtivarJanela(janela);
            Diagnostics.Medir("Clicar botão Importar", () => ClicarBotaoImportar(janela));

            Logger.Log("  [3/8] Abrindo busca de arquivo e preenchendo caminho...");
            var janelaWizard = PromobWindowHelper.EncontrarJanelaWizard(automation, janela) ?? janela;
            Diagnostics.Medir("Selecionar arquivo", () => AbrirDialogoEPreencher(automation, janelaWizard, caminhoArquivo));

            Logger.Log("  [4/8] Clicando em Avançar no Wizard...");
            InteractionHelper.AtivarJanela(janelaWizard);
            Diagnostics.Medir("Avançar wizard", () => ClicarAvancarWizard(automation, janelaWizard));

            Logger.Log("  [5/8] Tratando popup de Novo Projeto...");
            Diagnostics.Medir("Tratar popup", () => CancelarPopupNovoProjeto(automation));

            Logger.Log("  [6/8] Abrindo o projeto selecionado...");
            var nomeProjeto = Path.GetFileNameWithoutExtension(caminhoArquivo);
            Diagnostics.Medir("Abrir projeto", () => AbrirProjetoSelecionado(janela, nomeProjeto));

            Logger.Log("  [7/8] Navegando até Ferramentas > Orçamento > Listagem...");
            Diagnostics.Medir("Abrir listagem", () => AbrirListagem(automation, janela));

            Logger.Log("  [8/8] Fechando o projeto atual...");
            Diagnostics.Medir("Fechar projeto", () => FecharProjeto(automation, janela));

            Logger.Log("  [INFO] Fluxo concluído para este arquivo.");
        }

        private static void FecharProjeto(UIA3Automation automation, Window janela){
            var swTotal = Stopwatch.StartNew();
            Logger.Log("  [INFO] Iniciando sequência de fechamento do projeto...");
            InteractionHelper.AtivarJanela(janela);
            var raizBusca = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            var swAba = Stopwatch.StartNew();
            Logger.Log("    -> Procurando aba 'Arquivo' (FileTab)...");
            var abaArquivo = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                        .And(cf.ByAutomationId(PromobConfig.IdFileTab).Or(cf.ByName(PromobConfig.AbaArquivo))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem &&
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdFileTab, StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.AbaArquivo, StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );
            swAba.Stop();

            if (abaArquivo != null){
                Logger.Log($"    [OK] Aba 'Arquivo' localizada ({swAba.ElapsedMilliseconds}ms). Clicando...");
                InteractionHelper.SelecionarOuClicar(abaArquivo);
                InteractionHelper.EsperarUiRespirar(400);
            }
            else{
                Logger.Log($"    [AVISO] Aba 'Arquivo' não encontrada após {swAba.ElapsedMilliseconds}ms.", LogLevel.Warn);
            }

            var swBtn = Stopwatch.StartNew();
            Logger.Log("    -> Procurando botão 'Fechar' (ProjectClose)...");
            var btnFechar = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                        .And(cf.ByAutomationId(PromobConfig.IdProjectClose).Or(cf.ByName(PromobConfig.BtnFechar))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdProjectClose, StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.BtnFechar, StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );
            swBtn.Stop();

            if (btnFechar != null){
                Logger.Log($"    [OK] Botão 'Fechar' localizado ({swBtn.ElapsedMilliseconds}ms). Clicando...");
                InteractionHelper.AtivarJanela(janela); // Garante foco antes de clicar
                InteractionHelper.ClicarComFallback(btnFechar);
            }
            else{
                Logger.Log($"    [AVISO] Botão 'Fechar' não encontrado após {swBtn.ElapsedMilliseconds}ms. Tentando atalho Alt+F...", LogLevel.Warn);
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
                InteractionHelper.EsperarUiRespirar(800);
            }

            // Obrigatoriamente aguardar e tratar popup de salvar (até 10 segundos)
            Logger.Log("    [INFO] Aguardando possível popup 'Deseja salvar?' (até 10s)...");
            var swPopup = Stopwatch.StartNew();
            var popup = InteractionHelper.EsperarAteRetorno(
                () => PromobWindowHelper.EncontrarPopupAtencao(automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob),
                10000, intervaloMs: 500);
            swPopup.Stop();

            if (popup != null){
                Logger.Log($"    [OK] Popup '{popup.Name}' detectado ({swPopup.ElapsedMilliseconds}ms). Clicando em 'Não'...");
                InteractionHelper.AtivarJanela(popup.AsWindow());

                var btnNao = popup.FindFirstChild(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName(PromobConfig.BtnNao).Or(cf.ByName(PromobConfig.BtnNaoAlt)).Or(cf.ByName(PromobConfig.BtnNo))));

                if (btnNao == null){
                    btnNao = popup.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                          .And(cf.ByName(PromobConfig.BtnNao).Or(cf.ByName(PromobConfig.BtnNaoAlt)).Or(cf.ByName(PromobConfig.BtnNo))));
                }

                if (btnNao != null){
                    Logger.Log($"    [ACTION] Botão 'Não' encontrado. Clicando...");
                    InteractionHelper.ClicarComFallback(btnNao);
                }
                else{
                    Logger.Log($"    [AVISO] Botão 'Não' não encontrado via UIA. Usando atalho teclado 'N'...");
                    popup.AsWindow().SetForeground();
                    InteractionHelper.EsperarUiRespirar(200);
                    Keyboard.Type("n");
                }

                Logger.Log("    [INFO] Aguardando fechamento do popup...");
                bool fechou = InteractionHelper.EsperarAte(() => 
                    PromobWindowHelper.EncontrarPopupAtencao(automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob) == null, 3000, 200);

                if (!fechou){
                    var popupAinda = PromobWindowHelper.EncontrarPopupAtencao(automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob);
                    if (popupAinda != null){
                        Logger.Log("    [AVISO] Popup ainda aberto após primeira tentativa. Tentando atalho alternativo...");
                        InteractionHelper.AtivarJanela(popupAinda.AsWindow());
                        InteractionHelper.EsperarUiRespirar(300);
                        Keyboard.Type("n");
                        InteractionHelper.EsperarAte(() => 
                            PromobWindowHelper.EncontrarPopupAtencao(automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob) == null, 3000, 200);
                    }
                }
            }
            else{
                Logger.Log($"    [INFO] Nenhum popup de salvamento detectado em {swPopup.ElapsedMilliseconds}ms. Prosseguindo...");
            }

            swTotal.Stop();
            Logger.Log($"  [SUCESSO] Sequência de fechamento concluída em {swTotal.ElapsedMilliseconds}ms.");
        }

        private static void AbrirListagem(UIA3Automation automation, Window janela){
            InteractionHelper.AtivarJanela(janela);

            var raizBusca = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            Logger.Log("  [INFO] Procurando aba 'Ferramentas'...");
            var abaFerramentas = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                        .And(cf.ByAutomationId(PromobConfig.IdToolsTab).Or(cf.ByName(PromobConfig.AbaFerramentas))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem &&
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdToolsTab, StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.AbaFerramentas, StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (abaFerramentas != null){
                Logger.Log("  [OK] Aba 'Ferramentas' encontrada. Clicando...");
                InteractionHelper.SelecionarOuClicar(abaFerramentas);
                InteractionHelper.EsperarUiRespirar();
            }
            else{
                Logger.Log("  [AVISO] Aba 'Ferramentas' não encontrada.", LogLevel.Warn);
            }

            Logger.Log("  [INFO] Procurando botão de 'Orçamento'...");
            var btnOrcamento = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                        .And(cf.ByName(PromobConfig.SecaoOrcamento).Or(cf.ByName(PromobConfig.SecaoOrcamentoAlt)))
                        .And(cf.ByAutomationId(PromobConfig.IdOrcamentoToggle)),
                e => (e.Properties.AutomationId.ValueOrDefault ?? "") == PromobConfig.IdOrcamentoToggle &&
                     ((e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.SecaoOrcamento, StringComparison.OrdinalIgnoreCase) ||
                      (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.SecaoOrcamentoAlt, StringComparison.OrdinalIgnoreCase)),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            btnOrcamento ??= raizBusca.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuBar)
                  .And(cf.ByAutomationId(PromobConfig.IdOrcamentoMenu).Or(cf.ByAutomationId(PromobConfig.IdOrcamentoMenuAlt))));

            if (btnOrcamento != null){
                Logger.Log("  [OK] Botão 'Orçamento' encontrado. Clicando...");
                InteractionHelper.ClicarComFallback(btnOrcamento);
                InteractionHelper.EsperarUiRespirar();
            }
            else{
                Logger.Log("  [AVISO] Botão 'Orçamento' não encontrado.", LogLevel.Warn);
            }
        }

        private static void ClicarBotaoImportar(Window janelaPromob){
            int tentativas = 1;
            while (true){
                var swTotal = Stopwatch.StartNew();
                Logger.Log($"  [INFO] Procurando botão 'Importar' (Tentativa {tentativas})...");

                var swBusca = Stopwatch.StartNew();
                Logger.Log("    [SEARCH] Iniciando busca persistente do botão 'Importar Projeto'...");

                var buscaEm = WindowFinder.ObterHostOuJanela(janelaPromob, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

                AutomationElement? btnFound = WindowFinder.BuscarElementoComFallback(
                    buscaEm,
                    cf => cf.ByAutomationId(PromobConfig.IdImportarBotao),
                    e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals(PromobConfig.IdImportarBotao, StringComparison.OrdinalIgnoreCase) ||
                         (e.Properties.Name.ValueOrDefault ?? "").Equals(PromobConfig.NomeJanelaWizardImportacao, StringComparison.OrdinalIgnoreCase),
                    limitarAoMesmoProcesso: true,
                    processId: PromobWindowHelper.CachedProcessIdPromob
                );
                swBusca.Stop();

                if (btnFound != null) Logger.Log($"    [OK] Botão localizado em {swBusca.ElapsedMilliseconds}ms.");
                else Logger.Log($"    [AVISO] Botão não encontrado após {swBusca.ElapsedMilliseconds}ms.");

                if (btnFound != null){
                    InteractionHelper.AtivarJanela(janelaPromob);

                    Logger.Log("    [ACTION] Clicando no botão 'Importar'...");
                    InteractionHelper.ClicarComFallback(btnFound);

                    swTotal.Stop();
                    Logger.Log($"  [SUCESSO] Clique executado com sucesso (Tempo total: {swTotal.ElapsedMilliseconds}ms).");
                    break;
                }
                else{
                    swTotal.Stop();
                    Logger.Log($"  [AVISO] Tentativa {tentativas} falhou ({swTotal.ElapsedMilliseconds}ms). Aguardando 5s...", LogLevel.Warn);
                }

                tentativas++;
                Thread.Sleep(5000);
            }
        }

        private static void AbrirDialogoEPreencher(UIA3Automation automation, Window janelaPromob, string caminhoArquivo){
            InteractionHelper.AtivarJanela(janelaPromob);

            var btnBrowse = janelaPromob.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                  .And(cf.ByName(PromobConfig.BtnProcurar).Or(cf.ByName(PromobConfig.BtnProcurarTexto)).Or(cf.ByAutomationId(PromobConfig.IdBrowseButton))));

            if (btnBrowse == null){
                btnBrowse = WindowFinder.BuscarElementoComFallback(
                    janelaPromob,
                    cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                            .And(cf.ByName(PromobConfig.BtnProcurar)),
                    e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                         (((e.Properties.Name.ValueOrDefault ?? "").Contains(PromobConfig.BtnProcurar)) ||
                          ((e.Properties.AutomationId.ValueOrDefault ?? "").Contains("Browse", StringComparison.OrdinalIgnoreCase)))
                );
            }

            if (btnBrowse != null){
                Logger.Log($"  [OK] Botão de busca encontrado: {btnBrowse.Name}");
                InteractionHelper.ClicarComFallback(btnBrowse);
            }
            else{
                Logger.Log("  [AVISO] Botão de busca não encontrado. Usando TAB + SPACE...", LogLevel.Warn);
                InteractionHelper.AtivarJanela(janelaPromob);
                Keyboard.Press(VirtualKeyShort.TAB);
                InteractionHelper.EsperarUiRespirar();
                InteractionHelper.AtivarJanela(janelaPromob);
                Keyboard.Press(VirtualKeyShort.TAB);
                InteractionHelper.EsperarUiRespirar();
                InteractionHelper.AtivarJanela(janelaPromob);
                Keyboard.Press(VirtualKeyShort.SPACE);
            }

            var dialogo = InteractionHelper.EsperarAteRetorno(() => PromobWindowHelper.JanelaArquivoAberta(automation), PromobConfig.TimeoutLongo);
            if (dialogo == null)
                throw new Exception("Diálogo do Windows (Abrir/Salvar) não apareceu no tempo esperado.");

            PreencherDialogoNativo(automation, caminhoArquivo, dialogo);
        }

        private static void PreencherDialogoNativo(UIA3Automation automation, string caminhoCompleto, Window dialogo){
            Logger.Log($"  [OK] Diálogo encontrado: {dialogo.Name}");
            InteractionHelper.AtivarJanela(dialogo);

            // LOGICA ALTERADA: Usamos o caminho completo (Path) para garantir que o Windows encontre o arquivo, 
            // mesmo se o diálogo abrir na pasta errada.
            Logger.Log($"  [INFO] Preenchendo caminho completo via UIA: {caminhoCompleto}");

            bool preenchidoViaUia = false;
            AutomationElement? campoNome =
                dialogo.FindFirstDescendant(cf => cf.ByAutomationId(PromobConfig.IdCampoArquivoWin)) ??
                dialogo.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox)
                      .And(cf.ByAutomationId(PromobConfig.IdHostCampoArquivo))) ??
                dialogo.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)
                      .And(cf.ByAutomationId(PromobConfig.IdCampoArquivoWin)));

            if (campoNome == null){
                var combos = dialogo.FindAllDescendants(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.ComboBox));
                campoNome = combos.LastOrDefault();
            }

            if (campoNome != null){
                Logger.Log($"  [INFO] Campo 'Nome' encontrado (Id: {campoNome.AutomationId}, Tipo: {campoNome.ControlType}).");

                var editInterno = campoNome.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
                var alvo = editInterno ?? campoNome;
                if (editInterno != null) Logger.Log("  [INFO] Usando elemento 'Edit' interno do ComboBox para SetValue.");

                if (InteractionHelper.TentarDefinirValor(alvo, caminhoCompleto)){
                    preenchidoViaUia = true;
                    Logger.Log("  [OK] Valor definido via UIA (SetValue).");
                }
                else{
                    Logger.Log("  [INFO] SetValue falhou. Tentando foco + seleção + digitação...");
                    try{
                        InteractionHelper.AtivarJanela(dialogo);
                        campoNome.Focus();
                        InteractionHelper.EsperarUiRespirar(200);
                        InteractionHelper.AtivarJanela(dialogo);
                        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                        InteractionHelper.EsperarUiRespirar(100);
                        InteractionHelper.AtivarJanela(dialogo);
                        Keyboard.Type(caminhoCompleto);
                        
                        Logger.Log("  [INFO] Aguardando campo refletir a digitação...");
                        InteractionHelper.EsperarAte(() => {
                            try{
                                string? txt = campoNome.Patterns.Value.IsSupported ? campoNome.Patterns.Value.Pattern.Value.ValueOrDefault : campoNome.AsTextBox().Text;
                                return txt == caminhoCompleto;
                            } catch { return false; }
                        }, 3000, 100);

                        InteractionHelper.AtivarJanela(dialogo);
                        Keyboard.Type(VirtualKeyShort.RETURN); // Adiciona um RETURN extra para forçar atualização
                        InteractionHelper.EsperarUiRespirar(400); 
                        preenchidoViaUia = true;
                    }
                    catch (Exception ex){
                        Logger.Log($"  [AVISO] Fallback de teclado falhou: {ex.Message}", LogLevel.Warn);
                    }
                }
            }
            else{
                Logger.Log("  [AVISO] Campo 'Nome' não encontrado via UIA.", LogLevel.Warn);
            }

            if (!preenchidoViaUia){
                Logger.Log("  [AVISO] Usando clipboard como último recurso...", LogLevel.Warn);
                InteractionHelper.AtivarJanela(dialogo);

                // Preserva o estado atual do clipboard do usuário
                string? conteudoAnterior = NativeClipboard.ObterTexto();

                NativeClipboard.CopiarParaClipboardNativo(caminhoCompleto);
                InteractionHelper.EsperarUiRespirar(400);
                InteractionHelper.AtivarJanela(dialogo);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                
                Logger.Log("  [INFO] Aguardando o Ctrl+V surtir efeito...");
                InteractionHelper.EsperarAte(() => {
                    try{
                        var edit = dialogo.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
                        if (edit != null) return edit.AsTextBox().Text == caminhoCompleto;
                        return false;
                    } catch { return false; }
                }, 3000, 100);

                // Restaura o conteúdo original imediatamente após o uso
                if (conteudoAnterior != null){
                    NativeClipboard.CopiarParaClipboardNativo(conteudoAnterior);
                    Logger.Log("  [OK] Clipboard do usuário restaurado.");
                }

                InteractionHelper.AtivarJanela(dialogo);
                Keyboard.Type(VirtualKeyShort.RETURN);
                InteractionHelper.EsperarUiRespirar(500);
                return;
            }

            var btnAbrir =
                dialogo.FindFirstDescendant(cf => cf.ByAutomationId(PromobConfig.IdBtnAbrirWin).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))) ??
                dialogo.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName(PromobConfig.BtnAbrir).Or(cf.ByName(PromobConfig.BtnOpen))));

            bool fechou = false;
            for (int tentativa = 1; tentativa <= 3; tentativa++){
                if (btnAbrir != null) {
                    Logger.Log($"  [INFO] Tentativa {tentativa} de clicar no botão 'Abrir'...");
                    InteractionHelper.ClicarComFallback(btnAbrir); // Usa Invoke Pattern preferencialmente
                } else {
                    Logger.Log($"  [INFO] Tentativa {tentativa} de confirmar diálogo (ENTER)...");
                    InteractionHelper.AtivarJanela(dialogo);
                    Keyboard.Type(VirtualKeyShort.RETURN);
                }

                Logger.Log("  [INFO] Aguardando fechamento do diálogo...");
                var swAguardar = System.Diagnostics.Stopwatch.StartNew();
                bool popupInterceptado = false;
                
                while (swAguardar.ElapsedMilliseconds < 4000) {
                    try {
                        if (dialogo.Properties.IsOffscreen.ValueOrDefault) {
                            fechou = true;
                            break;
                        }

                        var desktop = automation.GetDesktop();
                        var popup = PromobWindowHelper.EncontrarPopupAtencao(desktop, PromobWindowHelper.CachedProcessIdPromob);
                        
                        if (popup != null && popup.Name != dialogo.Name) {
                            Logger.Log($"  [AVISO] Notificação do Promob roubou o foco do clique: '{popup.Name}'. Fechando...");
                            TratarPopupGenerico(popup);
                            InteractionHelper.AtivarJanela(dialogo);
                            popupInterceptado = true;
                            break; // Retorna ao loop principal para clicar em Abrir novamente
                        }
                    } catch { 
                        fechou = true; 
                        break; 
                    }
                    System.Threading.Thread.Sleep(200);
                }

                if (fechou) break;

                if (!popupInterceptado) {
                    Logger.Log($"  [AVISO] Diálogo não fechou após 4s. O clique pode ter sido ignorado.", LogLevel.Warn);
                }
            }

            if (fechou) {
                Logger.Log("  [OK] Diálogo de arquivo fechado e projeto selecionado com sucesso.");
            } else {
                throw new Exception("Falha Crítica: O Diálogo nativo de abrir arquivo não fechou, impedindo o carregamento ao Wizard.");
            }

            InteractionHelper.EsperarUiRespirar(500);
        }

        private static bool ValidarCamposWizard(Window janelaWizard){
            Logger.Log("  [INFO] Validando preenchimento dos campos obrigatórios no Wizard...");
            
            // Pequena espera para dar tempo da UI atualizar após o fechamento do diálogo
            InteractionHelper.EsperarUiRespirar(800);

            var campoCaminho = janelaWizard.FindFirstDescendant(cf => 
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)
                  .And(cf.ByName(PromobConfig.NameCampoCaminho).Or(cf.ByAutomationId(PromobConfig.NameCampoCaminho))));

            if (campoCaminho == null){
                // Tenta buscar por proximidade ou nome parcial se falhar
                var labels = janelaWizard.FindAllDescendants(cf => cf.ByName(PromobConfig.NameCampoCaminho));
                if (labels.Any()){
                    campoCaminho = janelaWizard.FindFirstDescendant(cf => 
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit)); // Pega o primeiro Edit que encontrar se houver label
                }
            }

            if (campoCaminho != null){
                var valor = campoCaminho.AsTextBox().Text;
                if (string.IsNullOrWhiteSpace(valor)){
                    Logger.Log("  [ERRO] O campo 'Caminho' está vazio no Wizard.", LogLevel.Error);
                    return false;
                }
                Logger.Log($"  [OK] Campo 'Caminho' preenchido: {valor}");
                return true;
            }

            Logger.Log("  [AVISO] Não foi possível encontrar o campo 'Caminho' para validação. Prosseguindo no escuro...", LogLevel.Warn);
            return true; // Retorna true para não travar se não achar o elemento, mas loga o aviso
        }

        private static void ClicarAvancarWizard(UIA3Automation automation, Window janelaWizard){
            InteractionHelper.AtivarJanela(janelaWizard);

            for (int tentativa = 1; tentativa <= 3; tentativa++){
                // Validação antes de avançar
                if (tentativa == 1 && !ValidarCamposWizard(janelaWizard)){
                    Logger.Log("  [AVISO] Campos obrigatórios parecem estar vazios. Tentando avançar mesmo assim para ver o erro do Promob...");
                }

                Logger.Log($"  [INFO] Procurando botão 'Avançar' (Tentativa {tentativa}/3)...");
                var btnAvancar = janelaWizard.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName(PromobConfig.BtnAvancar).Or(cf.ByName(PromobConfig.BtnAvancarAlt)).Or(cf.ByName(PromobConfig.BtnNext))));

                if (btnAvancar != null){
                    if (!btnAvancar.IsEnabled){
                        Logger.Log("  [ERRO] O botão 'Avançar' está desabilitado. Provavelmente faltam campos obrigatórios.", LogLevel.Error);
                    }
                    InteractionHelper.ClicarComFallback(btnAvancar);
                    Logger.Log("  [OK] Botão 'Avançar' clicado.");
                }
                else{
                    Logger.Log("  [AVISO] Botão 'Avançar' não encontrado. Tentando ENTER...", LogLevel.Warn);
                    InteractionHelper.AtivarJanela(janelaWizard);
                    Keyboard.Type(VirtualKeyShort.RETURN);
                }

                Logger.Log("  [INFO] Analisando comportamento do Wizard após o clique...");
                bool precisouTentarDenovo = false;

                // Pequena pausa para garantir que o Promob processe a validação do formulário
                InteractionHelper.EsperarUiRespirar(1500);

                var desktop = automation.GetDesktop();
                var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var popup in janelas){
                    // Filtro crítico: ignora a própria janela principal do Promob para não confundir com popup
                    if (popup.Properties.ProcessId != PromobWindowHelper.CachedProcessIdPromob) continue;
                    if (popup.Name == WindowFinder.CachedHost?.Name) continue; // Ignora o host principal
                    
                    var name = popup.Name ?? "";

                    // Tratamento para popup de Atenção ("Deseja cancelar a operação?")
                    if (InteractionHelper.ContemQualquer(name, PromobConfig.TitulosAviso)){
                        // Verifica se o texto do popup contém "cancelar"
                        var textElement = popup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                        var texto = textElement?.Properties.Name.ValueOrDefault ?? "";
                        
                        if (texto.Contains(PromobConfig.MsgConfirmarCancelamento, StringComparison.OrdinalIgnoreCase)){
                            Logger.Log($"  [AVISO] Popup de cancelamento interceptado: '{texto}'. Clicando em '{PromobConfig.BtnNao}'...");
                            
                            var btnNao = popup.FindFirstDescendant(cf => 
                                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnNao).Or(cf.ByName(PromobConfig.BtnNaoAlt))));
                            
                            if (btnNao != null) InteractionHelper.ClicarComFallback(btnNao);
                            else {
                                InteractionHelper.AtivarJanela(popup.AsWindow());
                                Keyboard.Type("n");
                            }
                        }
                        else {
                            Logger.Log($"  [INFO] Popup de Atenção detectado ('{texto}'). Tratando como informativo (OK/Nao).");
                            TratarPopupGenerico(popup.AsWindow());
                        }
                        precisouTentarDenovo = true;
                        InteractionHelper.EsperarUiRespirar(800);
                    }

                    // Tratamento para popup de Aviso ("Não é possível salvar enquanto há erros...")
                    if (InteractionHelper.ContemQualquer(name, PromobConfig.TitulosAviso)){
                        var textElement = popup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                        var texto = textElement?.Properties.Name.ValueOrDefault ?? "";
                        Logger.Log($"  [ERRO] O Promob exibiu um erro/aviso: '{texto}'", LogLevel.Error);

                        var btnOk = popup.FindFirstDescendant(cf => 
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnOk).Or(cf.ByName(PromobConfig.BtnOkAlt)).Or(cf.ByName(PromobConfig.BtnConcluir))));
                        
                        if (btnOk != null) InteractionHelper.ClicarComFallback(btnOk);
                        else {
                            InteractionHelper.AtivarJanela(popup.AsWindow());
                            Keyboard.Type(VirtualKeyShort.RETURN);
                        }
                        
                        precisouTentarDenovo = true;
                        InteractionHelper.EsperarUiRespirar(800);
                    }
                }

                if (precisouTentarDenovo){
                    Logger.Log("  [INFO] Voltando ao loop para tentar resolver campos ou avançar novamente.");
                    continue; 
                }

                break; // Sai do loop se não houve popups interceptados
            }
        }

        private static void CancelarPopupNovoProjeto(UIA3Automation automation){
            Logger.Log("  [INFO] Aguardando popup 'Atenção'...");

            var popup = InteractionHelper.EsperarAteRetorno(() => PromobWindowHelper.EncontrarPopupAtencao(automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob), 5000);
            if (popup == null){
                Logger.Log("  [INFO] Popup de novo projeto não apareceu.");
                return;
            }

            Logger.Log($"  [OK] Popup encontrado: {popup.Name}");
            InteractionHelper.AtivarJanela(popup);

            var btnCancelar = popup.FindFirstDescendant(cf =>
                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                  .And(cf.ByName(PromobConfig.BtnCancelar).Or(cf.ByName(PromobConfig.BtnNao)).Or(cf.ByName(PromobConfig.BtnNaoAlt)).Or(cf.ByName(PromobConfig.BtnNo))));

            if (btnCancelar != null){
                InteractionHelper.AtivarJanela(popup);
                InteractionHelper.ClicarComFallback(btnCancelar);
                Logger.Log("  [OK] Botão de cancelamento clicado no popup.");
            }
            else{
                Logger.Log("  [AVISO] Botão de cancelamento não encontrado. Usando ESC...", LogLevel.Warn);
                InteractionHelper.AtivarJanela(popup);
                Keyboard.Type(VirtualKeyShort.ESCAPE);
            }
        }

        private static void AbrirProjetoSelecionado(Window janelaPromob, string nomeProjeto){
            InteractionHelper.AtivarJanela(janelaPromob);
            Logger.Log($"  [INFO] Procurando projeto '{nomeProjeto}' para abrir...");

            var itemProjeto = WindowFinder.BuscarElementoComFallback(
                janelaPromob,
                cf => cf.ByName(nomeProjeto).Or(cf.ByName(nomeProjeto.ToUpperInvariant())),
                e => string.Equals(e.Properties.Name.ValueOrDefault, nomeProjeto, StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (itemProjeto != null){
                Logger.Log("  [OK] Projeto encontrado na lista. Executando duplo clique...");
                itemProjeto.DoubleClick();
            }
            else{
                Logger.Log("  [AVISO] Elemento do projeto não encontrado via nome. Tentando localizar o primeiro item da lista...", LogLevel.Warn);

                var qualquerItem = WindowFinder.BuscarElementoComFallback(
                    janelaPromob,
                    cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem).Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.DataItem)),
                    e => e.ControlType == FlaUI.Core.Definitions.ControlType.ListItem || e.ControlType == FlaUI.Core.Definitions.ControlType.DataItem,
                    limitarAoMesmoProcesso: true,
                    processId: PromobWindowHelper.CachedProcessIdPromob
                );

                if (qualquerItem != null){
                    Logger.Log($"  [OK] Item genérico encontrado ('{qualquerItem.Name}'). Executando duplo clique...");
                    qualquerItem.DoubleClick();
                    InteractionHelper.EsperarUiRespirar(800);
                    qualquerItem.DoubleClick();
                    InteractionHelper.EsperarUiRespirar(800);
                }
                else{
                    Logger.Log("  [AVISO] Nenhum item de lista encontrado. Tentando botão 'Abrir projeto'...", LogLevel.Warn);

                    var btnAbrir = janelaPromob.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                          .And(cf.ByName(PromobConfig.BtnAbrirProjeto).Or(cf.ByName(PromobConfig.BtnAbrir)).Or(cf.ByName("Acessar")).Or(cf.ByName("Editar"))));

                    if (btnAbrir != null){
                        Logger.Log("  [OK] Botão de abrir encontrado. Clicando...");
                        InteractionHelper.ClicarComFallback(btnAbrir);
                    }
                    else{
                        Logger.Log("  [AVISO] Nenhuma forma de abrir encontrada. Tentando ENTER...", LogLevel.Warn);
                        InteractionHelper.AtivarJanela(janelaPromob);
                        Keyboard.Type(VirtualKeyShort.RETURN);
                    }
                }
            }

            int timeoutAtual = 10000;
            int tentativaLoop = 1;

            while (true){
                Logger.Log($"  [INFO] Aguardando o carregamento do projeto (Tentativa {tentativaLoop}, timeout: {timeoutAtual / 1000}s)...");

                bool carregou = InteractionHelper.EsperarAte(() =>{
                    var swTotal = Stopwatch.StartNew();
                    Logger.Log("    [DEBUG] Iniciando ciclo de verificação UI...");

                    var raizBusca = WindowFinder.ObterHostOuJanela(janelaPromob, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

                    var swAba = Stopwatch.StartNew();
                    Logger.Log("      -> Procurando aba 'Ferramentas' (TabItem)...");
                    var aba = raizBusca.FindFirstDescendant(cf =>
                        cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem)
                          .And(cf.ByAutomationId(PromobConfig.IdToolsTab).Or(cf.ByName(PromobConfig.AbaFerramentas))));
                    swAba.Stop();

                    if (aba != null) Logger.Log($"      [OK] Aba encontrada em {swAba.ElapsedMilliseconds}ms.");
                    else Logger.Log($"      [AGUARDE] Aba não visível após {swAba.ElapsedMilliseconds}ms.");

                    var swMsg = Stopwatch.StartNew();
                    Logger.Log("      -> Verificando mensagem de carregamento (Text/Label)...");
                    var msgCarregando = raizBusca.FindFirstDescendant(cf =>
                        cf.ByName(PromobConfig.MsgCarregandoItens));
                    swMsg.Stop();

                    if (msgCarregando != null) Logger.Log($"      [LOADING] Módulos carregando ({swMsg.ElapsedMilliseconds}ms).");
                    else Logger.Log($"      [READY] Sem mensagem de carregamento ({swMsg.ElapsedMilliseconds}ms).");

                    bool pronto = (aba != null) && (msgCarregando == null);

                    if (!pronto){
                        var swPopup = Stopwatch.StartNew();
                        Logger.Log("    [INFO] Procurando popups de bloqueio...");
                        var desktop = janelaPromob.Automation.GetDesktop();
                        var popup = PromobWindowHelper.EncontrarPopupAtencao(desktop, PromobWindowHelper.CachedProcessIdPromob);
                        swPopup.Stop();

                        if (popup != null){
                            Logger.Log($"    [AVISO] Popup '{popup.Name}' tratado ({swPopup.ElapsedMilliseconds}ms).");
                            TratarPopupGenerico(popup);
                        }
                        else{
                            Logger.Log($"    [INFO] Sem popups detectados em {swPopup.ElapsedMilliseconds}ms.");
                        }
                    }
                    else{
                        Logger.Log("    [SUCESSO] Condições de carregamento concluídas.");
                        InteractionHelper.SelecionarOuClicar(aba!);
                    }

                    swTotal.Stop();
                    Logger.Log($"    [DEBUG] Ciclo finalizado em {swTotal.ElapsedMilliseconds}ms total.");
                    return pronto;
                }, timeoutMs: timeoutAtual, intervaloMs: 2500);

                if (carregou){
                    Logger.Log("  [OK] Projeto carregado e validado com sucesso.");
                    InteractionHelper.EsperarUiRespirar(1000);
                    break;
                }

                Logger.Log($"  [AVISO] Timeout de {timeoutAtual / 1000}s atingido sem concluir o carregamento por UIA.", LogLevel.Warn);

                if (VisionHelper.Habilitado){
                    Logger.Log("  [VISION] Iniciando verificação visual (IA) como fallback final para este ciclo...");
                    var visao = VisionHelper.AguardarEstadoTela(
                        "A aba 'Ferramentas' está visível e não há mensagens de 'Carregando' ou 'Módulos Invisíveis' na parte inferior da tela.",
                        maxTentativas: 1, fallbackMs: 500);

                    if (visao){
                        Logger.Log("  [VISION] IA detectou que a tela parece estar pronta (carregada). Prosseguindo.");
                        break;
                    }
                    else{
                        Logger.Log("  [VISION] IA confirmou que o projeto ainda parece estar carregando ou em estado inconsistente.");
                    }
                }

                tentativaLoop++;
                timeoutAtual = 10000;
                Logger.Log("  [INFO] Reiniciando verificação para novo ciclo de 10s...");
            }
        }

        private static void TratarPopupGenerico(Window popup){
            Logger.Log($"  [INFO] Tratando popup: '{popup.Name}'");
            InteractionHelper.AtivarJanela(popup);

            if (InteractionHelper.ContemQualquer(popup.Name, PromobConfig.TitulosAviso)){
                var btnOk = popup.FindFirstDescendant(cf => 
                    cf.ByName(PromobConfig.BtnOk)
                      .Or(cf.ByName(PromobConfig.BtnOkAlt))
                      .Or(cf.ByName(PromobConfig.BtnConcluir))
                      .Or(cf.ByName(PromobConfig.BtnSim))); // Adicionado Sim

                if (btnOk != null){
                    Logger.Log($"  [OK] Clicando em '{btnOk.Name}' no popup.");
                    InteractionHelper.AtivarJanela(popup);
                    InteractionHelper.ClicarComFallback(btnOk);
                }
                else{
                    Logger.Log("  [OK] Enviando ALT+F4 para fechar o popup.");
                    InteractionHelper.AtivarJanela(popup);
                    Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.F4);
                }
                InteractionHelper.EsperarUiRespirar(500);
            }
        }

        public static void TentarRecuperar(UIA3Automation automation){
            Logger.Log("  [INFO] Executando rotina de recuperação...");

            try{
                InvalidarCacheUi();

                var janelaBase = PromobWindowHelper.AguardarJanelaPromob(automation, 1000);
                if (janelaBase != null) InteractionHelper.AtivarJanela(janelaBase);
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                InteractionHelper.EsperarUiRespirar(250);

                if (janelaBase != null) InteractionHelper.AtivarJanela(janelaBase);
                Keyboard.Press(VirtualKeyShort.ESCAPE);
                InteractionHelper.EsperarUiRespirar(250);

                var desktop = automation.GetDesktop();
                var janelas = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                foreach (var j in janelas){
                    var titulo = j.Name ?? "";
                    if (InteractionHelper.ContemQualquer(titulo, PromobConfig.TitulosAviso)){
                        try{
                            var popup = j.AsWindow();
                            popup.SetForeground();
                            
                            // Verifica se é o popup de "Deseja cancelar a operação?"
                            var textElement = popup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                            var texto = textElement?.Properties.Name.ValueOrDefault ?? "";

                            if (texto.Contains(PromobConfig.MsgConfirmarCancelamento, StringComparison.OrdinalIgnoreCase)){
                                Logger.Log($"    [RECOVERY] Popup de cancelamento detectado. Clicando em '{PromobConfig.BtnNao}' para manter a aplicação aberta.");
                                var btnNao = popup.FindFirstDescendant(cf => 
                                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnNao).Or(cf.ByName(PromobConfig.BtnNaoAlt))));
                                
                                if (btnNao != null) InteractionHelper.ClicarComFallback(btnNao);
                                else {
                                    InteractionHelper.AtivarJanela(popup);
                                    Keyboard.Type("n");
                                }
                            }
                            else {
                                InteractionHelper.AtivarJanela(popup);
                                Keyboard.Press(VirtualKeyShort.ESCAPE);
                            }
                            InteractionHelper.EsperarUiRespirar(200);
                        }
                        catch{
                            // ignora
                        }
                    }
                }

                var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 2500);
                if (janela != null){
                    FecharProjetoEIgnorarSalvar(janela);
                }
            }
            catch (Exception ex){
                Logger.Log($"  [AVISO] Recuperação falhou: {ex.Message}", LogLevel.Warn);
            }

            InteractionHelper.EsperarUiRespirar(1000);
        }

        private static void FecharProjetoEIgnorarSalvar(Window janelaPromob){
            Logger.Log("  [RECOVERY] Tentando fechar projeto atual de forma segura...");
            InteractionHelper.AtivarJanela(janelaPromob);

            var raizBusca = WindowFinder.ObterHostOuJanela(janelaPromob, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);
            var btnFechar = WindowFinder.BuscarElementoComFallback(
                raizBusca,
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByAutomationId(PromobConfig.IdProjectClose).Or(cf.ByName(PromobConfig.BtnFechar))),
                e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button && ((e.Properties.AutomationId.ValueOrDefault ?? "") == PromobConfig.IdProjectClose || (e.Properties.Name.ValueOrDefault ?? "") == PromobConfig.BtnFechar),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (btnFechar != null) {
                Logger.Log("    [RECOVERY] Fechando projeto via UIA Pattern (Background)...");
                InteractionHelper.ClicarComFallback(btnFechar);
            } else {
                Logger.Log("    [RECOVERY] Fallback de teclado para fechar projeto...");
                InteractionHelper.AtivarJanela(janelaPromob);
                Keyboard.TypeSimultaneously(VirtualKeyShort.ALT, VirtualKeyShort.KEY_A);
                InteractionHelper.EsperarUiRespirar(300);

                InteractionHelper.AtivarJanela(janelaPromob);
                Keyboard.Type("f");
            }

            Logger.Log("    [RECOVERY] Tratando popup de salvamento...");
            bool fechou = InteractionHelper.EsperarAte(() => {
                var popup = PromobWindowHelper.EncontrarPopupAtencao(janelaPromob.Automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob);
                if (popup != null) {
                    var btnNao = popup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnNao).Or(cf.ByName(PromobConfig.BtnNaoAlt))));
                    if (btnNao != null) InteractionHelper.ClicarComFallback(btnNao);
                    else {
                        InteractionHelper.AtivarJanela(popup.AsWindow());
                        Keyboard.Type("n");
                    }
                    return true;
                }
                return false;
            }, 3000, 200);

            if (!fechou) {
                 InteractionHelper.AtivarJanela(janelaPromob);
                 Keyboard.Type("n");
            }
        }

        private static void InvalidarCacheUi(){
            WindowFinder.CachedHost = null;
        }
    }
}
