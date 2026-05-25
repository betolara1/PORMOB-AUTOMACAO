using System.Windows;
using System.Windows.Media;

namespace AutomacaoPromobTeste{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Janela de Login para autenticação do Cliente como Operador (admin) ou Espectador.
        /// </summary>
    //--------------------------------------------------------------------------------------
    public partial class LoginWindow : Window{
        
        /// <summary>Indica se o login foi confirmado (seja como Operador ou Espectador).</summary>
        public bool Confirmed { get; private set; } = false;

        public LoginWindow(){
            InitializeComponent();
            txtUsername.Focus();
        }

        private void BtnLogar_Click(object sender, RoutedEventArgs e){
            string user = txtUsername.Text.Trim();
            string pass = txtPassword.Password.Trim();

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)){
                ShowError("Por favor, preencha o Usuário e a Senha.");
                return;
            }

            // Credenciais fixas solicitadas pelo usuário: admin / admin
            if (user == "admin" && pass == "admin"){
                AppMode.IsSpectator = false;
                Confirmed = true;
                this.DialogResult = true;
                this.Close();
            } else{
                ShowError("Usuário ou Senha incorretos.");
            }
        }

        private void BtnEspectador_Click(object sender, RoutedEventArgs e){
            AppMode.IsSpectator = true;
            Confirmed = true;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e){
            Confirmed = false;
            this.DialogResult = false;
            this.Close();
        }

        private void ShowError(string message){
            txtErrorMsg.Text = "⚠ " + message;
        }
    }
}
