using System;
using System.Windows;

namespace AutomacaoPromobTeste{
    internal class Program{
        [STAThread]
        static void Main(string[] args){
            // Inicializa a aplicação WPF
            var app = new Application();
            
            // Impede que o fechamento da janela de startup finalize a aplicação antes de abrir a MainWindow
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Exibe a tela de seleção de modo (Local / Servidor / Cliente)
            var startup = new StartupWindow();
            startup.ShowDialog();

            // Se o usuário fechou a janela sem confirmar, encerra o programa
            if (!startup.Confirmed){
                app.Shutdown();
                return;
            }

            // Restaura o modo padrão para fechar a aplicação ao fechar a janela principal
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;

            // Instancia e exibe a janela de controle da automação no modo selecionado
            var mainWindow = new MainWindow();
            app.Run(mainWindow);
        }
    }
}