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
                try{
                    var swTotal = Stopwatch.StartNew();
                    AppLogs.LogImportadorProcurandoBotao(tentativas);

                    var swBusca = Stopwatch.StartNew();
                    AppLogs.LogImportadorIniciandoBusca();

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

                    if (btnFound != null) AppLogs.LogImportadorBotaoLocalizado(swBusca.ElapsedMilliseconds);
                    else AppLogs.LogImportadorBotaoNaoEncontrado(swBusca.ElapsedMilliseconds);

                    if (btnFound != null){
                        InteractionHelper.AtivarJanela(janelaPromob);

                        AppLogs.LogImportadorAguardandoEstabilizacao();
                        Thread.Sleep(2000);

                        AppLogs.LogImportadorClicandoBotao();
                        InteractionHelper.ClicarComFallback(btnFound);

                        swTotal.Stop();
                        AppLogs.LogImportadorCliqueSucesso(swTotal.ElapsedMilliseconds);
                        break;
                    }
                    else{
                        swTotal.Stop();
                        AppLogs.LogImportadorTentativaFalhou(tentativas, swTotal.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex){
                    AppLogs.LogImportadorErroBusca(tentativas, ex.Message);
                    WindowFinder.CachedHost = null;
                }

                tentativas++;
                Thread.Sleep(5000);
            }
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Verifica se the janela do Wizard de importação que está aberta é a correta,
        /// ou seja, se contém o botão "..." (Browse/Caminho) que permite selecionar o arquivo .promob.
        /// O Promob pode abrir um wizard errado (importação por sistema cloud/ERP) sem esse botão.
        /// </summary>
        /// <param name="janelaWizard">A janela de wizard aberta a ser inspecionada.</param>
        /// <returns>true se o botão "..." foi encontrado (wizard correto); false caso contrário (wizard errado).</returns>
        //--------------------------------------------------------------------------------------
        public static bool VerificarBotaoBrowseNoWizard(Window janelaWizard){
            try{
                var swVerificacao = Stopwatch.StartNew();
                AppLogs.LogImportadorAnalisandoEstruturaWizard();

                // Obtém os elementos dos primeiros 4 níveis do wizard (que cobrem todos os botões e campos estruturais WPF do Promob)
                var elementos = WindowFinder.BuscarAteNivel(janelaWizard, maxNivel: 4).ToList();

                // Busca o botão "..." ou Browse
                var btnBrowse = elementos.FirstOrDefault(e =>
                    e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                    ((e.Properties.Name.ValueOrDefault ?? "") == PromobConfig.BtnProcurar ||
                     (e.Properties.Name.ValueOrDefault ?? "") == PromobConfig.BtnProcurarTexto ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "") == PromobConfig.IdBrowseButton ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Contains("Browse", StringComparison.OrdinalIgnoreCase)));

                swVerificacao.Stop();

                if (btnBrowse != null){
                    AppLogs.LogImportadorWizardCorreto(swVerificacao.ElapsedMilliseconds, btnBrowse.Name, btnBrowse.Properties.AutomationId.ValueOrDefault);
                    return true;
                }

                AppLogs.LogImportadorWizardIncorreto(swVerificacao.ElapsedMilliseconds);
                return false;
            }
            catch (Exception ex){
                AppLogs.LogImportadorErroVerificacaoWizard(ex.Message);
                return false;
            }
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Fecha o wizard de importação que está aberto (quando é o wizard errado/incorreto),
        /// clicando em 'Cancelar' ou no botão 'X' da janela, para retornar à tela inicial do Promob.
        /// </summary>
        /// <param name="janelaWizard">A janela do wizard a ser fechada.</param>
        //--------------------------------------------------------------------------------------
        public static void FecharWizardAtual(Window janelaWizard){
            AppLogs.LogImportadorFechandoWizardIncorreto();
            try{
                InteractionHelper.AtivarJanela(janelaWizard);

                // Busca superficial pelo botão Cancelar
                var elementos = WindowFinder.BuscarAteNivel(janelaWizard, maxNivel: 4);
                var btnCancelar = elementos.FirstOrDefault(e =>
                    e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                    ((e.Properties.Name.ValueOrDefault ?? "") == PromobConfig.BtnCancelar ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Contains("Cancel", StringComparison.OrdinalIgnoreCase)));

                if (btnCancelar != null){
                    AppLogs.LogImportadorClicandoCancelarWizard();
                    InteractionHelper.ClicarComFallback(btnCancelar);
                    InteractionHelper.EsperarUiRespirar(800);
                    AppLogs.LogImportadorWizardFechadoCancelar();
                    return;
                }

                // Fallback: pressiona ESC para fechar o wizard
                AppLogs.LogImportadorBotaoCancelarNaoEncontrado();
                Keyboard.Type(VirtualKeyShort.ESCAPE);
                InteractionHelper.EsperarUiRespirar(800);
                AppLogs.LogImportadorWizardFechadoEsc();
            }
            catch (Exception ex){
                AppLogs.LogImportadorErroFecharWizard(ex.Message);
                try{
                    Keyboard.Type(VirtualKeyShort.ESCAPE);
                    InteractionHelper.EsperarUiRespirar(800);
                }
                catch { }
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
                AppLogs.LogImportadorBotaoBuscaEncontrado(btnBrowse.Name);
                InteractionHelper.ClicarComFallback(btnBrowse);
            }
            else{
                AppLogs.LogImportadorBotaoBuscaNaoEncontrado();
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
            AppLogs.LogImportadorDialogoEncontrado(dialogo.Name);
            InteractionHelper.AtivarJanela(dialogo);

            // LOGICA ALTERADA: Usamos o caminho completo (Path) para garantir que o Windows encontre o arquivo, 
            // mesmo se o diálogo abrir na pasta errada.
            AppLogs.LogImportadorPreenchendoCaminhoUia(caminhoCompleto);

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
                AppLogs.LogImportadorCampoNomeEncontrado(campoNome.AutomationId, campoNome.ControlType);

                var editInterno = campoNome.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit));
                var alvo = editInterno ?? campoNome;
                if (editInterno != null) AppLogs.LogImportadorUsandoEditInterno();

                if (InteractionHelper.TentarDefinirValor(alvo, caminhoCompleto)){
                    preenchidoViaUia = true;
                    AppLogs.LogImportadorValorDefinidoUia();
                }
                else{
                    AppLogs.LogImportadorSetValueFalhou();
                    try{
                        InteractionHelper.AtivarJanela(dialogo);
                        campoNome.Focus();
                        InteractionHelper.EsperarUiRespirar(200);
                        InteractionHelper.AtivarJanela(dialogo);
                        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                        InteractionHelper.EsperarUiRespirar(100);
                        InteractionHelper.AtivarJanela(dialogo);
                        Keyboard.Type(caminhoCompleto);
                        
                        AppLogs.LogImportadorAguardandoDigitacao();
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
                        AppLogs.LogImportadorFallbackTecladoFalhou(ex.Message);
                    }
                }
            }
            else{
                AppLogs.LogImportadorCampoNomeNaoEncontrado();
            }

            if (!preenchidoViaUia){
                AppLogs.LogImportadorUsandoClipboardFallback();
                InteractionHelper.AtivarJanela(dialogo);

                // Preserva o estado atual do clipboard do usuário
                string? conteudoAnterior = NativeClipboard.ObterTexto();

                NativeClipboard.CopiarParaClipboardNativo(caminhoCompleto);
                InteractionHelper.EsperarUiRespirar(400);
                InteractionHelper.AtivarJanela(dialogo);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
                
                AppLogs.LogImportadorAguardandoCtrlV();
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
                    AppLogs.LogImportadorClipboardRestaurado();
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
                    AppLogs.LogImportadorTentativaCliqueAbrir(tentativa);
                    InteractionHelper.ClicarComFallback(btnAbrir); // Usa Invoke Pattern preferencialmente
                } else {
                    AppLogs.LogImportadorTentativaConfirmarEnter(tentativa);
                    InteractionHelper.AtivarJanela(dialogo);
                    Keyboard.Type(VirtualKeyShort.RETURN);
                }

                AppLogs.LogImportadorAguardandoFechamentoDialogo();
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
                            AppLogs.LogImportadorNotificacaoRoubouFoco(popup.Name);
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
                    AppLogs.LogImportadorDialogoNaoFechouAviso();
                }
            }

            if (fechou) {
                AppLogs.LogImportadorDialogoFechadoSucesso();
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
            AppLogs.LogImportadorValidandoCamposWizard();
            
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
                    return false;
                }
                AppLogs.LogImportadorCampoCaminhoPreenchido(valor);
                return true;
            }

            AppLogs.LogImportadorCampoCaminhoNaoEncontradoVerificacao();
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
                    AppLogs.LogImportadorCamposVaziosAvançarAviso();
                }

                AppLogs.LogImportadorProcurandoAvancar(tentativa);
                var btnAvancar = janelaWizard.FindFirstDescendant(cf =>
                    cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)
                      .And(cf.ByName(PromobConfig.BtnAvancar).Or(cf.ByName(PromobConfig.BtnAvancarAlt)).Or(cf.ByName(PromobConfig.BtnNext))));

                if (btnAvancar != null){
                    if (!btnAvancar.IsEnabled){
                        AppLogs.LogImportadorBotaoAvancarDesabilitado();
                    }
                    InteractionHelper.ClicarComFallback(btnAvancar);
                    AppLogs.LogImportadorBotaoAvancarClicado();
                }
                else{
                    AppLogs.LogImportadorBotaoAvancarNaoEncontrado();
                    InteractionHelper.AtivarJanela(janelaWizard);
                    Keyboard.Type(VirtualKeyShort.RETURN);
                }

                AppLogs.LogImportadorAnalisandoWizardAposClique();
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
                            AppLogs.LogImportadorPopupCancelamentoInterceptado(texto, PromobConfig.BtnNao);
                            
                            var btnNao = popup.FindFirstDescendant(cf => 
                                cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnNao).Or(cf.ByName(PromobConfig.BtnNaoAlt))));
                            
                            if (btnNao != null) InteractionHelper.ClicarComFallback(btnNao);
                            else {
                                InteractionHelper.AtivarJanela(popup.AsWindow());
                                Keyboard.Type("n");
                            }
                        }
                        else {
                            AppLogs.LogImportadorPopupAtencaoGenerico(texto);
                            PromobCarregadorProjeto.TratarPopupGenerico(popup.AsWindow());
                        }
                        precisouTentarDenovo = true;
                        InteractionHelper.EsperarUiRespirar(800);
                    }

                    // Tratamento para popup de Aviso ("Não é possível salvar enquanto há erros...")
                    if (InteractionHelper.ContemQualquer(name, PromobConfig.TitulosAviso)){
                        var textElement = popup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                        var texto = textElement?.Properties.Name.ValueOrDefault ?? "";
                        AppLogs.LogImportadorPromobErroExibido(texto);

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
                    AppLogs.LogImportadorRetornandoLoopResolucao();
                    continue; 
                }

                break; // Sai do loop se não houve popups interceptados
            }
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Aguarda a conclusão da importação (fechamento do Wizard) e trata popups que podem 
        /// surgir durante o processo, como a confirmação de "Deseja importar como novo projeto?".
        /// </summary>
        /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        /// <param name="janelaWizard">A janela do wizard de importação.</param>
        //--------------------------------------------------------------------------------------
        public static void AguardarImportacaoETratarPopups(UIA3Automation automation, Window janelaWizard){
            AppLogs.LogImportadorAguardandoConclusaoImportacao();

            var swAguardar = System.Diagnostics.Stopwatch.StartNew();
            
            while (swAguardar.ElapsedMilliseconds < 45000){
                try{
                    // Se o wizard sumiu ou fechou, a importação terminou.
                    if (janelaWizard.IsAvailable == false || janelaWizard.Properties.IsOffscreen.ValueOrDefault){
                        AppLogs.LogImportadorWizardFechadoImportacaoConcluida();
                        break;
                    }
                }
                catch{
                    AppLogs.LogImportadorWizardInacessivelImportacaoConcluida();
                    break;
                }

                var popup = PromobWindowHelper.EncontrarPopupAtencao(automation.GetDesktop(), PromobWindowHelper.CachedProcessIdPromob);
                if (popup != null && popup.Name != janelaWizard.Name){
                    AppLogs.LogImportadorPopupDuranteImportacao(popup.Name);
                    InteractionHelper.AtivarJanela(popup);

                    var textElement = popup.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
                    var texto = textElement?.Properties.Name.ValueOrDefault ?? "";
                    AppLogs.LogImportadorPopupDuranteImportacaoTexto(texto);

                    AutomationElement? btnClicar = null;

                    if (texto.Contains("novo projeto", StringComparison.OrdinalIgnoreCase)){
                        AppLogs.LogImportadorPopupNovoProjeto();
                        btnClicar = popup.FindFirstDescendant(cf => 
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnCancelar)));
                    }
                    else if (texto.Contains("substituir", StringComparison.OrdinalIgnoreCase) || texto.Contains("sobrepor", StringComparison.OrdinalIgnoreCase)){
                        AppLogs.LogImportadorPopupSubstituirProjeto();
                        btnClicar = popup.FindFirstDescendant(cf => 
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName(PromobConfig.BtnSim)));
                    }

                    if (btnClicar == null){
                        btnClicar = popup.FindFirstDescendant(cf => 
                            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(
                                cf.ByName(PromobConfig.BtnSim).Or(
                                cf.ByName(PromobConfig.BtnOk)).Or(
                                cf.ByName(PromobConfig.BtnOkAlt)).Or(
                                cf.ByName(PromobConfig.BtnConcluir))
                            ));
                    }

                    if (btnClicar != null){
                        AppLogs.LogImportadorClicandoBotaoPopup(btnClicar.Name);
                        InteractionHelper.ClicarComFallback(btnClicar);
                        
                        // Aguarda o popup fechar para evitar cliques duplicados e lidar com múltiplos popups
                        InteractionHelper.EsperarAte(() => {
                            try { return popup.IsAvailable == false || popup.Properties.IsOffscreen.ValueOrDefault; }
                            catch { return true; }
                        }, 2000, 200);
                    }
                    else{
                        AppLogs.LogImportadorBotaoConfirmacaoNaoEncontradoPopup();
                        Keyboard.Type(VirtualKeyShort.RETURN);
                        InteractionHelper.EsperarUiRespirar(1000);
                    }
                }

                System.Threading.Thread.Sleep(500);
            }

            // Uma espera extra para garantir que a UI principal atualizou a lista de projetos recentes
            AppLogs.LogImportadorAguardandoEstabilizacaoListaProjetos();
            InteractionHelper.EsperarUiRespirar(1500);
        }
    }
}
