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
        /// Componente responsável por gerenciar a etapa de importação de arquivos no Promob,
        /// cobrindo o acionamento do botão Importar, o preenchimento do FileDialog e o avanço no Wizard.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobImportador{

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Realiza a busca ativa e clica no botão "Importar Projeto" na tela de início do Promob.
            /// </summary>
            /// <param name="janelaPromob">A janela principal ativa do Promob.</param>
        //--------------------------------------------------------------------------------------
        public static void ClicarBotaoImportar(Window janelaPromob){
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

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Abre o diálogo nativo do Windows para seleção de arquivos clicando no botão "Procurar" no Wizard
            /// e preenche com o caminho absoluto do arquivo a ser importado.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="janelaPromob">A janela de Wizard ou janela principal ativa do Promob.</param>
            /// <param name="caminhoArquivo">O caminho absoluto do arquivo C# do projeto a ser selecionado.</param>
        //--------------------------------------------------------------------------------------
        public static void AbrirDialogoEPreencher(UIA3Automation automation, Window janelaPromob, string caminhoArquivo){
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

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Insere o caminho do arquivo no campo de texto de seleção de arquivo do Windows FileDialog
            /// e confirma a seleção de forma robusta e resiliente (tentando SetValue, Clipboard e Teclado).
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="caminhoCompleto">O caminho completo do arquivo a ser importado.</param>
            /// <param name="dialogo">A janela do FileDialog nativo do Windows aberta.</param>
        //--------------------------------------------------------------------------------------
        public static void PreencherDialogoNativo(UIA3Automation automation, string caminhoCompleto, Window dialogo){
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
                            PromobCarregadorProjeto.TratarPopupGenerico(popup);
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

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Verifica e valida se o campo obrigatório de "Caminho do arquivo" foi devidamente 
            /// preenchido pelo robô no assistente de importação antes de disparar o clique de avançar.
            /// </summary>
            /// <param name="janelaWizard">A janela ativa do assistente (Wizard) de Importação.</param>
            /// <returns><c>true</c> se a validação passar ou se o campo não puder ser verificado; caso contrário, <c>false</c>.</returns>
        //--------------------------------------------------------------------------------------
        public static bool ValidarCamposWizard(Window janelaWizard){
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

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Localiza e clica no botão "Avançar" do Wizard de importação, gerenciando ativamente possíveis
            /// diálogos de cancelamento, avisos de validação de formulário ou erros emitidos pelo Promob.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
            /// <param name="janelaWizard">A janela do assistente (Wizard) ativa.</param>
        //--------------------------------------------------------------------------------------
        public static void ClicarAvancarWizard(UIA3Automation automation, Window janelaWizard){
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
                            PromobCarregadorProjeto.TratarPopupGenerico(popup.AsWindow());
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

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Intercepta e cancela com segurança possíveis popups de confirmação de "Novo Projeto" ou
            /// salvamento indesejados abertos após avançar etapas do wizard.
            /// </summary>
            /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void CancelarPopupNovoProjeto(UIA3Automation automation){
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
    }
}
