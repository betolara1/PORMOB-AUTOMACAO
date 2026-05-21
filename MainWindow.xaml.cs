using System;
using System.IO;
using System.Linq;
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
using FlaUI.UIA3;

namespace AutomacaoPromobTeste
{
    public partial class MainWindow : Window
    {
        private bool _isMonitoring = false;
        private CancellationTokenSource? _cts;
        private Task? _automationTask;
        
        private int _processadosCount = 0;
        private int _errosCount = 0;
        private System.Windows.Threading.DispatcherTimer? _statusTimer;

        public MainWindow()
        {
            InitializeComponent();
            
            // Exibir caminhos das pastas configuradas
            txtPastaMonitorada.Text = Path.GetFileName(PromobConfig.PastaPromob) ?? "promob";
            txtPastaMonitorada.ToolTip = PromobConfig.PastaPromob;
            
            txtPastaXml.Text = Path.GetFileName(PromobConfig.PastaXml) ?? "xml";
            txtPastaXml.ToolTip = PromobConfig.PastaXml;

            // Inscreve a interface no evento de logs
            Logger.OnLog += LogToTerminal;

            Logger.Log("══════════════════════════════════════════");
            Logger.Log("║   Painel de Controle de Automação      ║");
            Logger.Log("║   Pronto para iniciar o monitoramento. ║");
            Logger.Log("══════════════════════════════════════════");

            // Inicializar timer de monitoramento do estado do Promob
            _statusTimer = new System.Windows.Threading.DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1.5);
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            // Executa verificação inicial imediata
            AtualizarBotaoIniciar();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Para o timer de verificação de processos
            _statusTimer?.Stop();

            // Garante a parada da automação se fechar a janela
            if (_isMonitoring)
            {
                _cts?.Cancel();
            }
            Logger.OnLog -= LogToTerminal;
            base.OnClosed(e);
        }

        // ==========================================
        // --- EVENT HANDLERS ---
        // ==========================================

        private void BtnToggleAutomacao_Click(object sender, RoutedEventArgs e)
        {
            if (!_isMonitoring)
            {
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
            }
        }

        private void BtnAbrirPromob_Click(object sender, RoutedEventArgs e)
        {
            btnAbrirPromob.IsEnabled = false;
            try
            {
                // 1. Verifica se já está em execução
                var currentProcId = Process.GetCurrentProcess().Id;
                var promobProc = Process.GetProcesses()
                    .FirstOrDefault(p => p.Id != currentProcId &&
                                         p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                                         !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase) &&
                                         !p.ProcessName.Contains("Automacao", StringComparison.OrdinalIgnoreCase));

                if (promobProc != null)
                {
                    Logger.Log($"[INFO] Promob já está em execução (PID: {promobProc.Id}). Trazendo para a tela...");
                    // Tentativa de foco
                    try
                    {
                        using var automation = new UIA3Automation();
                        var janela = PromobWindowHelper.AguardarJanelaPromob(automation, 2000);
                        if (janela != null)
                        {
                            InteractionHelper.AtivarJanela(janela);
                        }
                    }
                    catch
                    {
                        // Fallback básico de foco pelo processo se falhar UIA3
                        var handle = promobProc.MainWindowHandle;
                        if (handle != IntPtr.Zero)
                        {
                            // Traz janela do SO ao topo
                            InteractionHelper.EsperarUiRespirar(200);
                        }
                    }
                    return;
                }

                // 2. Tenta localizar o executável
                string? caminhoExe = DetectarPromobExe();

                if (string.IsNullOrEmpty(caminhoExe))
                {
                    // Diálogo de seleção manual se não achou automático
                    var dialog = new OpenFileDialog
                    {
                        Title = "Selecione o Executável do Promob (Promob.exe)",
                        Filter = "Executável do Promob (*.exe)|*.exe;*.lnk",
                        FileName = "Promob5.exe"
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        caminhoExe = dialog.FileName;
                        // Salva para futuras execuções
                        var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "promob_path.txt");
                        File.WriteAllText(configPath, caminhoExe);
                    }
                }

                if (!string.IsNullOrEmpty(caminhoExe) && File.Exists(caminhoExe))
                {
                    Logger.Log($"[INFO] Iniciando Promob a partir de: {caminhoExe}");
                    var info = new ProcessStartInfo
                    {
                        FileName = caminhoExe,
                        WorkingDirectory = Path.GetDirectoryName(caminhoExe) ?? "",
                        UseShellExecute = false, // Necessário para modificar variáveis de ambiente
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                    info.EnvironmentVariables["__COMPAT_LAYER"] = "RunAsInvoker";
                    Process.Start(info);
                }
                else
                {
                    Logger.Log("[AVISO] Operação cancelada ou executável do Promob não foi encontrado.", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[ERRO] Não foi possível iniciar o Promob: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                btnAbrirPromob.IsEnabled = true;
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLogTerminal.Clear();
        }

        // ==========================================
        // --- LOGIC FUNCTIONS ---
        // ==========================================

        private void StartMonitoring()
        {
            _isMonitoring = true;
            _cts = new CancellationTokenSource();

            // Atualiza UI para estado Ativo
            txtStatusText.Text = "Monitorando...";
            txtStatusText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Verde
            statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            if (statusIndicator.Effect is DropShadowEffect shadow)
            {
                shadow.Color = Color.FromRgb(16, 185, 129);
            }

            btnToggleAutomacao.Content = "Parar Automação";
            btnToggleAutomacao.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Vermelho
            btnAbrirPromob.IsEnabled = false;

            Logger.Log("[INFO] Modo contínuo iniciado. Monitorando arquivos na pasta...");

            // Inicia o processamento em segundo plano
            _automationTask = Task.Run(() => ExecutarLoopAutomacao(_cts.Token), _cts.Token);
        }

        private void StopMonitoring()
        {
            btnToggleAutomacao.IsEnabled = false;
            Logger.Log("[INFO] Solicitando parada da automação... Por favor, aguarde a conclusão da etapa atual.");
            _cts?.Cancel();
        }

        private void ExecutarLoopAutomacao(CancellationToken token)
        {
            VisionHelper.Inicializar();

            if (!Directory.Exists(PromobConfig.PastaPromob))
            {
                Logger.Log($"[ERRO] Pasta do Promob na Área de Trabalho não encontrada: {PromobConfig.PastaPromob}", LogLevel.Error);
                ResetUiStateOnStop();
                return;
            }

            Directory.CreateDirectory(PromobConfig.PastaXml);
            Directory.CreateDirectory(PromobConfig.PastaPromobErro);

            using var automation = new UIA3Automation();
            using var fileAddedEvent = new AutoResetEvent(true);
            
            // Configura o FileSystemWatcher para monitorar a pasta
            using var watcher = new FileSystemWatcher(PromobConfig.PastaPromob, "*.promob")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            watcher.Created += (s, e) => { try { fileAddedEvent.Set(); } catch { } };
            watcher.Renamed += (s, e) => { try { fileAddedEvent.Set(); } catch { } };

            bool loggedWaiting = false;

            while (!token.IsCancellationRequested)
            {
                // Obtém todos os arquivos pendentes
                var arquivos = Directory.GetFiles(PromobConfig.PastaPromob, "*.promob")
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (arquivos.Count == 0)
                {
                    if (!loggedWaiting)
                    {
                        Logger.Log($"[AGUARDANDO] Nenhum arquivo para processar. Aguardando novos arquivos...");
                        loggedWaiting = true;
                    }
                    
                    // Aguarda sinal de novos arquivos ou solicitação de cancelamento
                    WaitHandle.WaitAny(new[] { fileAddedEvent, token.WaitHandle });
                    continue;
                }

                loggedWaiting = false;

                foreach (var arquivo in arquivos)
                {
                    if (token.IsCancellationRequested)
                        break;

                    var nome = Path.GetFileName(arquivo);

                    Logger.Log("══════════════════════════════════════════");
                    Logger.Log($"[NOVO] Iniciando processamento: {nome}");
                    Logger.Log($"  Status: Processados: {_processadosCount} | Erros: {_errosCount}");
                    Logger.Log("══════════════════════════════════════════");

                    try
                    {
                        // Pequena pausa de segurança
                        Thread.Sleep(500);

                        Diagnostics.Medir("Processar arquivo", () => PromobWorkflow.ProcessarArquivo(automation, arquivo));
                        
                        _processadosCount++;
                        UpdateMetricsOnUi();
                        
                        Logger.Log($"[OK] {nome} processado com sucesso!");

                        // Exclui o arquivo processado da pasta
                        try
                        {
                            File.Delete(arquivo);
                            Logger.Log($"  [OK] Arquivo original '{nome}' excluído.");
                        }
                        catch (Exception exDel)
                        {
                            Logger.Log($"  [AVISO] Não foi possível excluir '{nome}': {exDel.Message}", LogLevel.Warn);
                        }
                    }
                    catch (PromobExportException exErp)
                    {
                        _errosCount++;
                        UpdateMetricsOnUi();
                        
                        Logger.Log($"[ERRO EXPORTAÇÃO] {nome}: {exErp.Message}", LogLevel.Error);
                        Logger.RegistrarErro(nome, exErp);

                        // Move o arquivo para a pasta "promob erro"
                        try
                        {
                            var destino = Path.Combine(PromobConfig.PastaPromobErro, nome);
                            if (File.Exists(destino))
                            {
                                var semExtensao = Path.GetFileNameWithoutExtension(nome);
                                var extensao = Path.GetExtension(nome);
                                destino = Path.Combine(PromobConfig.PastaPromobErro, $"{semExtensao}_{DateTime.Now:yyyyMMdd_HHmmss}{extensao}");
                            }
                            File.Move(arquivo, destino);
                            Logger.Log($"  [OK] Arquivo com erro movido para '{PromobConfig.PastaPromobErro}'.");
                        }
                        catch (Exception exMove)
                        {
                            Logger.Log($"  [AVISO] Não foi possível mover '{nome}' para 'promob erro': {exMove.Message}", LogLevel.Warn);
                        }
                    }
                    catch (Exception ex)
                    {
                        _errosCount++;
                        UpdateMetricsOnUi();
                        
                        Logger.Log($"[ERRO] Falha no processamento de {nome}: {ex.Message}", LogLevel.Error);
                        Logger.RegistrarErro(nome, ex);
                        
                        try
                        {
                            PromobWorkflow.TentarRecuperar(automation);
                        }
                        catch { }
                        
                        Logger.Log($"  [INFO] O arquivo '{nome}' permanecerá na pasta para reprocessamento.");
                    }
                }
            }

            ResetUiStateOnStop();
        }

        private void UpdateMetricsOnUi()
        {
            Dispatcher.Invoke(() =>
            {
                txtSucessosCount.Text = _processadosCount.ToString();
                txtErrosCount.Text = _errosCount.ToString();
            });
        }

        private void ResetUiStateOnStop()
        {
            Dispatcher.Invoke(() =>
            {
                _isMonitoring = false;
                
                txtStatusText.Text = "Parado";
                txtStatusText.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)); // Cinza
                statusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Vermelho
                if (statusIndicator.Effect is DropShadowEffect shadow)
                {
                    shadow.Color = Color.FromRgb(239, 68, 68);
                }

                btnToggleAutomacao.Content = "Iniciar Automação";
                btnToggleAutomacao.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Verde
                btnToggleAutomacao.IsEnabled = IsPromobRunning();
                btnAbrirPromob.IsEnabled = true;

                Logger.Log("[INFO] Monitoramento parado. Automação inativa.");
            });
        }

        // Helper to output to the logs textbox
        private void LogToTerminal(string message, LogLevel level)
        {
            Dispatcher.Invoke(() =>
            {
                string prefix = level switch
                {
                    LogLevel.Error => "[ERRO] ",
                    LogLevel.Warn => "[AVISO] ",
                    LogLevel.Debug => "[DEBUG] ",
                    _ => ""
                };

                // Remove prefixos duplicados se a própria mensagem já começar com eles
                string cleanMessage = message;
                if (!string.IsNullOrEmpty(prefix) && message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cleanMessage = message.Substring(prefix.Length);
                }

                string formattedTime = DateTime.Now.ToString("HH:mm:ss");
                txtLogTerminal.AppendText($"[{formattedTime}] {prefix}{cleanMessage}{Environment.NewLine}");
                txtLogTerminal.ScrollToEnd();
            });
        }

        // Tenta detectar o executável do Promob no sistema
        private string? DetectarPromobExe()
        {
            // 1. Tenta carregar de um arquivo de configuração local
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "promob_path.txt");
            if (File.Exists(configPath))
            {
                var path = File.ReadAllText(configPath).Trim();
                if (File.Exists(path)) return path;
            }

            // 2. Busca em pastas padrões comuns de instalação
            string[] raizes = {
                @"C:\Program Files\Promob",
                @"C:\Program Files (x86)\Promob"
            };

            foreach (var raiz in raizes)
            {
                if (Directory.Exists(raiz))
                {
                    try
                    {
                        var arquivos = Directory.GetFiles(raiz, "Promob.exe", SearchOption.AllDirectories);
                        if (arquivos.Length == 0)
                        {
                            arquivos = Directory.GetFiles(raiz, "Promob5.exe", SearchOption.AllDirectories);
                        }

                        if (arquivos.Length > 0)
                        {
                            // Salva para as próximas vezes
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

        private async void StatusTimer_Tick(object? sender, EventArgs e)
        {
            if (_isMonitoring)
            {
                btnToggleAutomacao.IsEnabled = true;
                return;
            }

            bool promobAberto = await Task.Run(() => IsPromobRunning());
            
            // Só atualiza se o estado do monitoramento não tiver mudado nesse meio tempo
            if (!_isMonitoring)
            {
                btnToggleAutomacao.IsEnabled = promobAberto;
            }
        }

        private void AtualizarBotaoIniciar()
        {
            if (_isMonitoring)
            {
                btnToggleAutomacao.IsEnabled = true;
                return;
            }

            btnToggleAutomacao.IsEnabled = IsPromobRunning();
        }

        private bool IsPromobRunning()
        {
            try
            {
                var currentProcId = Process.GetCurrentProcess().Id;
                return Process.GetProcesses()
                    .Any(p => p.Id != currentProcId &&
                              p.ProcessName.Contains("Promob", StringComparison.OrdinalIgnoreCase) &&
                              !p.ProcessName.Contains("Uploader", StringComparison.OrdinalIgnoreCase) &&
                              !p.ProcessName.Contains("Automacao", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }
}
