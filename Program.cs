using System;
using System.Windows;

namespace AutomacaoPromobTeste{
    internal class Program{
        [STAThread]
        static void Main(string[] args){
            // Inicializa a aplicação WPF
            var app = new Application();
            
            // Instancia e exibe a janela de controle da automação
            var mainWindow = new MainWindow();
            app.Run(mainWindow);
        }
    }
}