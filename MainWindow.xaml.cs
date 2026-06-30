using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Diagnostics;
using Microsoft.Win32;
using PromobAutomacao.Utils;
using PromobAutomacao.Promob;
using PromobAutomacao.Automation;
using PromobAutomacao.Network;
using FlaUI.UIA3;
using FlaUIWindow = FlaUI.Core.AutomationElements.Window;

namespace PromobAutomacao{
    public partial class MainWindow : Window{
        private bool _isMonitoring = false;
        private CancellationTokenSource? _cts;
        private Task? _automationTask;
        private CancellationTokenSource? _ctsUpdateMonitor;
        private Task? _updateMonitorTask;

        private int _processadosCount = 0;
        private int _errosCount = 0;
        private System.Windows.Threading.DispatcherTimer? _statusTimer;

        // ==========================================
        // --- Campos de Rede ---
        // ==========================================
        private PromobServer? _server;
        private PromobClient? _promobClient;
        private System.Windows.Threading.DispatcherTimer? _metricsTimer;

        /// <summary>
        /// Armazena o estado do Promob reportado pelo servidor (apenas em Modo Cliente).
        /// Usado para determinar qual comando enviar ao clicar no botão Abrir/Fechar Promob.
        /// </summary>
        private bool _promobRunningOnServer = false;

        public MainWindow(){
            InitializeComponent();

            // Exibir caminhos das pastas configuradas
            txtPastaMonitorada.Text = Path.GetFileName(PromobConfig.PastaPromob) ?? "promob";
            txtPastaMonitorada.ToolTip = PromobConfig.PastaPromob;

            txtPastaXml.Text = Path.GetFileName(PromobConfig.PastaXml) ?? "xml";
            txtPastaXml.ToolTip = PromobConfig.PastaXml;

            // Inscreve a interface no evento de logs (sempre, inclusive para mensagens locais do cliente)
            Logger.OnLog += LogToTerminal;

            AppLogs.LogMainWindowPanelReady();

            // Initialize the networking mode according to selection in StartupWindow
            InitializeNetworking();

            // Timer de monitoramento do Promob — apenas em modo Local ou Servidor
            // (em modo Cliente, o estado do Promob vem das mensagens METRICS do servidor)
            if (AppMode.Mode != AppRunMode.Client){
                _statusTimer = new System.Windows.Threading.DispatcherTimer();
                _statusTimer.Interval = TimeSpan.FromSeconds(1.5);
                _statusTimer.Tick += StatusTimer_Tick;
                _statusTimer.Start();
                AtualizarBotaoIniciar();
            }

            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e){
            if (AppMode.Mode == AppRunMode.Client && AppMode.IsSpectator){
                cardOperacoes.Visibility = Visibility.Collapsed;
            } else {
                cardOperacoes.Visibility = Visibility.Visible;
            }
        }

        protected override void OnClosed(EventArgs e){
            _statusTimer?.Stop();
            _metricsTimer?.Stop();
            _server?.Dispose();
            _promobClient?.Dispose();

            if (_isMonitoring){
                _cts?.Cancel();
            }
            Logger.OnLog -= LogToTerminal;
            base.OnClosed(e);
        }

        // ==========================================
        // --- EVENT HANDLERS ---
        // ==========================================

        private void BtnToggleAutomacao_Click(object sender, RoutedEventArgs e){
            // Modo Cliente: envia comando ao servidor em vez de executar localmente
            if (AppMode.Mode == AppRunMode.Client){
                if (!_isMonitoring){
                    _promobClient?.Send(WsMessage.CreateCommand("START_AUTOMATION"));
                } else{
                    _promobClient?.Send(WsMessage.CreateCommand("STOP_AUTOMATION"));
                }
                return;
            }

            // Modo Local / Servidor: execução direta
            if (!_isMonitoring){
                StartMonitoring();
            } else{
                StopMonitoring();
            }
        }

        private void BtnAbrirPromob_Click(object sender, RoutedEventArgs e){
            // Modo Cliente: envia comando ao servidor
            if (AppMode.Mode == AppRunMode.Client){
                if (_promobRunningOnServer){
                    _promobClient?.Send(WsMessage.CreateCommand("CLOSE_PROMOB"));
                } else{
                    _promobClient?.Send(WsMessage.CreateCommand("OPEN_PROMOB"));
                }
                return;
            }

            bool isFechar = btnAbrirPromob.Content.ToString() == "Fechar Promob";

            if (isFechar){
                var result = MessageBox.Show(
                    "Tem certeza de que deseja fechar o Promob? Todos os dados não salvos serão perdidos.",
                    "Confirmar Fechamento",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.Yes){
                    btnAbrirPromob.IsEnabled = false;
                    try{
                        AppLogs.LogMainWindowClosingPromobByUser();

                        var currentProcId = Process.GetCurrentProcess().Id;
                        var processos = Process.GetProcesses()
                            .Where(p => p.Id != currentProcId &&
                                         p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                                         !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase) &&
                                         !p.ProcessName.Contains("Automacao", StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        foreach (var p in processos){
                            try{
                                p.Kill();
                                p.WaitForExit(1000);
                            }
                            catch { }
                        }

                        AppLogs.LogMainWindowProcessesClosedSuccess();
                    }
                    catch (Exception ex){
                        AppLogs.LogMainWindowClosingPromobError(ex.Message);
                    }
                    finally{
                        bool promobAberto = IsPromobRunning();
                        AtualizarEstadoBotaoPromob(promobAberto);
                    }
                }
                return;
            }

            btnAbrirPromob.IsEnabled = false;
            try{
                // 1. Verifica se já está em execução
                var currentProcId = Process.GetCurrentProcess().Id;
                var promobProc = Process.GetProcesses()
                    .FirstOrDefault(p => p.Id != currentProcId &&
                                         p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                                         !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase) &&
                                         !p.ProcessName.Contains("Automacao", StringComparison.OrdinalIgnoreCase));

                if (promobProc != null){
                    AppLogs.LogMainWindowPromobAlreadyRunning(promobProc.Id);
                    try{
                        using var automation = new UIA3Automation();
                        var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 2000);
                        if (janela != null){
                            InteractionHelper.AtivarJanela(janela);
                        }
                    }
                    catch{
                        var handle = promobProc.MainWindowHandle;
                        if (handle != IntPtr.Zero){
                            InteractionHelper.EsperarUiRespirar(200);
                        }
                    }
                    return;
                }

                // 2. Tenta localizar o executável
                string? caminhoExe = DetectarPromobExe();

                if (string.IsNullOrEmpty(caminhoExe)){
                    var dialog = new OpenFileDialog{
                        Title = "Selecione o Executável do Promob (Promob.exe)",
                        Filter = "Executável do Promob (*.exe)|*.exe;*.lnk",
                        FileName = "Promob5.exe"
                    };

                    if (dialog.ShowDialog() == true){
                        caminhoExe = dialog.FileName;
                        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "promob_path.txt");
                        File.WriteAllText(configPath, caminhoExe);
                    }
                }

                if (!string.IsNullOrEmpty(caminhoExe) && File.Exists(caminhoExe)){
                    AppLogs.LogMainWindowStartingPromob();
                    var info = new ProcessStartInfo{
                        FileName = caminhoExe,
                        WorkingDirectory = Path.GetDirectoryName(caminhoExe) ?? "",
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                    info.EnvironmentVariables["__COMPAT_LAYER"] = "RunAsInvoker";
                    Process.Start(info);
                }
                else{
                    AppLogs.LogMainWindowPromobExeNotFound();
                }
            }
            catch (Exception ex){
                AppLogs.LogMainWindowStartingPromobError(ex.Message);
            }
            finally{
                bool promobAberto = IsPromobRunning();
                AtualizarEstadoBotaoPromob(promobAberto);
            }
        }

        private void BtnAtualizarPromob_Click(object sender, RoutedEventArgs e){
            if (AppMode.Mode == AppRunMode.Client){
                _promobClient?.Send(WsMessage.CreateCommand("UPDATE_PROMOB"));
                // Bloqueia botões imediatamente no cliente
                btnAtualizarPromob.IsEnabled = false;
                btnToggleAutomacao.IsEnabled = false;
                btnAbrirPromob.IsEnabled = false;
                return;
            }

            // Local or Server mode:
            AutomacaoEstado.AtualizacaoEmAndamento = true;
            btnAtualizarPromob.IsEnabled = false;
            btnToggleAutomacao.IsEnabled = false;
            btnAbrirPromob.IsEnabled = false;
            if (AppMode.Mode == AppRunMode.Server) {
                BroadcastMetrics();
            }

            Task.Run(() => {
                try{
                    using var automation = new UIA3Automation();
                    
                    // Verifica e ativa a interface 'Novo Promob' caso o checkbox esteja desativado
                    PromobWindowHelper.VerificarEAtivarNovoPromob(automation);

                    PromobUpdater.ExecutarAtualizacao(automation);
                    AppLogs.LogUpdaterConcluidaSucesso();
                }
                catch (Exception ex){
                    AppLogs.LogMainWindowUpdateExecutionError(ex.Message);
                }
                finally{
                    AutomacaoEstado.AtualizacaoEmAndamento = false;
                    if (AppMode.Mode == AppRunMode.Server) {
                        BroadcastMetrics();
                    }
                    Dispatcher.Invoke(() => {
                        AtualizarBotaoIniciar();
                    });
                }
            });
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e){
            txtLogTerminal.Clear();
        }

        // ==========================================
        // --- LOGIC FUNCTIONS ---
        // ==========================================

        private void StartMonitoring(){
            _isMonitoring = true;
            _cts = new CancellationTokenSource();

            txtStatusText.Text = "Monitorando...";
            txtStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            if (statusIndicator.Effect is DropShadowEffect shadow){
                shadow.Color = Color.FromRgb(16, 185, 129);
            }

            btnToggleAutomacao.Content = "Parar Automação";
            btnToggleAutomacao.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            btnAbrirPromob.IsEnabled = false;

            AppLogs.LogMainWindowAutomationStarted();

            _automationTask = Task.Run(() => ExecutarLoopAutomacao(_cts.Token), _cts.Token);

            // Inicia o monitor de janela Promob Update em background
            _ctsUpdateMonitor = new CancellationTokenSource();
            _updateMonitorTask = Task.Run(() => MonitorarJanelaUpdate(_ctsUpdateMonitor.Token));
        }

        private void StopMonitoring(){
            btnToggleAutomacao.IsEnabled = false;
            btnToggleAutomacao.Content = "Parando...";
            AppLogs.LogMainWindowStoppingAutomationRequested();
            _cts?.Cancel();
            _ctsUpdateMonitor?.Cancel();
        }

        private void ExecutarLoopAutomacao(CancellationToken token){
            VisionHelper.Inicializar();

            if (!Directory.Exists(PromobConfig.PastaPromob)){
                AppLogs.LogMainWindowDesktopFolderNotFound(PromobConfig.PastaPromob);
                ResetUiStateOnStop();
                return;
            }

            Directory.CreateDirectory(PromobConfig.PastaXml);
            Directory.CreateDirectory(PromobConfig.PastaPromobErro);

            using var automation = new UIA3Automation();

            // Verifica e ativa a interface 'Novo Promob' caso o checkbox esteja desativado
            PromobWindowHelper.VerificarEAtivarNovoPromob(automation);

            // Verifica e fecha o popup de conclusão do update ('PromobUpdate') se estiver aberto antes de começar
            try {
                PromobUpdater.VerificarEFecharPopupSucessoSeAberto(automation);
            }
            catch (Exception ex) {
                AppLogs.LogMainWindowFalhaVerificarPopupInicial(ex.Message);
            }

            using var fileAddedEvent = new AutoResetEvent(true);

            using var watcher = new FileSystemWatcher(PromobConfig.PastaPromob, "*.promob"){
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            watcher.Created += (s, e) => { try { fileAddedEvent.Set(); } catch { } };
            watcher.Renamed += (s, e) => { try { fileAddedEvent.Set(); } catch { } };

            bool loggedWaiting = false;

            while (!token.IsCancellationRequested){
                var arquivos = Directory.GetFiles(PromobConfig.PastaPromob, "*.promob")
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (arquivos.Count == 0){
                    if (!loggedWaiting){
                        AppLogs.LogMainWindowWaitingForFiles();
                        loggedWaiting = true;
                    }

                    WaitHandle.WaitAny(new[] { fileAddedEvent, token.WaitHandle });
                    continue;
                }

                loggedWaiting = false;

                foreach (var arquivo in arquivos){
                    if (token.IsCancellationRequested)
                        break;

                    // Se há atualização em andamento, aguarda terminar antes de pegar o arquivo!
                    if (AutomacaoEstado.AtualizacaoEmAndamento) {
                        AppLogs.LogMainWindowPausandoProcessamentoAtualizacao();
                        while (AutomacaoEstado.AtualizacaoEmAndamento && !token.IsCancellationRequested) {
                            Thread.Sleep(2000);
                        }
                        if (token.IsCancellationRequested)
                            break;
                        AppLogs.LogMainWindowAtualizacaoFinalizadaRetomando();
                    }

                    var nome = Path.GetFileName(arquivo);

                    AppLogs.LogMainWindowStartingProcessingFile(nome);

                    try{
                        Thread.Sleep(500);

                        AutomacaoEstado.ArquivoEmProcessamento = true;
                        try{
                            Diagnostics.Medir("Processar arquivo", () => PromobWorkflow.ProcessarArquivo(automation, arquivo, token));
                        }
                        finally{
                            AutomacaoEstado.ArquivoEmProcessamento = false;
                        }

                        _processadosCount++;
                        UpdateMetricsOnUi();

                        AppLogs.LogMainWindowProcessingSuccess(nome);

                        try{
                            File.Delete(arquivo);
                        }
                        catch (Exception exDel){
                            AppLogs.LogMainWindowDeleteFileWarning(nome, exDel.Message);
                        }
                    }
                    catch (PromobExportException exErp){
                        _errosCount++;
                        UpdateMetricsOnUi();

                        AppLogs.LogMainWindowExportFailure(nome, exErp.Message);
                        Logger.RegistrarErro(nome, exErp);

                        try{
                            var destino = Path.Combine(PromobConfig.PastaPromobErro, nome);
                            if (File.Exists(destino)){
                                var semExtensao = Path.GetFileNameWithoutExtension(nome);
                                var extensao = Path.GetExtension(nome);
                                destino = Path.Combine(PromobConfig.PastaPromobErro, $"{semExtensao}_{DateTime.Now:yyyyMMdd_HHmmss}{extensao}");
                            }
                            File.Move(arquivo, destino);
                            AppLogs.LogMainWindowFileMovedToErrorFolder(Path.GetFileName(PromobConfig.PastaPromobErro));
                        }
                        catch (Exception exMove){
                            AppLogs.LogMainWindowMoveToErrorFolderWarning(nome, exMove.Message);
                        }
                    }
                    catch (OperationCanceledException){
                        break;
                    }
                    catch (Exception ex){
                        // Intercepta cancelamento com máxima robustez
                        if (token.IsCancellationRequested ||
                            ex is OperationCanceledException ||
                            ex.InnerException is OperationCanceledException ||
                            (ex is AggregateException ae && ae.InnerExceptions.Any(e => e is OperationCanceledException))){

                            break;
                        }

                        _errosCount++;
                        UpdateMetricsOnUi();

                        AppLogs.LogMainWindowProcessingError(nome, ex.Message);
                        Logger.RegistrarErro(nome, ex);

                        try{
                            PromobWorkflow.TentarRecuperar(automation);
                        }
                        catch { }

                        AppLogs.LogMainWindowFileKeptForReprocessing(nome);
                    }
                }
            }

            AutomacaoEstado.ArquivoEmProcessamento = false;
            ResetUiStateOnStop();
        }

        //--------------------------------------------------------------------------------------
        /// <summary>
        /// Thread em background que monitora continuamente o desktop em busca da janela
        /// "Promob Update" aberta automaticamente pelo Promob.
        /// Quando detectada, aguarda o momento seguro (sem arquivo em processamento ou após
        /// o passo 9/9) e aciona a atualização.
        /// </summary>
        //--------------------------------------------------------------------------------------
        private void MonitorarJanelaUpdate(CancellationToken token){

            while (!token.IsCancellationRequested){
                try{
                    // Verifica a cada 3 segundos
                    token.WaitHandle.WaitOne(3000);
                    if (token.IsCancellationRequested) break;

                    // Busca a janela "Promob Update" aberta no desktop
                    using var automation = new UIA3Automation();
                    var desktopChildren = automation.GetDesktop().FindAllChildren();
                    FlaUIWindow? janelaUpdate = null;
                    foreach (var child in desktopChildren){
                        if (child.ControlType != FlaUI.Core.Definitions.ControlType.Window) continue;
                        var w = new FlaUIWindow(child.FrameworkAutomationElement);
                        if ((w?.Title ?? "").Contains("Promob Update", StringComparison.OrdinalIgnoreCase)){
                            janelaUpdate = w;
                            break;
                        }
                    }

                    if (janelaUpdate == null)
                        continue;

                    AppLogs.LogMainWindowUpdateWindowDetected();

                    // Ativa a trava de atualização para evitar que a automação principal inicie novos arquivos
                    AutomacaoEstado.AtualizacaoEmAndamento = true;
                    Dispatcher.Invoke(() => {
                        AtualizarBotaoIniciar();
                    });
                    if (AppMode.Mode == AppRunMode.Server) {
                        BroadcastMetrics();
                    }

                    try{
                        // Se há um arquivo em processamento, aguarda o projeto ser fechado (passo 9/9)
                        if (AutomacaoEstado.ArquivoEmProcessamento){
                            AppLogs.LogMainWindowProjectInProgressAwaitingClose();

                            const int timeoutEsperaMs = 10 * 60 * 1000; // 10 minutos de timeout
                            var sw = System.Diagnostics.Stopwatch.StartNew();

                            while (!AutomacaoEstado.FechouProjetoAtual && sw.ElapsedMilliseconds < timeoutEsperaMs){
                                if (token.IsCancellationRequested) return;
                                Thread.Sleep(500);
                            }

                            if (!AutomacaoEstado.FechouProjetoAtual){
                                AppLogs.LogMainWindowTimeoutAwaitingClose();
                                continue;
                            }

                            AppLogs.LogMainWindowProjectFinishedStartingUpdateCheck();
                        }
                        else{
                            AppLogs.LogMainWindowStartingUpdateCheckDirectly();
                        }

                        // Executa a atualização (o método re-localiza a janela internamente)
                        bool sucesso = false;
                        try{
                            PromobUpdater.ExecutarAtualizacaoUpdate(automation);
                            sucesso = true;
                        }
                        catch (Exception exUpdate){
                            AppLogs.LogMainWindowUpdateExecutionError(exUpdate.Message);
                        }

                        if (sucesso){
                            AppLogs.LogUpdaterConcluidaSucesso();
                        }
                    }
                    finally{
                        // Sempre garante a liberação da trava de atualização ao concluir ou falhar
                        AutomacaoEstado.AtualizacaoEmAndamento = false;
                        Dispatcher.Invoke(() => {
                            AtualizarBotaoIniciar();
                        });
                        if (AppMode.Mode == AppRunMode.Server) {
                            BroadcastMetrics();
                        }
                    }
                }
                catch (OperationCanceledException){
                    break;
                }
                catch (Exception ex){
                    AppLogs.LogMainWindowUpdateMonitorError(ex.Message);
                }
            }

            AppLogs.LogMainWindowUpdateMonitorFinished();
        }

        private void UpdateMetricsOnUi(){
            Dispatcher.Invoke(() => {
                txtSucessosCount.Text = _processadosCount.ToString();
                txtErrosCount.Text = _errosCount.ToString();
            });

            // Em modo Servidor, transmite métricas atualizadas aos clientes imediatamente
            if (AppMode.Mode == AppRunMode.Server){
                BroadcastMetrics();
            }
        }

        private void ResetUiStateOnStop(){
            Dispatcher.Invoke(() => {
                _isMonitoring = false;

                txtStatusText.Text = "Parado";
                txtStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                if (statusIndicator.Effect is DropShadowEffect shadow) {
                    shadow.Color = Color.FromRgb(239, 68, 68);
                }

                btnToggleAutomacao.Content = "Iniciar Automação";
                btnToggleAutomacao.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));

                bool promobRunning = IsPromobRunning();
                btnToggleAutomacao.IsEnabled = promobRunning;
                AtualizarEstadoBotaoPromob(promobRunning);
                btnAtualizarPromob.IsEnabled = promobRunning;

                AppLogs.LogMainWindowAutomationStopped();
            });
        }

        // Helper to output to the logs textbox
        private void LogToTerminal(string message, LogLevel level){
            Dispatcher.Invoke(() => {
                string prefix = level switch{
                    LogLevel.Error => "[ERRO] ",
                    LogLevel.Warn  => "[AVISO] ",
                    LogLevel.Debug => "[DEBUG] ",
                    _ => ""
                };

                // Remove prefixos duplicados se a própria mensagem já começar com eles
                string cleanMessage = message;
                if (!string.IsNullOrEmpty(prefix) && message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)){
                    cleanMessage = message.Substring(prefix.Length);
                }

                string formattedTime = DateTime.Now.ToString("HH:mm:ss");
                txtLogTerminal.AppendText($"[{formattedTime}] {prefix}{cleanMessage}{Environment.NewLine}");
                txtLogTerminal.ScrollToEnd();
            });
        }

        // Tenta detectar o executável do Promob no sistema
        private string? DetectarPromobExe(){
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "promob_path.txt");

            if (File.Exists(configPath)){
                var path = File.ReadAllText(configPath).Trim();
                if (File.Exists(path)) return path;
            }

            string[] raizes = {
                @"C:\Program Files\Promob",
                @"C:\Program Files (x86)\Promob"
            };

            foreach (var raiz in raizes){
                if (Directory.Exists(raiz)){
                    try{
                        var arquivos = Directory.GetFiles(raiz, "Promob.exe", SearchOption.AllDirectories);
                        if (arquivos.Length == 0){
                            arquivos = Directory.GetFiles(raiz, "Promob5.exe", SearchOption.AllDirectories);
                        }

                        if (arquivos.Length > 0){
                            File.WriteAllText(configPath, arquivos[0]);
                            return arquivos[0];
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        // ==========================================
        // --- PROMOB MONITORING LOGIC ---
        // ==========================================

        private async void StatusTimer_Tick(object? sender, EventArgs e){
            if (AutomacaoEstado.AtualizacaoEmAndamento){
                btnAbrirPromob.IsEnabled = false;
                btnToggleAutomacao.IsEnabled = false;
                btnAtualizarPromob.IsEnabled = false;
                return;
            }

            bool promobAberto = await Task.Run(() => IsPromobRunning());

            if (AutomacaoEstado.AtualizacaoEmAndamento){
                btnAbrirPromob.IsEnabled = false;
                btnToggleAutomacao.IsEnabled = false;
                btnAtualizarPromob.IsEnabled = false;
                return;
            }

            Dispatcher.Invoke(() => {
                AtualizarEstadoBotaoPromob(promobAberto);
            });

            if (_isMonitoring){
                bool isStopping = _cts?.IsCancellationRequested ?? false;
                btnToggleAutomacao.IsEnabled = !isStopping;
                btnAtualizarPromob.IsEnabled = false;
                return;
            }

            if (!_isMonitoring){
                btnToggleAutomacao.IsEnabled = promobAberto;
                btnAtualizarPromob.IsEnabled = promobAberto;
            }
        }

        private void AtualizarBotaoIniciar(){
            if (AutomacaoEstado.AtualizacaoEmAndamento){
                btnAbrirPromob.IsEnabled = false;
                btnToggleAutomacao.IsEnabled = false;
                btnAtualizarPromob.IsEnabled = false;
                return;
            }

            bool promobAberto = IsPromobRunning();
            AtualizarEstadoBotaoPromob(promobAberto);

            if (_isMonitoring){
                bool isStopping = _cts?.IsCancellationRequested ?? false;
                btnToggleAutomacao.IsEnabled = !isStopping;
                btnAtualizarPromob.IsEnabled = false;
                return;
            }

            btnToggleAutomacao.IsEnabled = promobAberto;
            btnAtualizarPromob.IsEnabled = promobAberto;
        }

        private void AtualizarEstadoBotaoPromob(bool promobAberto){
            if (promobAberto){
                btnAbrirPromob.Content = "Fechar Promob";
                btnAbrirPromob.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            else{
                btnAbrirPromob.Content = "Abrir Promob";
                btnAbrirPromob.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            }

            if (AutomacaoEstado.AtualizacaoEmAndamento){
                btnAbrirPromob.IsEnabled = false;
                return;
            }

            if (!_isMonitoring){
                btnAbrirPromob.IsEnabled = true;
            }
            else{
                btnAbrirPromob.IsEnabled = false;
            }
        }

        private bool IsPromobRunning(){
            try{
                var currentProcId = Process.GetCurrentProcess().Id;
                return Process.GetProcesses()
                    .Any(p => p.Id != currentProcId &&
                              p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                              !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase) &&
                              !p.ProcessName.Contains("Automacao", StringComparison.OrdinalIgnoreCase));
            }
            catch{
                return false;
            }
        }

        // ==========================================
        // --- NETWORKING ---
        // ==========================================

        private void InitializeNetworking(){
            switch (AppMode.Mode){
                case AppRunMode.Server: InitServer(); break;
                case AppRunMode.Client: InitClient(); break;
                default: UpdateNetworkStatus(); break;
            }
        }

        private void InitServer(){
            _server = new PromobServer(AppMode.Port);

            // Transmite todos os logs em tempo real para os clientes conectados
            Logger.OnLog += (msg, level) => {
                _ = Task.Run(() => _server?.Broadcast(WsMessage.CreateLog(msg, level.ToString())));
            };

            // Processa comandos recebidos dos clientes remotos
            _server.OnCommandReceived += HandleServerCommand;

            // Atualiza o badge de rede quando um cliente conecta ou desconecta
            _server.OnClientCountChanged += () => Dispatcher.InvokeAsync(() => UpdateNetworkStatus());

            _server.Start();

            // Timer para enviar métricas periódicas aos clientes (a cada 2 segundos)
            _metricsTimer = new System.Windows.Threading.DispatcherTimer();
            _metricsTimer.Interval = TimeSpan.FromSeconds(2);
            _metricsTimer.Tick += (s, e) => {
                BroadcastMetrics();
                UpdateNetworkStatus();
            };
            _metricsTimer.Start();

            AppLogs.LogMainWindowServerActive(AppMode.Port);
            UpdateNetworkStatus();
        }

        private void InitClient(){
            _promobClient = new PromobClient();
            _promobClient.OnMessage     += HandleClientMessage;
            _promobClient.OnDisconnected += HandleClientDisconnected;

            // Desabilita botões até a conexão ser estabelecida
            btnToggleAutomacao.IsEnabled = false;
            btnAbrirPromob.IsEnabled     = false;

            AppLogs.LogMainWindowClientConnecting(AppMode.ServerHost, AppMode.Port);
            UpdateNetworkStatus("Conectando...", false);

            _ = ConnectClientAsync();
        }

        private async Task ConnectClientAsync(){
            var success = await _promobClient!.ConnectAsync(AppMode.ServerHost, AppMode.Port);

            Dispatcher.Invoke(() => {
                if (success){
                    AppLogs.LogMainWindowClientConnected();
                    UpdateNetworkStatus("Cliente Conectado", true);
                    btnToggleAutomacao.IsEnabled = true;
                    btnAbrirPromob.IsEnabled     = true;
                } else{
                    AppLogs.LogMainWindowClientConnectionFailed(AppMode.ServerHost, AppMode.Port);
                    UpdateNetworkStatus("Falha na Conexão", false);
                }
            });
        }

        private void HandleServerCommand(string action){
            Dispatcher.Invoke(() => {
                switch (action){
                    case "START_AUTOMATION":
                        if (!_isMonitoring) StartMonitoring();
                        break;
                    case "STOP_AUTOMATION":
                        if (_isMonitoring) StopMonitoring();
                        break;
                    case "OPEN_PROMOB":
                        var exe = DetectarPromobExe();
                        if (!string.IsNullOrEmpty(exe) && File.Exists(exe)){
                            try{
                                var info = new ProcessStartInfo{
                                    FileName = exe,
                                    WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
                                    UseShellExecute  = false,
                                    WindowStyle      = ProcessWindowStyle.Normal
                                };
                                info.EnvironmentVariables["__COMPAT_LAYER"] = "RunAsInvoker";
                                Process.Start(info);
                                AppLogs.LogMainWindowRemotePromobStarted();
                            }
                            catch (Exception ex){
                                AppLogs.LogMainWindowRemoteStartError(ex.Message);
                            }
                        } else{
                            AppLogs.LogMainWindowRemoteStartExeNotFound();
                        }
                        break;
                    case "CLOSE_PROMOB":
                        ForceClosePromob();
                        break;
                    case "UPDATE_PROMOB":
                        if (!_isMonitoring){
                            BtnAtualizarPromob_Click(this, new RoutedEventArgs());
                        }
                        break;
                }
            });
        }

        private void HandleClientMessage(WsMessage msg){
            Dispatcher.Invoke(() => {
                switch (msg.Type){
                    case MessageType.Log:
                        var level = msg.Level switch{
                            "Error" => LogLevel.Error,
                            "Warn"  => LogLevel.Warn,
                            "Debug" => LogLevel.Debug,
                            _       => LogLevel.Info
                        };
                        // Append direto (já estamos no Dispatcher thread)
                        AppendLogLine(msg.Text ?? "", level);

                        // Alerta sonoro para erros recebidos do servidor
                        if (level == LogLevel.Error){
                            SystemSounds.Exclamation.Play();
                        }
                        break;

                    case MessageType.Metrics:
                        _processadosCount      = msg.Sucessos;
                        _errosCount            = msg.Erros;
                        _promobRunningOnServer = msg.PromobRunning;

                        txtSucessosCount.Text = msg.Sucessos.ToString();
                        txtErrosCount.Text    = msg.Erros.ToString();

                        // Sincroniza estado da automação com o servidor
                        bool nowMonitoring = msg.Status == "Monitorando";
                        if (nowMonitoring != _isMonitoring){
                            _isMonitoring = nowMonitoring;

                            if (_isMonitoring){
                                txtStatusText.Text = "Monitorando...";
                                txtStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                                if (statusIndicator.Effect is DropShadowEffect shadowOn) shadowOn.Color = Color.FromRgb(16, 185, 129);
                                btnToggleAutomacao.Content    = "Parar Automação";
                                btnToggleAutomacao.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                            } else{
                                txtStatusText.Text = "Parado";
                                txtStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                                if (statusIndicator.Effect is DropShadowEffect shadowOff) shadowOff.Color = Color.FromRgb(239, 68, 68);
                                btnToggleAutomacao.Content    = "Iniciar Automação";
                                btnToggleAutomacao.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                            }
                        }

                        if (msg.Updating) {
                            btnToggleAutomacao.IsEnabled = false;
                            btnAbrirPromob.IsEnabled     = false;
                            btnAtualizarPromob.IsEnabled = false;
                        } else {
                            // Atualiza o botão Abrir/Fechar Promob e a disponibilidade do toggle
                            AtualizarEstadoBotaoPromob(msg.PromobRunning);
                            btnToggleAutomacao.IsEnabled = msg.PromobRunning || _isMonitoring;
                            btnAtualizarPromob.IsEnabled = msg.PromobRunning && !_isMonitoring;
                        }
                        break;
                }
            });
        }

        private void HandleClientDisconnected(){
            Dispatcher.Invoke(() => {
                AppLogs.LogMainWindowServerConnectionLost();
                UpdateNetworkStatus("Desconectado do Servidor", false);
                _isMonitoring = false;
                btnToggleAutomacao.IsEnabled = false;
                btnAbrirPromob.IsEnabled     = false;
                txtStatusText.Text = "Parado";
                txtStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                statusIndicator.Fill     = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                SystemSounds.Exclamation.Play();
            });
        }

        private void BroadcastMetrics(){
            if (_server == null) return;
            var status        = _isMonitoring ? "Monitorando" : "Parado";
            var processados   = _processadosCount;
            var erros         = _errosCount;
            _ = Task.Run(() => {
                var promobRunning = IsPromobRunning();
                var updating = AutomacaoEstado.AtualizacaoEmAndamento;
                _server.Broadcast(WsMessage.CreateMetrics(processados, erros, status, promobRunning, updating));
            });
        }

        private void UpdateNetworkStatus(string? customText = null, bool? connected = null){
            string text;
            SolidColorBrush color;

            switch (AppMode.Mode){
                case AppRunMode.Server:
                    var count = _server?.ClientCount ?? 0;
                    text = count == 0
                        ? "● Servidor Ativo | Sem clientes"
                        : $"● Servidor Ativo | {count} cliente{(count == 1 ? "" : "s")}";
                    color = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    break;

                case AppRunMode.Client:
                    if (customText != null){
                        text  = $"● {customText}";
                        color = connected == true
                            ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
                            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    } else{
                        bool isConn = _promobClient?.IsConnected ?? false;
                        string role = AppMode.IsSpectator ? "Espectador" : "Operador";
                        text  = isConn ? $"● Cliente ({role}) → {AppMode.ServerHost}:{AppMode.Port}" : "● Desconectado";
                        color = isConn
                            ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
                            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                    break;

                default:
                    text  = "● Modo Local";
                    color = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    break;
            }

            txtNetworkStatus.Text       = text;
            txtNetworkStatus.Foreground = color;
        }

        private void ForceClosePromob(){
            try{
                var currentProcId = Process.GetCurrentProcess().Id;
                var processos = Process.GetProcesses()
                    .Where(p => p.Id != currentProcId &&
                               p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                               !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase) &&
                               !p.ProcessName.Contains("Automacao", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var p in processos){
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }

                AppLogs.LogMainWindowRemotePromobClosed();
            }
            catch (Exception ex){
                AppLogs.LogMainWindowRemoteCloseError(ex.Message);
            }
        }

        // Versão interna de LogToTerminal sem Dispatcher.Invoke (para chamar quando já estiver no UI thread)
        private void AppendLogLine(string message, LogLevel level){
            string prefix = level switch{
                LogLevel.Error => "[ERRO] ",
                LogLevel.Warn  => "[AVISO] ",
                LogLevel.Debug => "[DEBUG] ",
                _ => ""
            };

            string cleanMessage = message;
            if (!string.IsNullOrEmpty(prefix) && message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)){
                cleanMessage = message.Substring(prefix.Length);
            }

            string formattedTime = DateTime.Now.ToString("HH:mm:ss");
            txtLogTerminal.AppendText($"[{formattedTime}] {prefix}{cleanMessage}{Environment.NewLine}");
            txtLogTerminal.ScrollToEnd();
        }
    }
}
