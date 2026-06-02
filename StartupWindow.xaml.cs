using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PromobAutomacao.Network;

namespace PromobAutomacao{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Tela de inicialização para seleção do modo de operação (Local, Servidor ou Cliente).
        /// Define as propriedades em <see cref="AppMode"/> antes de abrir a MainWindow.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public partial class StartupWindow : Window{

        /// <summary>Indica se o usuário confirmou a seleção (true) ou fechou a janela sem confirmar (false).</summary>
        public bool Confirmed { get; private set; } = false;

        private AppRunMode _selectedMode = AppRunMode.Local;

        // Cores dos cartões
        private static readonly SolidColorBrush _selectedBorder = new(Color.FromRgb(56, 189, 248));   // #38BDF8
        private static readonly SolidColorBrush _selectedBg     = new(Color.FromRgb(30, 41, 59));     // #1E293B
        private static readonly SolidColorBrush _defaultBorder  = new(Color.FromRgb(38, 42, 59));     // #262A3B
        private static readonly SolidColorBrush _defaultBg      = new(Color.FromRgb(24, 26, 37));     // #181A25

        public StartupWindow(){
            InitializeComponent();

            // Detecta e exibe o IP local
            txtLocalIp.Text    = GetLocalIp();
            txtPortDisplay.Text = NetworkSettings.Port.ToString();
            txtPortClient.Text  = NetworkSettings.Port.ToString();

            // Pré-preenche o último IP utilizado (se houver)
            if (NetworkSettings.RecentServerIps.Length > 0){
                txtServerIp.Text = NetworkSettings.RecentServerIps[0];
            }

            SelectMode(AppRunMode.Local);
        }

        // ==========================================
        // --- Seleção de Modo ---
        // ==========================================

        private void SelectMode(AppRunMode mode){
            _selectedMode = mode;

            ResetCard(cardLocal);
            ResetCard(cardServidor);
            ResetCard(cardCliente);

            switch (mode){
                case AppRunMode.Local:
                    SelectCard(cardLocal);
                    panelLocalInfo.Visibility    = Visibility.Visible;
                    panelServidorInfo.Visibility = Visibility.Collapsed;
                    panelClienteInfo.Visibility  = Visibility.Collapsed;
                    break;

                case AppRunMode.Server:
                    SelectCard(cardServidor);
                    panelLocalInfo.Visibility    = Visibility.Collapsed;
                    panelServidorInfo.Visibility = Visibility.Visible;
                    panelClienteInfo.Visibility  = Visibility.Collapsed;
                    break;

                case AppRunMode.Client:
                    SelectCard(cardCliente);
                    panelLocalInfo.Visibility    = Visibility.Collapsed;
                    panelServidorInfo.Visibility = Visibility.Collapsed;
                    panelClienteInfo.Visibility  = Visibility.Visible;
                    txtConnectionStatus.Text     = "";
                    break;
            }
        }

        private void SelectCard(Border card){
            card.BorderBrush = _selectedBorder;
            card.Background  = _selectedBg;
        }

        private void ResetCard(Border card){
            card.BorderBrush = _defaultBorder;
            card.Background  = _defaultBg;
        }

        // ==========================================
        // --- Event Handlers ---
        // ==========================================

        private void CardLocal_MouseDown(object sender, MouseButtonEventArgs e)    => SelectMode(AppRunMode.Local);
        private void CardServidor_MouseDown(object sender, MouseButtonEventArgs e) => SelectMode(AppRunMode.Server);
        private void CardCliente_MouseDown(object sender, MouseButtonEventArgs e)  => SelectMode(AppRunMode.Client);

        private async void BtnConfirmar_Click(object sender, RoutedEventArgs e){
            btnConfirmar.IsEnabled = false;

            int parsedPort = NetworkSettings.Port;

            if (_selectedMode == AppRunMode.Client){
                var host = txtServerIp.Text.Trim();

                if (string.IsNullOrWhiteSpace(host)){
                    SetStatus("⚠  Informe o IP do servidor antes de continuar.", "#EF4444");
                    btnConfirmar.IsEnabled = true;
                    return;
                }

                SetStatus("⏳  Verificando conexão com o servidor...", "#94A3B8");

                bool ok = await Task.Run(() => TestTcpConnection(host, parsedPort));

                if (!ok){
                    SetStatus("✗  Servidor não encontrado. Verifique o IP, Porta e se o Servidor está ativo.", "#EF4444");
                    btnConfirmar.IsEnabled = true;
                    return;
                }

                // Abre a tela de Login para o Cliente decidir se entra como Espectador ou Operador (admin)
                var login = new LoginWindow();
                login.ShowDialog();

                if (!login.Confirmed){
                    SetStatus("ℹ  Conexão estabelecida, mas login cancelado.", "#94A3B8");
                    btnConfirmar.IsEnabled = true;
                    return;
                }

                AppMode.ServerHost = host;
                NetworkSettings.AddRecentIp(host);
            }

            AppMode.Mode = _selectedMode;
            AppMode.Port = parsedPort;

            Confirmed = true;
            Close();
        }

        private void SetStatus(string text, string hexColor){
            txtConnectionStatus.Text = text;
            txtConnectionStatus.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hexColor));
        }

        // ==========================================
        // --- Helpers ---
        // ==========================================

        private static bool TestTcpConnection(string host, int port){
            try{
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);
                return task.Wait(TimeSpan.FromSeconds(4)) && client.Connected;
            }
            catch{ return false; }
        }

        private static string GetLocalIp(){
            try{
                return NetworkInterface.GetAllNetworkInterfaces()
                    .Where(i => i.OperationalStatus == OperationalStatus.Up &&
                                i.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(i => i.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())
                    .FirstOrDefault() ?? "localhost";
            }
            catch{ return "localhost"; }
        }
    }
}
