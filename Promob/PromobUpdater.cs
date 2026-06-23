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
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;  // janela minimizada na barra de tarefas
        private const int SW_HIDE     = 0;       // janela completamente oculta
        private const int GWL_EXSTYLE = -20;
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
            var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 5000)
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
            InteractionHelper.EsperarUiRespirar(500);

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
            const int timeoutCarregamentoMs = 900000; // 15 minutos
            
            while (swCarregando.ElapsedMilliseconds < timeoutCarregamentoMs){
                // Garante que a janela de atualização esteja ativa e em primeiro plano
                try {
                    InteractionHelper.AtivarJanela(janelaUpdate);
                }
                catch { }

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
            InteractionHelper.ClicarComEstrategias(btnAtualizar, janelaUpdate, "Atualizar");

            // --- Aguardar download e clicar em "Instalar" ---
            AppLogs.LogUpdaterBaixandoAtualizacoes();
            
            AutomationElement? btnInstalar = null;
            var swDownload = Stopwatch.StartNew();
            const int timeoutDownloadMs = 900000; // 15 minutos
            
            while (swDownload.ElapsedMilliseconds < timeoutDownloadMs) {
                // Garante que a janela de atualização esteja ativa e em primeiro plano
                try {
                    InteractionHelper.AtivarJanela(janelaUpdate);
                }
                catch { }

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
                throw new Exception("[FLUXO] Timeout: O download demorou mais de 15 minutos ou o botão 'Instalar' não apareceu.");
            }

            AppLogs.LogUpdaterBotaoInstalarDefinido(btnInstalar.ControlType.ToString(), btnInstalar.Name, btnInstalar.Properties.AutomationId.ValueOrDefault);
            InteractionHelper.ClicarComEstrategias(btnInstalar, janelaUpdate, "Instalar");

            // --- Aguardar e clicar no botão "Ok" do alerta de fechamento ---
            AppLogs.LogUpdaterConfirmandoFechamento();
            
            AutomationElement? btnOk = null;
            var swAlerta = Stopwatch.StartNew();
            const int timeoutAlertaMs = 30000; // 30 segundos
            
            while (swAlerta.ElapsedMilliseconds < timeoutAlertaMs) {
                // Garante que a janela de atualização esteja ativa e em primeiro plano
                try {
                    InteractionHelper.AtivarJanela(janelaUpdate);
                }
                catch { }

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
            InteractionHelper.ClicarComEstrategias(btnOk, janelaUpdate, "Ok");
            AppLogs.LogUpdaterCliqueOkSucesso();

            IntPtr hwndUpdate = IntPtr.Zero;
            try {
                hwndUpdate = (IntPtr)janelaUpdate.Properties.NativeWindowHandle.ValueOrDefault;
            } catch { }

            // --- Aguardar popup de sucesso da instalação e fechar ---
            AguardarEFecharPopupSucesso(automation, hwndUpdate);
        }

        // ==================================================================================
        // MÉTODOS PRIVADOS — UTILITÁRIOS
        // ==================================================================================



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
        private static IntPtr BuscarERestaurarJanelaOculta(Func<string, IntPtr, bool> matchPredicate, bool logDiagnostico = false){
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

                if (!matchPredicate(titulo, hWnd))
                    return true; // Continua enumerando

                // Janela encontrada — verifica se precisa ser restaurada
                var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)) };
                GetWindowPlacement(hWnd, ref placement);
                bool janelaVisivel = IsWindowVisible(hWnd);
                int exStyleJanela  = GetWindowLong(hWnd, GWL_EXSTYLE);

                AppLogs.LogUpdaterJanelaEncontradaHwnd("Filtro Customizado", hWnd, titulo);
                AppLogs.LogUpdaterJanelaEstado(janelaVisivel, placement.showCmd, (exStyleJanela & WS_EX_TOOLWINDOW) != 0);

                // Restaura em todos os casos onde não está em estado normal visível
                bool precisaRestaurar = !janelaVisivel
                    || placement.showCmd == SW_HIDE
                    || placement.showCmd == SW_SHOWMINIMIZED
                    || (exStyleJanela & WS_EX_TOOLWINDOW) != 0;

                if (precisaRestaurar) {
                    AppLogs.LogUpdaterRestaurandoTray();
                    ShowWindow(hWnd, SW_RESTORE);
                    InteractionHelper.EsperarUiRespirar(250);
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

        private static IntPtr BuscarERestaurarJanelaOculta(string tituloContem, bool logDiagnostico = false){
            return BuscarERestaurarJanelaOculta((titulo, _) => titulo.Contains(tituloContem, StringComparison.OrdinalIgnoreCase), logDiagnostico);
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

            InteractionHelper.ClicarComEstrategias(btnFechar, janelaUpdate, "Fechar");

            // --- Confirmar fechamento clicando em "Sim" no alerta (se aparecer) ---
            AppLogs.LogUpdaterAlertaSimNaoApareceu();
            
            AutomationElement? btnSim = null;
            var swSim = Stopwatch.StartNew();
            const int timeoutSimMs = 15000;
            
            while (swSim.ElapsedMilliseconds < timeoutSimMs) {
                // Se a janela principal já fechou ou sumiu, não há motivo para continuar esperando pelo popup
                try {
                    if (janelaUpdate.IsAvailable == false || janelaUpdate.Properties.IsOffscreen.ValueOrDefault) {
                        break;
                    }
                }
                catch {
                    break;
                }

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
                InteractionHelper.ClicarComEstrategias(btnSim, janelaUpdate, "Sim");
                AppLogs.LogUpdaterSimClicado();
            }
            else {
                AppLogs.LogUpdaterAlertaSimNaoApareceu();
            }
            
            AppLogs.LogUpdaterJanelaFechada();
        }

        // Palavras-chave que indicam que a janela é a tela de CONCLUSÃO da atualização.
        // Mantido amplo de propósito: o Promob varia o texto ("concluída", "completa",
        // "atualizado com sucesso" etc.) dependendo da versão/módulo.
        private static readonly string[] TextosConclusaoSucesso = {
            "concluíd",   // concluída / concluído / concluido
            "sucedida",
            "sucesso",
            "completa",
            "completo",
            "atualizado",
            "finalizad",  // finalizada / finalizado
        };

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Indica se algum dos textos de conclusão de sucesso está contido no nome informado.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static bool ContemTextoConclusaoSucesso(string? texto) {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            return TextosConclusaoSucesso.Any(k => texto.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Verifica se uma janela identificada por HWND é a janela de sucesso da instalação
        /// analisando a presença de textos de conclusão.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static bool VerificarSeEhJanelaSucessoPorHwnd(IntPtr hWnd, UIA3Automation automation) {
            try {
                var el = automation.FromHandle(hWnd);
                if (el != null) {
                    var desc = el.FindAllDescendants();
                    return desc.Any(e => ContemTextoConclusaoSucesso(e.Name));
                }
            }
            catch { }
            return false;
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Localiza robustamente o botão "Fechar" (ou qualquer botão alternativo caso não localizado)
        /// na janela de sucesso da instalação.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static AutomationElement? ObterBotaoFecharRobustamente(AutomationElement janela) {
            try {
                var desc = janela.FindAllDescendants();

                // O Promob renderiza vários botões como ControlType.Custom (não só Button),
                // por isso todas as buscas abaixo aceitam Button OU Custom — assim como o
                // restante do fluxo de atualização faz para "Atualizar"/"Instalar"/"Ok".
                bool EhAcionavel(AutomationElement e) =>
                    e.ControlType == FlaUI.Core.Definitions.ControlType.Button ||
                    e.ControlType == FlaUI.Core.Definitions.ControlType.Custom;

                // 1. Botão (Button ou Custom) com nome contendo "Fechar", ou AutomationId conhecido
                var btn = desc.FirstOrDefault(e =>
                    EhAcionavel(e) &&
                    ((e.Name ?? "").Contains("Fechar", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnClose", StringComparison.OrdinalIgnoreCase) ||
                     (e.Properties.AutomationId.ValueOrDefault ?? "").Equals("btnFechar", StringComparison.OrdinalIgnoreCase))
                );
                if (btn != null) {
                    AppLogs.LogUpdaterBotaoFecharSucessoEncontrado("nome/id 'Fechar'", btn.Name, btn.ControlType.ToString(), btn.Properties.AutomationId.ValueOrDefault);
                    return btn;
                }

                // 2. Qualquer elemento (qualquer tipo) cujo nome contenha "Fechar"
                btn = desc.FirstOrDefault(e =>
                    (e.Name ?? "").Contains("Fechar", StringComparison.OrdinalIgnoreCase)
                );
                if (btn != null) {
                    AppLogs.LogUpdaterBotaoFecharSucessoEncontrado("nome contém 'Fechar'", btn.Name, btn.ControlType.ToString(), btn.Properties.AutomationId.ValueOrDefault);
                    return btn;
                }

                // 3. Demais botões de fechamento comuns em popups de confirmação ("OK", "Sim", "Concluir")
                btn = desc.FirstOrDefault(e =>
                    EhAcionavel(e) && e.IsEnabled && !e.Properties.IsOffscreen.ValueOrDefault &&
                    InteractionHelper.ContemQualquer(e.Name, "Ok", "OK", "Sim", "Concluir", "Finalizar")
                );
                if (btn != null) {
                    AppLogs.LogUpdaterBotaoFecharSucessoEncontrado("botão de confirmação", btn.Name, btn.ControlType.ToString(), btn.Properties.AutomationId.ValueOrDefault);
                    return btn;
                }

                // 4. Heurística por posição: em um popup de conclusão o botão de ação fica
                //    na parte inferior. Pega o elemento acionável habilitado/visível mais
                //    abaixo (maior Y) e, em empate, o mais à direita.
                var acionaveis = desc.Where(e =>
                    EhAcionavel(e) &&
                    e.IsEnabled &&
                    !e.Properties.IsOffscreen.ValueOrDefault &&
                    !e.BoundingRectangle.IsEmpty
                ).ToList();

                if (acionaveis.Count > 0) {
                    var candidato = acionaveis
                        .OrderByDescending(e => e.BoundingRectangle.Y)
                        .ThenByDescending(e => e.BoundingRectangle.X)
                        .First();
                    AppLogs.LogUpdaterBotaoFecharSucessoEncontrado("posição (inferior)", candidato.Name, candidato.ControlType.ToString(), candidato.Properties.AutomationId.ValueOrDefault);
                    return candidato;
                }

                // Nenhum candidato encontrado: despeja o conteúdo do popup para diagnóstico.
                var dump = string.Join(" | ", desc
                    .Where(e => !string.IsNullOrWhiteSpace(e.Name) ||
                                !string.IsNullOrWhiteSpace(e.Properties.AutomationId.ValueOrDefault))
                    .Select(e => $"'{e.Name}' ({e.ControlType}, Id={e.Properties.AutomationId.ValueOrDefault}, Enabled={e.IsEnabled})"));
                AppLogs.LogUpdaterDumpPopupSucesso(string.IsNullOrWhiteSpace(dump) ? "(nenhum elemento legível)" : dump);
            }
            catch (Exception ex) {
                AppLogs.LogUpdaterFalhaVarrerBotoesSucesso(ex.Message);
            }

            return null;
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Busca no desktop o popup de sucesso "PromobUpdate" (sem espaço) que contém
        /// um botão "Fechar". Retorna a janela e o botão, ou (null, null).
        /// </summary>
        /// <param name="automation">A instância de automação UIA.</param>
        /// <param name="hwndParaIgnorar">Handle opcional da janela principal do atualizador a ser ignorada.</param>
        //--------------------------------------------------------------------------------------
        private static (Window? janela, AutomationElement? btnFechar) BuscarPopupSucessoNoDesktop(UIA3Automation automation, IntPtr hwndParaIgnorar = default){
            // ETAPA 1: Tenta restaurar a janela caso esteja oculta (ícone em segundo plano)
            // O popup "PromobUpdate" pode estar invisível para o FlaUI.
            // Aceita tanto "PromobUpdate" quanto "Promob Update" desde que contenha o texto de sucesso.
            var hOculta = BuscarERestaurarJanelaOculta((titulo, hWnd) => {
                if (hwndParaIgnorar != IntPtr.Zero && hWnd == hwndParaIgnorar) return false;
                if (titulo.Contains("PromobUpdate", StringComparison.OrdinalIgnoreCase)) return true;
                if (titulo.Contains("Promob Update", StringComparison.OrdinalIgnoreCase)) {
                    return VerificarSeEhJanelaSucessoPorHwnd(hWnd, automation);
                }
                return false;
            });

            if (hOculta != IntPtr.Zero){
                AppLogs.LogUpdaterPopupSucessoOcultoRestaurado();
                InteractionHelper.EsperarUiRespirar(800);

                // Como a janela foi encontrada via EnumWindows, tentamos retornar direto pelo handle
                try {
                    var elDireto = automation.FromHandle(hOculta);
                    if (elDireto != null) {
                        var wDireto = elDireto.AsWindow();
                        var btnCheck = ObterBotaoFecharRobustamente(wDireto);
                        // Retorna o Window mesmo se btnCheck for null, permitindo fechar por coordenadas/teclado/kill
                        return (wDireto, btnCheck);
                    }
                }
                catch { }
            }

            // ETAPA 2: Scan via FlaUI (caso o acesso direto pelo HWND falhe ou não tenha sido encontrada janela oculta)
            try {
                var janelasDesktop = automation.GetDesktop().FindAllChildren();
                AppLogs.LogUpdaterIniciandoVarreduraJanelas();

                foreach (var child in janelasDesktop) {
                    if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                    
                    IntPtr childHwnd = IntPtr.Zero;
                    try { childHwnd = (IntPtr)child.Properties.NativeWindowHandle.ValueOrDefault; } catch { }
                    if (hwndParaIgnorar != IntPtr.Zero && childHwnd == hwndParaIgnorar) continue;

                    string nome = child.Name ?? "";
                    if (nome.Contains("Promob", StringComparison.OrdinalIgnoreCase)) {
                        AppLogs.LogUpdaterJanelaPromobLocalizadaVarredura(nome, childHwnd);
                    }

                    // Se a janela já for a mesma hOculta que verificamos anteriormente, ou se bate com os critérios de sucesso
                    bool ehJanelaSucesso = (hOculta != IntPtr.Zero && childHwnd == hOculta) || nome.Contains("PromobUpdate", StringComparison.OrdinalIgnoreCase);
                    if (!ehJanelaSucesso && nome.Contains("Promob Update", StringComparison.OrdinalIgnoreCase)) {
                        try {
                            var desc = child.FindAllDescendants();
                            ehJanelaSucesso = desc.Any(e => ContemTextoConclusaoSucesso(e.Name));
                        } catch { }
                    }

                    if (ehJanelaSucesso) {
                        var btnCheck = ObterBotaoFecharRobustamente(child);
                        // Retorna o Window mesmo se btnCheck for null
                        return (child.AsWindow(), btnCheck);
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
        /// <param name="automation">A instância de automação UIA (não utilizada devido ao uso de conexão local fresca).</param>
        /// <param name="hwndParaIgnorar">Handle opcional da janela principal do atualizador a ser ignorada.</param>
        //--------------------------------------------------------------------------------------
        private static void AguardarEFecharPopupSucesso(UIA3Automation automation, IntPtr hwndParaIgnorar = default){
            AppLogs.LogUpdaterAguardandoConclusaoInstalacao();
            
            // Usamos uma conexão local fresca com o UIA para evitar problemas com buffers COM corrompidos
            // após o encerramento do processo principal do atualizador
            using var localAutomation = new UIA3Automation();
            
            AutomationElement? btnFecharSucesso = null;
            Window? janelaSucesso = null;
            var swSucesso = Stopwatch.StartNew();
            const int timeoutSucessoMs = 600000; // 10 minutos
            
            while (swSucesso.ElapsedMilliseconds < timeoutSucessoMs) {
                var (janela, btn) = BuscarPopupSucessoNoDesktop(localAutomation, hwndParaIgnorar);
                if (janela != null) {
                    janelaSucesso = janela;
                    btnFecharSucesso = btn;
                    break;
                }

                if (((int)swSucesso.Elapsed.TotalSeconds) % 10 == 0) {
                    AppLogs.LogUpdaterAguardandoPopupConclusao(swSucesso.Elapsed.Minutes, swSucesso.Elapsed.Seconds);
                }

                Thread.Sleep(2000);
            }

            if (janelaSucesso == null) {
                throw new Exception("[SUCESSO] Timeout: O popup de conclusão da instalação não apareceu após 10 minutos.");
            }

            FinalizarEFecharPopupSucesso(janelaSucesso, btnFecharSucesso);
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Executa a sequência de cliques e atalhos de teclado para focar e
        /// acionar o botão "Fechar" da janela de sucesso "PromobUpdate".
        /// </summary>
        //--------------------------------------------------------------------------------------
        private static void FinalizarEFecharPopupSucesso(Window janelaSucesso, AutomationElement? btnFecharSucesso) {
            AppLogs.LogUpdaterPopupConclusaoDetectado(btnFecharSucesso?.Name ?? "Fechar");
            
            bool tentouCliqueCoordenada = false;
            bool fechadoPelaVisao = false;

            // 1. Tentar fechar usando a visão (VisionHelper) se estiver habilitado e disponível
            if (VisionHelper.Habilitado) {
                Logger.Log("  [SUCESSO] VisionHelper habilitado. Tentando localizar o botão 'Fechar' visualmente...", LogLevel.Info);
                try {
                    // Garante que a janela de sucesso esteja ativa e visível antes de tirar screenshot
                    InteractionHelper.AtivarJanela(janelaSucesso);
                    InteractionHelper.EsperarUiRespirar(600);

                    System.Drawing.Rectangle? areaBusca = null;
                    var rectJanela = janelaSucesso.BoundingRectangle;
                    if (!rectJanela.IsEmpty && rectJanela.Width > 100 && rectJanela.Height > 100) {
                        areaBusca = new System.Drawing.Rectangle((int)rectJanela.X, (int)rectJanela.Y, (int)rectJanela.Width, (int)rectJanela.Height);
                    }

                    // Localiza o botão "Fechar"
                    var pontoFechar = VisionHelper.LocalizarElemento("botão Fechar", maxTentativas: 3, intervaloMs: 1200, area: areaBusca);
                    if (pontoFechar.HasValue) {
                        Logger.Log($"  [SUCESSO] Botão 'Fechar' localizado visualmente em: X={pontoFechar.Value.X}, Y={pontoFechar.Value.Y}", LogLevel.Info);
                        Mouse.MoveTo(pontoFechar.Value.X, pontoFechar.Value.Y);
                        InteractionHelper.EsperarUiRespirar(250);
                        Mouse.Click();
                        InteractionHelper.EsperarUiRespirar(1000);
                        fechadoPelaVisao = true;
                    }
                    else {
                        Logger.Log("  [SUCESSO] Não foi possível localizar o botão 'Fechar' usando a visão.", LogLevel.Warn);
                    }
                }
                catch (Exception ex) {
                    Logger.Log($"  [AVISO] Erro ao tentar fechar visualmente: {ex.Message}", LogLevel.Warn);
                }
            }

            // Se a visão fechou o popup (ou se não estava habilitada / falhou), vamos verificar se a janela ainda está aberta
            bool precisaFallback = true;
            if (fechadoPelaVisao) {
                try {
                    if (!janelaSucesso.IsAvailable || janelaSucesso.Properties.IsOffscreen.ValueOrDefault) {
                        precisaFallback = false;
                        Logger.Log("  [SUCESSO] Janela de sucesso fechada com sucesso via visão.", LogLevel.Info);
                    }
                }
                catch {
                    precisaFallback = false; // Se a janela sumiu e deu erro ao consultar propriedades, está fechada.
                }
            }

            if (precisaFallback) {
                // 2. Se localizou o botão do UIA, tenta clicar com estratégias
                if (btnFecharSucesso != null) {
                    var rectBtn = btnFecharSucesso.BoundingRectangle;

                    if (rectBtn.IsEmpty) {
                        // Fallback: Se o retângulo do botão estiver vazio, tenta estimar as coordenadas usando o retângulo da janela
                        var rectJanela = janelaSucesso.BoundingRectangle;
                        if (!rectJanela.IsEmpty) {
                            int x = (int)(rectJanela.X + (rectJanela.Width * 0.72));
                            int y = (int)(rectJanela.Y + (rectJanela.Height * 0.82));
                            
                            Logger.Log($"  [SUCESSO] Retângulo do botão vazio. Tentando clique nas coordenadas estimadas da janela: X={x}, Y={y}", LogLevel.Info);
                            try {
                                InteractionHelper.AtivarJanela(janelaSucesso);
                                InteractionHelper.EsperarUiRespirar(300);
                                Mouse.MoveTo(x, y);
                                InteractionHelper.EsperarUiRespirar(250);
                                Mouse.Click();
                                tentouCliqueCoordenada = true;
                            }
                            catch (Exception ex) {
                                Logger.Log($"  [AVISO] Falha ao mover/clicar nas coordenadas estimadas: {ex.Message}", LogLevel.Warn);
                            }
                        }
                    }

                    if (!tentouCliqueCoordenada) {
                        InteractionHelper.ClicarComEstrategias(btnFecharSucesso, janelaSucesso, labelLog: "Fechar Popup Sucesso", apenasMouse: false);
                    }
                }

                // 3. Se o botão é nulo (por exemplo, UIA bloqueado), tenta clicar diretamente na coordenada estimada do botão
                if (btnFecharSucesso == null && !tentouCliqueCoordenada) {
                    var rectJanela = janelaSucesso.BoundingRectangle;
                    if (!rectJanela.IsEmpty) {
                        int x = (int)(rectJanela.X + (rectJanela.Width * 0.72));
                        int y = (int)(rectJanela.Y + (rectJanela.Height * 0.82));
                        
                        Logger.Log($"  [SUCESSO] Botão inacessível por UIA. Tentando clique físico nas coordenadas estimadas da janela: X={x}, Y={y}", LogLevel.Info);
                        try {
                            InteractionHelper.AtivarJanela(janelaSucesso);
                            InteractionHelper.EsperarUiRespirar(300);
                            Mouse.MoveTo(x, y);
                            InteractionHelper.EsperarUiRespirar(250);
                            Mouse.Click();
                            tentouCliqueCoordenada = true;
                        }
                        catch (Exception ex) {
                            Logger.Log($"  [AVISO] Falha ao mover/clicar nas coordenadas estimadas: {ex.Message}", LogLevel.Warn);
                        }
                    }
                }

                // 4. Fallback de Teclado (ENTER): se o popup ainda estiver na tela
                InteractionHelper.EsperarUiRespirar(1000);
                try {
                    if (janelaSucesso.IsAvailable && !janelaSucesso.Properties.IsOffscreen.ValueOrDefault) {
                        Logger.Log("  [AVISO] Janela de sucesso ainda está aberta. Tentando focar e enviar ENTER...", LogLevel.Warn);
                        InteractionHelper.AtivarJanela(janelaSucesso);
                        InteractionHelper.EsperarUiRespirar(300);
                        Keyboard.Type(VirtualKeyShort.ENTER);
                        InteractionHelper.EsperarUiRespirar(1000);
                    }
                }
                catch { }

                // 5. Fallback de Encerramento Forçado (Kill Process): se a janela ainda persistir aberta
                try {
                    if (janelaSucesso.IsAvailable) {
                        int pid = janelaSucesso.Properties.ProcessId.ValueOrDefault;
                        if (pid > 0) {
                            Logger.Log($"  [ATUALIZAÇÃO] Janela de sucesso ainda aberta. Encerrando processo do atualizador de forma forçada (PID: {pid})...", LogLevel.Info);
                            var proc = Process.GetProcessById(pid);
                            proc.Kill();
                            InteractionHelper.EsperarUiRespirar(1000);
                        }
                    }
                }
                catch (Exception ex) {
                    Logger.Log($"  [AVISO] Falha ao encerrar o processo pelo PID: {ex.Message}", LogLevel.Warn);
                }
            }

            AppLogs.LogUpdaterConcluidaSucesso();
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Verifica se o popup de conclusão da atualização ("PromobUpdate") está aberto no desktop
        /// e, se estiver, faz o fechamento automático.
        /// </summary>
        //--------------------------------------------------------------------------------------
        public static bool VerificarEFecharPopupSucessoSeAberto(UIA3Automation automation) {
            try {
                var (janela, btn) = BuscarPopupSucessoNoDesktop(automation);
                if (janela != null) {
                    AppLogs.LogUpdaterPopupUpdateAbertoDesktop();
                    FinalizarEFecharPopupSucesso(janela, btn);
                    return true;
                }
            }
            catch (Exception ex) {
                AppLogs.LogUpdaterFalhaVerificarFecharPopupSucessoInicial(ex.Message);
            }
            return false;
        }

    }
}