using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using PromobAutomacao.Automation;
using PromobAutomacao.Utils;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace PromobAutomacao.Promob{
    //--------------------------------------------------------------------------------------
    /// <summary>
    /// Componente responsável por gerenciar a rotina de atualização do Promob,
    /// cobrindo o acionamento do menu Arquivo > Atualizar o Promob, verificação do status
    /// dos módulos (Desatualizado vs Atualizado) e tomada de decisão de atualizar ou fechar.
    /// </summary>
    //--------------------------------------------------------------------------------------
    public static class PromobUpdater{

        // ==================================================================================
        // WIN32 P/INVOKE — necessário para detectar janelas ocultas/em segundo plano
        // ==================================================================================

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        private const int SW_RESTORE  = 9;
        private const int SW_SHOW     = 5;
        private const int SW_SHOWNA   = 8;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;  // janela minimizada na barra de tarefas
        private const int SW_HIDE     = 0;       // janela completamente oculta
        private const int GWL_STYLE   = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_VISIBLE  = 0x10000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080; // oculto da barra de tarefas

        // ==================================================================================
        // MÉTODOS PÚBLICOS
        // ==================================================================================

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Executa a rotina completa de atualização acionada pelo botão "Atualizar Promob" na UI.
        /// Abre o menu Arquivo, clica em "Atualizar o Promob", e delega para o fluxo unificado.
        /// </summary>
        /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void ExecutarAtualizacao(UIA3Automation automation){
            AppLogs.LogUpdaterIniciandoRotina();

            AppLogs.LogUpdaterLocalizandoJanela();
            var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 15000)
                ?? throw new Exception("Janela do Promob não encontrada. Abra o Promob antes de atualizar.");

            InteractionHelper.AtivarJanela(janela);
            
            AppLogs.LogUpdaterLocalizandoMenuArquivo();
            var buscaEm = WindowFinder.ObterHostOuJanela(janela, PromobConfig.AutomationIdHost, PromobWindowHelper.CachedProcessIdPromob);

            AutomationElement? menuArquivo = WindowFinder.BuscarElementoComFallback(
                buscaEm,
                cf => cf.ByName("Arquivo"),
                e => (e.Properties.Name.ValueOrDefault ?? "").Equals("Arquivo", StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            // AppLogs.LogWorkflowProcurandoJanelaBotoes();
            // var janelaInicial = PromobWindowHelper.AguardarJanelaPromob(automation, 5000);
            // if (janelaInicial != null){
            //     Diagnostics.ListarBotoesProject(janelaInicial, PromobConfig.AutomationIdHost);
            // }

            if (menuArquivo == null){
                throw new Exception("Menu 'Arquivo' não encontrado na janela do Promob.");
            }

            // Verifica se o botão "Atualizar o Promob" já está visível e habilitado na tela.
            // Se já estiver visível (Ribbon expandido na aba Arquivo), clicar no menu "Arquivo" iria
            // recolher/esconder o Ribbon, fazendo o botão desaparecer!
            AppLogs.LogUpdaterVerificandoBotaoVisivel();
            AutomationElement? btnPrevia = WindowFinder.BuscarElementoComFallback(
                buscaEm,
                cf => cf.ByAutomationId("OpenProcadUpdate"),
                e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("OpenProcadUpdate", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.Name.ValueOrDefault ?? "").Equals("Atualizar o Promob", StringComparison.OrdinalIgnoreCase),
                limitarAoMesmoProcesso: true,
                processId: PromobWindowHelper.CachedProcessIdPromob
            );

            if (btnPrevia != null && btnPrevia.IsEnabled) {
                AppLogs.LogUpdaterBotaoJaVisivel();
            }
            else {
                AppLogs.LogUpdaterClicandoMenuArquivo();
                InteractionHelper.ClicarComFallback(menuArquivo);
                InteractionHelper.EsperarUiRespirar(1000);
            }

            AppLogs.LogUpdaterLocalizandoBotaoAtualizar();
            AutomationElement? btnAtualizarPromob = LocalizarBotaoAtualizarPromob(automation, buscaEm);

            if (btnAtualizarPromob == null){
                throw new Exception("Botão 'Atualizar o Promob' (OpenProcadUpdate) não encontrado no menu 'Arquivo' (tentativas na janela principal e popups do desktop esgotadas).");
            }

            AppLogs.LogUpdaterClicandoAtualizar();
            InteractionHelper.ClicarComFallback(btnAtualizarPromob);

            AppLogs.LogUpdaterAguardandoJanelaUpdate();
            Window? janelaUpdate = AguardarJanelaUpdate(automation, 25000);

            if (janelaUpdate == null){
                throw new Exception("Janela 'Promob Update' não apareceu a tempo.");
            }

            AppLogs.LogUpdaterJanelaUpdateDetectada();

            // Delega para o fluxo unificado de verificação + atualização
            VerificarStatusEAtualizar(janelaUpdate, automation);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Interage com a janela "Promob Update" que foi aberta automaticamente pelo próprio Promob.
        /// Ao contrário de <see cref="ExecutarAtualizacao"/>, aqui a janela já está aberta —
        /// o método apenas a localiza e delega para o fluxo unificado.
        /// </summary>
        /// <param name="automation">A instância ativa do motor de automação UIA3.</param>
        //--------------------------------------------------------------------------------------
        public static void ExecutarAtualizacaoUpdate(UIA3Automation automation){
            AppLogs.LogUpdaterIniciandoVerificacaoAutomatica();

            // 1. ANTES DE QUALQUER COISA: Verifica se o popup de sucesso "PromobUpdate" já está aberto.
            // Se estiver, pulamos todas as etapas intermediárias e fechamos ele diretamente.
            AppLogs.LogUpdaterVerificandoConclusaoAnterior();
            var (janelaSucessoPrevia, btnFecharSucessoPrevio) = BuscarPopupSucessoNoDesktop(automation);

            if (janelaSucessoPrevia != null && btnFecharSucessoPrevio != null) {
                AppLogs.LogUpdaterConcluidaSucessoAnterior();
                FinalizarEFecharPopupSucesso(janelaSucessoPrevia, btnFecharSucessoPrevio);
                return;
            }

            // 2. Fluxo Normal: Re-localiza a janela principal "Promob Update"
            AppLogs.LogUpdaterRelocalizandoJanelaUpdate();
            Window? janelaUpdate = AguardarJanelaUpdate(automation, 10000);

            if (janelaUpdate == null){
                throw new Exception("[UPDATE] Janela 'Promob Update' não encontrada para clicar em Atualizar.");
            }

            AppLogs.LogUpdaterForcandoFoco();
            try { janelaUpdate.SetForeground(); } catch { }
            InteractionHelper.EsperarUiRespirar(800);
            try { janelaUpdate.SetForeground(); } catch { }
            InteractionHelper.EsperarUiRespirar(1200);

            // Delega para o fluxo unificado de verificação + atualização
            VerificarStatusEAtualizar(janelaUpdate, automation);
        }

        // ==================================================================================
        // MÉTODOS PRIVADOS — FLUXO UNIFICADO
        // ==================================================================================

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Fluxo unificado: clica na aba "Status", verifica se há módulos desatualizados
        /// e decide se fecha a janela ou inicia o processo completo de atualização.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void VerificarStatusEAtualizar(Window janelaUpdate, UIA3Automation automation){
            InteractionHelper.AtivarJanela(janelaUpdate);
            InteractionHelper.EsperarUiRespirar(1500);

            // --- Clicar na aba "Status" ---
            AppLogs.LogUpdaterClicandoAbaStatus();
            var btnStatus = janelaUpdate.FindFirstDescendant(cf => cf.ByName("Status"))
                ?? janelaUpdate.FindAllDescendants()
                    .FirstOrDefault(e => (e.Name ?? "").Contains("Status", StringComparison.OrdinalIgnoreCase));

            if (btnStatus == null){
                throw new Exception("Aba/Botão 'Status' não encontrado na janela 'Promob Update'.");
            }

            InteractionHelper.ClicarComFallback(btnStatus);
            AppLogs.LogUpdaterAbaStatusClicada();
            InteractionHelper.EsperarUiRespirar(3000);

            // --- Analisar status dos módulos ---
            AppLogs.LogUpdaterAnalisandoStatusModulos();
            var todosElementos = janelaUpdate.FindAllDescendants().ToList();
            
            bool temDesatualizado = todosElementos.Any(e => 
                (e.Name ?? "").Contains("Desatualizado", StringComparison.OrdinalIgnoreCase) ||
                (e.Properties.AutomationId.ValueOrDefault ?? "").Contains("Desatualizado", StringComparison.OrdinalIgnoreCase)
            );

            if (!temDesatualizado){
                AppLogs.LogUpdaterModulosAtualizados();
                FecharJanela(janelaUpdate);
                return;
            }

            // --- Há módulos desatualizados: iniciar atualização ---
            AppLogs.LogUpdaterModulosDesatualizadosDetectados();
            AppLogs.LogUpdaterClicandoAbaAtualizar();
            
            var btnAtualizarAba = janelaUpdate.FindFirstDescendant(cf => cf.ByName("Atualizar"))
                ?? janelaUpdate.FindAllDescendants()
                    .FirstOrDefault(e => (e.Name ?? "").Equals("Atualizar", StringComparison.OrdinalIgnoreCase));

            if (btnAtualizarAba == null){
                throw new Exception("Botão/Aba 'Atualizar' não encontrado no painel lateral.");
            }

            InteractionHelper.ClicarComFallback(btnAtualizarAba);
            AppLogs.LogUpdaterAbaAtualizarClicada();

            ExecutarFluxoAtualizacao(janelaUpdate, automation);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Executa o fluxo completo de atualização após a decisão de atualizar:
        /// aguarda botão "Atualizar" no rodapé → clica → aguarda "Instalar" → clica →
        /// aguarda "Ok" no alerta → clica → aguarda popup de sucesso → fecha.
        /// Também trata o caso de "Não existem novas atualizações".
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void ExecutarFluxoAtualizacao(Window janelaUpdate, UIA3Automation automation){
            AppLogs.LogUpdaterAguardandoRodape();
            
            AutomationElement? btnAtualizar = null;
            var swCarregando = Stopwatch.StartNew();
            const int timeoutCarregamentoMs = 120000; // 2 minutos
            
            while (swCarregando.ElapsedMilliseconds < timeoutCarregamentoMs){
                var todosElementos = janelaUpdate.FindAllDescendants();
                
                // 1. Tenta encontrar diretamente pelo AutomationId 'btnUpdate' habilitado
                btnAtualizar = todosElementos.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.Button || e.ControlType == FlaUI.Core.Definitions.ControlType.Custom) &&
                    (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnUpdate", StringComparison.OrdinalIgnoreCase) &&
                    e.IsEnabled
                );

                if (btnAtualizar != null) {
                    AppLogs.LogUpdaterBtnUpdateHabilitado();
                    break;
                }

                // Fallback: botão "Atualizar" no rodapé (próximo ao botão "Fechar" do rodapé)
                var btnFecharRodape = todosElementos.FirstOrDefault(e =>
                    e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                    (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase)
                );

                if (btnFecharRodape != null) {
                    var candidatos = todosElementos.Where(e =>
                        (e.ControlType == FlaUI.Core.Definitions.ControlType.Button || e.ControlType == FlaUI.Core.Definitions.ControlType.Custom) &&
                        (e.Name ?? "").Contains("Atualizar", StringComparison.OrdinalIgnoreCase) &&
                        e.IsEnabled
                    ).ToList();

                    var yFechar = btnFecharRodape.BoundingRectangle.Y;
                    var candidatoRodape = candidatos
                        .FirstOrDefault(c => Math.Abs(c.BoundingRectangle.Y - yFechar) < 25);

                    if (candidatoRodape != null) {
                        btnAtualizar = candidatoRodape;
                        AppLogs.LogUpdaterBtnUpdateProximidade();
                        break;
                    }
                }

                // 2. Verifica se "Não existem novas atualizações" — nesse caso, fecha a janela
                var txtSemAtualizacao = todosElementos.FirstOrDefault(e =>
                    (e.Name ?? "").Contains("Não existem novas atualizações", StringComparison.OrdinalIgnoreCase) ||
                    (e.Name ?? "").Contains("Não existem atualizações", StringComparison.OrdinalIgnoreCase)
                );

                if (txtSemAtualizacao != null) {
                    AppLogs.LogUpdaterNenhumaAtualizacaoDisponivel();
                    FecharJanela(janelaUpdate);
                    return;
                }

                // Log periódico do progresso a cada 4 segundos
                if (((int)swCarregando.Elapsed.TotalSeconds) % 4 == 0) {
                    var txtBuscando = todosElementos.FirstOrDefault(e => 
                        (e.Name ?? "").Contains("Buscando atualizações", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("Verificando arquivos", StringComparison.OrdinalIgnoreCase)
                    );
                    if (txtBuscando != null) {
                        AppLogs.LogUpdaterBuscandoAtualizacoesPendentes(txtBuscando.Name);
                    }
                    else {
                        AppLogs.LogUpdaterAguardandoCarregamentoArquivos();
                    }
                }

                Thread.Sleep(1000);
            }

            if (btnAtualizar == null){
                var todosElementosFinal = janelaUpdate.FindAllDescendants();
                var botoes = todosElementosFinal.Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button).ToList();
                AppLogs.LogUpdaterBotoesDisponiveisTimeout(string.Join(" | ", botoes.Select(b => $"'{b.Name}' (ID: {b.Properties.AutomationId.ValueOrDefault})")));
                throw new Exception("[FLUXO] Timeout: O botão 'Atualizar' (btnUpdate) não apareceu no rodapé da janela do Promob Update.");
            }

            // --- Clicar no botão "Atualizar" do rodapé ---
            AppLogs.LogUpdaterBotaoAtualizarDefinido(btnAtualizar.ControlType.ToString(), btnAtualizar.Name, btnAtualizar.Properties.AutomationId.ValueOrDefault);
            ClicarBotaoComEstrategias(janelaUpdate, btnAtualizar, "Atualizar");

            // --- Aguardar download e clicar em "Instalar" ---
            AppLogs.LogUpdaterBaixandoAtualizacoes();
            
            AutomationElement? btnInstalar = null;
            var swDownload = Stopwatch.StartNew();
            const int timeoutDownloadMs = 600000; // 10 minutos
            
            while (swDownload.ElapsedMilliseconds < timeoutDownloadMs) {
                var todosElementosFresh = janelaUpdate.FindAllDescendants();
                
                btnInstalar = todosElementosFresh.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.Button || e.ControlType == FlaUI.Core.Definitions.ControlType.Custom) &&
                    ((e.Name ?? "").Contains("Instalar", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnInstall", StringComparison.OrdinalIgnoreCase) ||
                     ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnUpdate", StringComparison.OrdinalIgnoreCase) && (e.Name ?? "").Contains("Instalar", StringComparison.OrdinalIgnoreCase))) &&
                    e.IsEnabled
                );

                if (btnInstalar != null) {
                    AppLogs.LogUpdaterBtnInstalarHabilitado();
                    break;
                }

                // Log do progresso do download a cada 5 segundos
                if (((int)swDownload.Elapsed.TotalSeconds) % 5 == 0) {
                    var txtProgresso = todosElementosFresh.FirstOrDefault(e =>
                        (e.Name ?? "").Contains("Baixando", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("Download", StringComparison.OrdinalIgnoreCase) ||
                        (e.Name ?? "").Contains("%", StringComparison.OrdinalIgnoreCase)
                    );

                    if (txtProgresso != null) {
                        AppLogs.LogUpdaterProgressoDownload(txtProgresso.Name);
                    }
                    else {
                        AppLogs.LogUpdaterTempoDownload(swDownload.Elapsed.Minutes, swDownload.Elapsed.Seconds);
                    }
                }

                Thread.Sleep(1500);
            }

            if (btnInstalar == null) {
                throw new Exception("[FLUXO] Timeout: O download demorou mais de 10 minutos ou o botão 'Instalar' não apareceu.");
            }

            AppLogs.LogUpdaterBotaoInstalarDefinido(btnInstalar.ControlType.ToString(), btnInstalar.Name, btnInstalar.Properties.AutomationId.ValueOrDefault);
            ClicarBotaoComEstrategias(janelaUpdate, btnInstalar, "Instalar");

            // --- Aguardar e clicar no botão "Ok" do alerta de fechamento ---
            AppLogs.LogUpdaterConfirmandoFechamento();
            
            AutomationElement? btnOk = null;
            var swAlerta = Stopwatch.StartNew();
            const int timeoutAlertaMs = 25000;
            
            while (swAlerta.ElapsedMilliseconds < timeoutAlertaMs) {
                var todosElementosFresh = janelaUpdate.FindAllDescendants();
                
                var candidatosOk = todosElementosFresh.Where(e =>
                    ((e.Name ?? "").Equals("Ok", StringComparison.OrdinalIgnoreCase) ||
                     (e.Name ?? "").Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnOk", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnOK", StringComparison.OrdinalIgnoreCase)) &&
                    e.IsEnabled
                ).ToList();

                if (candidatosOk.Count > 0) {
                    btnOk = candidatosOk.FirstOrDefault(e => e.ControlType == FlaUI.Core.Definitions.ControlType.Button)
                         ?? candidatosOk.First();
                    
                    AppLogs.LogUpdaterElementoOkLocalizado(btnOk.Name, btnOk.ControlType.ToString(), btnOk.Properties.AutomationId.ValueOrDefault);
                    break;
                }

                Thread.Sleep(500);
            }

            if (btnOk == null) {
                throw new Exception("[FLUXO] Timeout: O botão 'Ok' de confirmação de fechamento não apareceu na tela.");
            }

            AppLogs.LogUpdaterBotaoOkDefinido(btnOk.ControlType.ToString(), btnOk.Name, btnOk.Properties.AutomationId.ValueOrDefault);
            ClicarBotaoComEstrategias(janelaUpdate, btnOk, "Ok");
            AppLogs.LogUpdaterCliqueOkSucesso();

            // --- Aguardar popup de sucesso da instalação e fechar ---
            AguardarEFecharPopupSucesso(automation);
        }

        // ==================================================================================
        // MÉTODOS PRIVADOS — UTILITÁRIOS
        // ==================================================================================

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Executa a sequência completa de estratégias de clique para acionar um botão:
        /// 1. Clique físico por coordenadas absolutas (Mouse real)
        /// 2. Clique via UIA Invoke/Click fallback
        /// 3. Envio direto de teclado (Focus + ENTER + SPACE)
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void ClicarBotaoComEstrategias(Window janela, AutomationElement botao, string label){
            // Garante foco na janela e no botão
            try { janela.SetForeground(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);
            try { botao.Focus(); } catch {}
            InteractionHelper.EsperarUiRespirar(300);

            // ESTRATÉGIA 1: Clique Físico por coordenadas absolutas
            var rect = botao.BoundingRectangle;
            if (!rect.IsEmpty) {
                try {
                    int x = (int)(rect.X + (rect.Width / 2));
                    int y = (int)(rect.Y + (rect.Height / 2));
                    
                    AppLogs.LogUpdaterMovendoCursor(x, y);
                    Mouse.MoveTo(x, y);
                    InteractionHelper.EsperarUiRespirar(250);
                    Mouse.Click();
                    InteractionHelper.EsperarUiRespirar(1000);
                }
                catch (Exception exMouse) {
                    AppLogs.LogUpdaterFalhaCliqueFisico(label, exMouse.Message);
                }
            }

            // ESTRATÉGIA 2: Clicar com Fallback UIA (Invoke -> Click -> Keyboard Space)
            AppLogs.LogUpdaterAcionandoFallback();
            InteractionHelper.ClicarComFallback(botao);
            InteractionHelper.EsperarUiRespirar(500);

            // ESTRATÉGIA 3: Envio Direto de Teclado (Focus + ENTER / ESPAÇO)
            try {
                AppLogs.LogUpdaterEnviandoFocusEnterSpace();
                botao.Focus();
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.ENTER);
                InteractionHelper.EsperarUiRespirar(150);
                Keyboard.Type(VirtualKeyShort.SPACE);
            }
            catch (Exception exKey) {
                AppLogs.LogUpdaterFalhaTeclado(label, exKey.Message);
            }
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Faz polling buscando a janela "Promob Update" até que seja encontrada ou o
        /// timeout expire. Além do scan normal do desktop via FlaUI, usa EnumWindows para
        /// detectar janelas ocultas/em segundo plano (ícone na área de notificação) e as
        /// restaura automaticamente via ShowWindow antes de retorná-las.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static Window? AguardarJanelaUpdate(UIA3Automation automation, int timeoutMs){
            var sw = Stopwatch.StartNew();
            bool logDiagnosticoFeito = false; // Loga janelas Promob apenas uma vez no primeiro ciclo
            bool trayTentado = false;

            while (sw.ElapsedMilliseconds < timeoutMs){

                // ETAPA 1: Scan normal via FlaUI (janelas visíveis no desktop)
                var janelas = automation.GetDesktop().FindAllChildren();
                foreach (var child in janelas){
                    if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                    try {
                        var titulo = child.Name ?? child.Properties.Name.ValueOrDefault ?? "";
                        if (titulo.Contains("Promob Update", StringComparison.OrdinalIgnoreCase)){
                            return child.AsWindow();
                        }
                    }
                    catch { }
                }

                // Se após 5 segundos a janela não apareceu e ainda não tentamos o tray, tenta restaurar pelo tray
                if (sw.ElapsedMilliseconds > 5000 && !trayTentado) {
                    trayTentado = true;
                    Logger.Log("[UPDATE] Janela de atualização não apareceu. Tentando restaurar a partir do tray...", LogLevel.Info);
                    PromobWindowHelper.RestaurarJanelaUpdateDoTray(automation);
                }

                // ETAPA 2: Win32 EnumWindows — busca em TODAS as janelas (visíveis e ocultas)
                // Cobre: janelas minimizadas para tray, WS_EX_TOOLWINDOW, showCmd=SW_HIDE
                var hWnd = BuscarERestaurarJanelaOculta("Promob Update", logDiagnostico: !logDiagnosticoFeito);
                logDiagnosticoFeito = true;

                if (hWnd != IntPtr.Zero){
                    // Aguarda o Windows processar a restauração
                    InteractionHelper.EsperarUiRespirar(1200);

                    // Nova varredura FlaUI após restauração
                    var janelasAposRestore = automation.GetDesktop().FindAllChildren();
                    foreach (var child in janelasAposRestore){
                        if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                        try {
                            var titulo = child.Name ?? child.Properties.Name.ValueOrDefault ?? "";
                            if (titulo.Contains("Promob Update", StringComparison.OrdinalIgnoreCase)){
                                AppLogs.LogUpdaterJanelaRestaurada();
                                return child.AsWindow();
                            }
                        }
                        catch { }
                    }

                    // Se o FlaUI ainda não vê, tenta acessar direto pelo HWND via UIA
                    AppLogs.LogUpdaterAcessoDiretoHwnd();
                    try {
                        var elDireto = automation.FromHandle(hWnd);
                        if (elDireto != null) {
                            AppLogs.LogUpdaterElementoObtidoHwnd(elDireto.Name);
                            return elDireto.AsWindow();
                        }
                    }
                    catch (Exception exDirect) {
                        AppLogs.LogUpdaterFalhaHwnd(exDirect.Message);
                    }
                }

                Thread.Sleep(500);
            }
            return null;
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Usa EnumWindows do Win32 para varrer TODAS as janelas do sistema (incluindo
        /// visíveis e ocultas) buscando aquelas cujo título contém <paramref name="tituloContem"/>.
        /// Loga TODAS as janelas relacionadas ao Promob encontradas para diagnóstico.
        /// Restaura via ShowWindow qualquer janela encontrada que esteja minimizada/oculta.
        /// </summary>
        /// <returns>O HWND da janela encontrada e restaurada, ou IntPtr.Zero se não encontrada.</returns>
        //--------------------------------------------------------------------------------------
        private static IntPtr BuscarERestaurarJanelaOculta(string tituloContem, bool logDiagnostico = false){
            IntPtr resultado = IntPtr.Zero;
            var sb = new StringBuilder(512);

            EnumWindows((hWnd, _) => {
                // Lê o título da janela (todas, sem filtro de visibilidade)
                sb.Clear();
                GetWindowText(hWnd, sb, sb.Capacity);
                string titulo = sb.ToString();

                // Log diagnóstico: lista qualquer janela com "Promob" no título
                if (logDiagnostico && titulo.Contains("Promob", StringComparison.OrdinalIgnoreCase)) {
                    bool visivel = IsWindowVisible(hWnd);
                    var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
                    GetWindowPlacement(hWnd, ref wp);
                    int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
                    bool isToolWin = (exStyle & WS_EX_TOOLWINDOW) != 0;
                    AppLogs.LogUpdaterHwndDetalhe(hWnd, titulo, visivel, wp.showCmd, isToolWin);
                }

                if (!titulo.Contains(tituloContem, StringComparison.OrdinalIgnoreCase))
                    return true; // Continua enumerando

                // Janela encontrada — verifica se precisa ser restaurada
                var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
                GetWindowPlacement(hWnd, ref placement);
                bool janelaVisivel = IsWindowVisible(hWnd);
                int exStyleJanela  = GetWindowLong(hWnd, GWL_EXSTYLE);

                AppLogs.LogUpdaterJanelaEncontradaHwnd(tituloContem, hWnd, titulo);
                AppLogs.LogUpdaterJanelaEstado(janelaVisivel, placement.showCmd, (exStyleJanela & WS_EX_TOOLWINDOW) != 0);

                // Restaura em todos os casos onde não está em estado normal visível
                bool precisaRestaurar = !janelaVisivel
                    || placement.showCmd == SW_HIDE
                    || placement.showCmd == SW_SHOWMINIMIZED
                    || (exStyleJanela & WS_EX_TOOLWINDOW) != 0;

                if (precisaRestaurar) {
                    AppLogs.LogUpdaterRestaurandoTray();
                    ShowWindow(hWnd, SW_RESTORE);
                    InteractionHelper.EsperarUiRespirar(200);
                    ShowWindow(hWnd, SW_SHOWNORMAL);
                    InteractionHelper.EsperarUiRespirar(200);
                    ShowWindow(hWnd, SW_SHOW);
                    SetForegroundWindow(hWnd);
                    AppLogs.LogUpdaterRestauradaPrimeiroPlano();
                }
                else {
                    AppLogs.LogUpdaterJanelaVisivelTrazendoFrente();
                    SetForegroundWindow(hWnd);
                }

                resultado = hWnd;
                return false; // Para a enumeração
            }, IntPtr.Zero);

            return resultado;
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Localiza o botão "Atualizar o Promob" (OpenProcadUpdate) na janela principal
        /// ou nos popups do desktop pertencentes ao processo do Promob.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static AutomationElement? LocalizarBotaoAtualizarPromob(UIA3Automation automation, AutomationElement buscaEm){
            AutomationElement? btnAtualizarPromob = null;
            var swBusca = Stopwatch.StartNew();
            const int timeoutBuscaMs = 500;
            
            while (swBusca.ElapsedMilliseconds < timeoutBuscaMs){
                // Invalida cache de host para busca limpa
                WindowFinder.CachedHost = null;

                // 1. Tenta buscar a partir do Host elementHost1 (buscaEm)
                btnAtualizarPromob = WindowFinder.BuscarElementoComFallback(
                    buscaEm,
                    cf => cf.ByAutomationId("OpenProcadUpdate"),
                    e => (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("OpenProcadUpdate", StringComparison.OrdinalIgnoreCase) ||
                         (e.Properties.Name.ValueOrDefault ?? "").Equals("Atualizar o Promob", StringComparison.OrdinalIgnoreCase),
                    limitarAoMesmoProcesso: true,
                    processId: PromobWindowHelper.CachedProcessIdPromob
                );

                if (btnAtualizarPromob != null) return btnAtualizarPromob;

                // 2. Tenta buscar nas janelas/popups do Desktop pertencentes ao mesmo processo
                var desktopChildren = automation.GetDesktop().FindAllChildren();
                foreach (var child in desktopChildren){
                    try{
                        if (child.Properties.ProcessId.ValueOrDefault == PromobWindowHelper.CachedProcessIdPromob){
                            btnAtualizarPromob = child.FindFirstDescendant(cf => cf.ByAutomationId("OpenProcadUpdate"))
                                              ?? child.FindFirstDescendant(cf => cf.ByName("Atualizar o Promob"));
                            if (btnAtualizarPromob != null){
                                AppLogs.LogUpdaterBotaoEncontradoPopup(btnAtualizarPromob.Name);
                                return btnAtualizarPromob;
                            }
                        }
                    }
                    catch { }
                }

                Thread.Sleep(500);
            }

            return null;
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Fecha a janela "Promob Update" clicando no botão "Fechar" do rodapé
        /// e, se necessário, confirma o alerta clicando em "Sim".
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void FecharJanela(Window janelaUpdate){
            AppLogs.LogUpdaterClicandoFecharJanela();

            var todosElementos = janelaUpdate.FindAllDescendants();

            // Localiza o botão "Fechar" do rodapé (btnClose ou pelo nome, excluindo o Close do topo)
            var btnFechar = todosElementos.FirstOrDefault(e =>
                e.ControlType == FlaUI.Core.Definitions.ControlType.Button &&
                ((e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase) ||
                 ((e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase) && 
                  !(e.Properties.AutomationId.ValueOrDefault ?? "").Equals("Close", StringComparison.OrdinalIgnoreCase)))
            );

            if (btnFechar == null){
                // Fallback: busca qualquer elemento com nome "Fechar"
                btnFechar = todosElementos.FirstOrDefault(e => 
                    (e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase));
            }

            if (btnFechar == null){
                throw new Exception("Botão 'Fechar' não encontrado na janela 'Promob Update'.");
            }

            ClicarBotaoComEstrategias(janelaUpdate, btnFechar, "Fechar");

            // --- Confirmar fechamento clicando em "Sim" no alerta (se aparecer) ---
            AppLogs.LogUpdaterAlertaSimNaoApareceu();
            
            AutomationElement? btnSim = null;
            var swSim = Stopwatch.StartNew();
            const int timeoutSimMs = 15000;
            
            while (swSim.ElapsedMilliseconds < timeoutSimMs) {
                var todosElementosAlerta = janelaUpdate.FindAllDescendants();
                
                btnSim = todosElementosAlerta.FirstOrDefault(e =>
                    (e.ControlType == FlaUI.Core.Definitions.ControlType.Button) &&
                    ((e.Name ?? "").Equals("Sim", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnYes", StringComparison.OrdinalIgnoreCase)) &&
                    e.IsEnabled
                );

                if (btnSim != null) {
                    AppLogs.LogUpdaterBtnSimLocalizado();
                    break;
                }

                Thread.Sleep(500);
            }

            if (btnSim != null) {
                AppLogs.LogUpdaterBtnSimDefinido(btnSim.ControlType.ToString(), btnSim.Name);
                ClicarBotaoComEstrategias(janelaUpdate, btnSim, "Sim");
                AppLogs.LogUpdaterSimClicado();
            }
            else {
                AppLogs.LogUpdaterAlertaSimNaoApareceu();
            }
            
            AppLogs.LogUpdaterJanelaFechada();
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Busca no desktop o popup de sucesso "PromobUpdate" (sem espaço) que contém
        /// um botão "Fechar". Retorna a janela e o botão, ou (null, null).
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static (Window? janela, AutomationElement? btnFechar) BuscarPopupSucessoNoDesktop(UIA3Automation automation){
            // ETAPA 1: Tenta restaurar a janela caso esteja oculta (ícone em segundo plano)
            // O popup "PromobUpdate" pode estar invisível para o FlaUI
            var hOculta = BuscarERestaurarJanelaOculta("PromobUpdate");
            if (hOculta != IntPtr.Zero){
                AppLogs.LogUpdaterPopupSucessoOcultoRestaurado();
                InteractionHelper.EsperarUiRespirar(800);
            }

            // ETAPA 2: Scan via FlaUI (janelas visíveis, incluindo a recém-restaurada)
            try {
                var janelasDesktop = automation.GetDesktop().FindAllChildren();
                foreach (var child in janelasDesktop) {
                    if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                    string nome = child.Name ?? "";
                    if (nome.Equals("PromobUpdate", StringComparison.OrdinalIgnoreCase)) {
                        var descSucesso = child.FindAllDescendants();
                        var btnCheck = descSucesso.FirstOrDefault(e =>
                            (e.ControlType == FlaUI.Core.Definitions.ControlType.Button) &&
                            ((e.Name ?? "").Equals("Fechar", StringComparison.OrdinalIgnoreCase) ||
                             (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase))
                        );
                        if (btnCheck != null) {
                            return (child.AsWindow(), btnCheck);
                        }
                    }
                }
            }
            catch (Exception exCheck) {
                AppLogs.LogUpdaterFalhaVerificarPopupSucesso(exCheck.Message);
            }

            return (null, null);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Aguarda o popup de sucesso "PromobUpdate" aparecer no desktop (até 5 minutos)
        /// e aciona o fechamento via <see cref="FinalizarEFecharPopupSucesso"/>.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void AguardarEFecharPopupSucesso(UIA3Automation automation){
            AppLogs.LogUpdaterBaixandoAtualizacoes(); // Usa a mensagem de andamento
            
            AutomationElement? btnFecharSucesso = null;
            Window? janelaSucesso = null;
            var swSucesso = Stopwatch.StartNew();
            const int timeoutSucessoMs = 300000; // 5 minutos
            
            while (swSucesso.ElapsedMilliseconds < timeoutSucessoMs) {
                var (janela, btn) = BuscarPopupSucessoNoDesktop(automation);
                if (janela != null && btn != null) {
                    janelaSucesso = janela;
                    btnFecharSucesso = btn;
                    break;
                }

                if (((int)swSucesso.Elapsed.TotalSeconds) % 10 == 0) {
                    AppLogs.LogUpdaterTempoInstalacao(swSucesso.Elapsed.Minutes, swSucesso.Elapsed.Seconds);
                }

                Thread.Sleep(2000);
            }

            if (btnFecharSucesso == null || janelaSucesso == null) {
                throw new Exception("[SUCESSO] Timeout: O popup de conclusão da instalação não apareceu após 5 minutos.");
            }

            FinalizarEFecharPopupSucesso(janelaSucesso, btnFecharSucesso);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Executa a sequência de cliques e atalhos de teclado para focar e
        /// acionar o botão "Fechar" da janela de sucesso "PromobUpdate".
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void FinalizarEFecharPopupSucesso(Window janelaSucesso, AutomationElement btnFecharSucesso) {
            AppLogs.LogUpdaterBtnFecharSucessoDefinido(btnFecharSucesso.ControlType.ToString(), btnFecharSucesso.Name);
            ClicarBotaoComEstrategias(janelaSucesso, btnFecharSucesso, "Fechar Sucesso");
            AppLogs.LogUpdaterConcluidaSucesso();
        }

    }
}
