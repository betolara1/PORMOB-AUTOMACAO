// using System;
// using System.Diagnostics;
// using System.Linq;
// using System.Threading;
// using AutomacaoPromobTeste.Utils;
// using FlaUI.Core.AutomationElements;

// namespace AutomacaoPromobTeste.Automation{
//     public static class PromobWatchdog{
//         private const string ProcessName = "Promob5";
        
//         // 1. Verifica se o processo está respondendo ou se travou
//         public static bool PromobEstaSaudavel(){
//             var processos = Process.GetProcessesByName(ProcessName);

//             if(!processos.any()) return false; // Nem sequer está aberto

//             foreach(var proc in processos){
//                 // Se a UI do processo estiver travada (congelada), retorna falso
//                 if(!proc.responding){
//                     Logger.Log("[CRÍTICO] Promob detectado como 'Não Respondendo'!");
//                     return false; 
//                 }
//             }

//             return true;
//         }

//         // 2. Mata qualquer processo órfão ou travado de forma limpa e agressiva
//         public static void ForcarFechamentoPromob(){
//             var processos = Process.GetProcessesByName(ProcessName);

//             foreach(var proc in processo){
//                 try{
//                     Logger.Log($"[INFO] Finalizando processo do Promob travado (PID: {proc.Id})...");
 
//                     proc.Kill(); // Envia o sinal SIGKILL ao Windows
//                     proc.WaitForExit(5000); // Espera até 5 segundos para sumir da memória
//                 }
//                 catch(Exception ex){
//                     Logger.Log($"[ERRO] Não foi possível matar o processo: {ex.Message}");
//                 }
//             }
//         }

//         // 3. Inicializa o aplicativo do zero
//         public static Process IniciarPromob(string Executavel){
//             Logger.Log("[INFO] Iniciando uma nova instância limpa do Promob...");

//             var info = new ProcessStartInfo{
//                 FileName = executavel,
//                 UseShellExecute = true,
//                 WindowStyle = ProcessWindowStyle.Normal
//             };

//             return Process.start(info);
//         }
//     }
// }