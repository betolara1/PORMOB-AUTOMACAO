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
using AutomacaoPromobTeste.Utils;
using AutomacaoPromobTeste.Promob;
using AutomacaoPromobTeste.Automation;
using AutomacaoPromobTeste.Network;
using FlaUI.UIA3;

namespace AutomacaoPromobTeste{
    public partial class MainWindow : Window{
        private bool _isMonitoring = false;
        private CancellationTokenSource? _cts;
        private Task? _automationTask;

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

            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║   Painel de Controle de Automação      ║");
            Logger.Log("║   Pronto para iniciar o monitoramento. ║");
            Logger.Log("══════════════════════════════════════════");

            // Inicializa o modo de rede conforme selecionado na StartupWindow
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
                        Logger.Log("[INFO] Fechando o Promob conforme solicitado pelo usuário...");

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

                        Logger.Log("[OK] Processos do Promob encerrados com sucesso.");
                    }
                    catch (Exception ex){
                        Logger.Log($"[ERRO] Falha ao fechar o Promob: {ex.Message}", LogLevel.Error);
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
                    Logger.Log($"[INFO] Promob já está em execução (PID: {promobProc.Id}). Trazendo para a tela...");
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
                    Logger.Log($"[INFO] Iniciando Promob a partir de: {caminhoExe}");
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
                    Logger.Log("[AVISO] Operação cancelada ou executável do Promob não foi encontrado.", LogLevel.Warn);
                }
            }
            catch (Exception ex){
                Logger.Log($"[ERRO] Não foi possível iniciar o Promob: {ex.Message}", LogLevel.Error);
            }
            finally{
                bool promobAberto = IsPromobRunning();
                AtualizarEstadoBotaoPromob(promobAberto);
            }
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

            Logger.Log("[INFO] Modo contínuo iniciado. Monitorando arquivos na pasta...");

            _automationTask = Task.Run(() => ExecutarLoopAutomacao(_cts.Token), _cts.Token);
        }

        private void StopMonitoring(){
            btnToggleAutomacao.IsEnabled = false;
            btnToggleAutomacao.Content = "Parando...";
            Logger.Log("[INFO] Solicitando parada da automação... Por favor, aguarde a conclusão da etapa atual.");
            _cts?.Cancel();
        }

        private void ExecutarLoopAutomacao(CancellationToken token){
            VisionHelper.Inicializar();

            if (!Directory.Exists(PromobConfig.PastaPromob)){
                Logger.Log($"[ERRO] Pasta do Promob na Área de Trabalho não encontrada: {PromobConfig.PastaPromob}", LogLevel.Error);
                ResetUiStateOnStop();
                return;
            }

            Directory.CreateDirectory(PromobConfig.PastaXml);
            Directory.CreateDirectory(PromobConfig.PastaPromobErro);

            using var automation = new UIA3Automation();
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
                        Logger.Log($"[AGUARDANDO] Nenhum arquivo para processar. Aguardando novos arquivos...");
                        loggedWaiting = true;
                    }

                    WaitHandle.WaitAny(new[] { fileAddedEvent, token.WaitHandle });
                    continue;
                }

                loggedWaiting = false;

                foreach (var arquivo in arquivos){
                    if (token.IsCancellationRequested)
                        break;

                    var nome = Path.GetFileName(arquivo);

                    Logger.Log("══════════════════════════════════════════");
                    Logger.Log($"[NOVO] Iniciando processamento: {nome}");
                    Logger.Log($"  Status: Processados: {_processadosCount} | Erros: {_errosCount}");
                    Logger.Log("══════════════════════════════════════════");

                    try{
                        Thread.Sleep(500);

                        Diagnostics.Medir("Processar arquivo", () => PromobWorkflow.ProcessarArquivo(automation, arquivo, token));

                        _processadosCount++;
                        UpdateMetricsOnUi();

                        Logger.Log($"[OK] {nome} processado com sucesso!");

                        try{
                            File.Delete(arquivo);
                            Logger.Log($"  [OK] Arquivo original '{nome}' excluído.");
                        }
                        catch (Exception exDel){
                            Logger.Log($"  [AVISO] Não foi possível excluir '{nome}': {exDel.Message}", LogLevel.Warn);
                        }
                    }
                    catch (PromobExportException exErp){
                        _errosCount++;
                        UpdateMetricsOnUi();

                        Logger.Log($"[ERRO EXPORTAÇÃO] {nome}: {exErp.Message}", LogLevel.Error);
                        Logger.RegistrarErro(nome, exErp);

                        // Notifica via Telegram
                        NotificationService.EnviarAlertaFalha(nome, exErp.Message);

                        try{
                            var destino = Path.Combine(PromobConfig.PastaPromobErro, nome);
                            if (File.Exists(destino)){
                                var semExtensao = Path.GetFileNameWithoutExtension(nome);
                                var extensao = Path.GetExtension(nome);
                                destino = Path.Combine(PromobConfig.PastaPromobErro, $"{semExtensao}_{DateTime.Now:yyyyMMdd_HHmmss}{extensao}");
                            }
                            File.Move(arquivo, destino);
                            Logger.Log($"  [OK] Arquivo com erro movido para '{PromobConfig.PastaPromobErro}'.");
                        }
                        catch (Exception exMove){
                            Logger.Log($"  [AVISO] Não foi possível mover '{nome}' para 'promob erro': {exMove.Message}", LogLevel.Warn);
                        }
                    }
                    catch (OperationCanceledException){
                        Logger.Log($"[INFO] Processamento de '{nome}' cancelado manualmente pelo usuário.");
                        break;
                    }
                    catch (Exception ex){
                        // Intercepta cancelamento com máxima robustez
                        if (token.IsCancellationRequested ||
                            ex is OperationCanceledException ||
                            ex.InnerException is OperationCanceledException ||
                            (ex is AggregateException ae && ae.InnerExceptions.Any(e => e is OperationCanceledException))){

                            Logger.Log($"[INFO] Processamento de '{nome}' cancelado manualmente pelo usuário.");
                            break;
                        }

                        _errosCount++;
                        UpdateMetricsOnUi();

                        Logger.Log($"[ERRO] Falha no processamento de {nome}: {ex.Message}", LogLevel.Error);
                        Logger.RegistrarErro(nome, ex);

                        // Notifica via Telegram
                        NotificationService.EnviarAlertaFalha(nome, ex.Message);

                        try{
                            PromobWorkflow.TentarRecuperar(automation);
                        }
                        catch { }

                        Logger.Log($"  [INFO] O arquivo '{nome}' permanecerá na pasta para reprocessamento.");
                    }
                }
            }

            ResetUiStateOnStop();
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

                Logger.Log("[INFO] Monitoramento parado. Automação inativa.");
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
            bool promobAberto = await Task.Run(() => IsPromobRunning());

            Dispatcher.Invoke(() => {
                AtualizarEstadoBotaoPromob(promobAberto);
            });

            if (_isMonitoring){
                bool isStopping = _cts?.IsCancellationRequested ?? false;
                btnToggleAutomacao.IsEnabled = !isStopping;
                return;
            }

            if (!_isMonitoring){
                btnToggleAutomacao.IsEnabled = promobAberto;
            }
        }

        private void AtualizarBotaoIniciar(){
            bool promobAberto = IsPromobRunning();
            AtualizarEstadoBotaoPromob(promobAberto);

            if (_isMonitoring){
                bool isStopping = _cts?.IsCancellationRequested ?? false;
                btnToggleAutomacao.IsEnabled = !isStopping;
                return;
            }

            btnToggleAutomacao.IsEnabled = promobAberto;
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

            Logger.Log($"[REDE] Modo Servidor ativo. Porta: {AppMode.Port}. Aguardando clientes...");
            UpdateNetworkStatus();
        }

        private void InitClient(){
            _promobClient = new PromobClient();
            _promobClient.OnMessage     += HandleClientMessage;
            _promobClient.OnDisconnected += HandleClientDisconnected;

            // Desabilita botões até a conexão ser estabelecida
            btnToggleAutomacao.IsEnabled = false;
            btnAbrirPromob.IsEnabled     = false;

            Logger.Log($"[REDE] Modo Cliente. Conectando ao servidor {AppMode.ServerHost}:{AppMode.Port}...");
            UpdateNetworkStatus("Conectando...", false);

            _ = ConnectClientAsync();
        }

        private async Task ConnectClientAsync(){
            var success = await _promobClient!.ConnectAsync(AppMode.ServerHost, AppMode.Port);

            Dispatcher.Invoke(() => {
                if (success){
                    Logger.Log("[REDE] Conectado ao servidor com sucesso!");
                    UpdateNetworkStatus("Cliente Conectado", true);
                    btnToggleAutomacao.IsEnabled = true;
                    btnAbrirPromob.IsEnabled     = true;
                } else{
                    Logger.Log($"[REDE] Falha ao conectar em {AppMode.ServerHost}:{AppMode.Port}. Verifique se o Servidor está rodando.", LogLevel.Error);
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
                                Logger.Log("[INFO] Promob iniciado remotamente pelo operador.");
                            }
                            catch (Exception ex){
                                Logger.Log($"[ERRO] Falha ao iniciar Promob remotamente: {ex.Message}", LogLevel.Error);
                            }
                        } else{
                            Logger.Log("[AVISO] Executável do Promob não encontrado. Configure o caminho primeiro.", LogLevel.Warn);
                        }
                        break;

                    case "CLOSE_PROMOB":
                        ForceClosePromob();
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

                        // Atualiza o botão Abrir/Fechar Promob e a disponibilidade do toggle
                        AtualizarEstadoBotaoPromob(msg.PromobRunning);
                        btnToggleAutomacao.IsEnabled = msg.PromobRunning || _isMonitoring;
                        break;
                }
            });
        }

        private void HandleClientDisconnected(){
            Dispatcher.Invoke(() => {
                Logger.Log("[REDE] Conexão com o servidor foi perdida.", LogLevel.Error);
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
                _server.Broadcast(WsMessage.CreateMetrics(processados, erros, status, promobRunning));
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

                Logger.Log("[OK] Promob encerrado remotamente pelo operador.");
            }
            catch (Exception ex){
                Logger.Log($"[ERRO] Falha ao encerrar o Promob remotamente: {ex.Message}", LogLevel.Error);
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
