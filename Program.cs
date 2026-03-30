using System;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.UIA3;
using AutomacaoPromobTeste.Utils;
using AutomacaoPromobTeste.Promob;

namespace AutomacaoPromobTeste{
    internal class Program{
        static void Main(string[] args){
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Banner();

            VisionHelper.Inicializar();

            if (!Directory.Exists(PromobConfig.PastaPromob)){
                Logger.Log($"[ERRO] Pasta não encontrada: {PromobConfig.PastaPromob}", LogLevel.Error);
                Console.ReadKey();
                return;
            }

            Directory.CreateDirectory(PromobConfig.PastaXml);

            using var automation = new UIA3Automation();

            int processados = 0;
            int erros = 0;

            Logger.Log("[INFO] Modo contínuo ativado. Monitorando pasta para novos arquivos...");
            Logger.Log($"[INFO] Pasta: {PromobConfig.PastaPromob}\n");

            // Loop eterno: sempre monitora a pasta
            while (true){
                // Lê os arquivos a cada iteração (a pasta muda ao longo do tempo)
                var arquivo = Directory.GetFiles(PromobConfig.PastaPromob, "*.promob")
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (arquivo == null){
                    // Pasta vazia: aguarda e tenta novamente
                    Console.Write($"\r[AGUARDANDO] Nenhum arquivo na pasta. Processados: {processados} | Erros: {erros} — Verificando...");
                    Thread.Sleep(3000);
                    continue;
                }

                var nome = Path.GetFileName(arquivo);

                Console.WriteLine();
                Console.WriteLine("══════════════════════════════════════════");
                Console.WriteLine($"[NOVO] Processando: {nome}");
                Console.WriteLine($"       Processados até agora: {processados} | Erros: {erros}");
                Console.WriteLine("══════════════════════════════════════════");

                try{
                    Diagnostics.Medir("Processar arquivo", () => PromobWorkflow.ProcessarArquivo(automation, arquivo));
                    processados++;
                    Console.WriteLine($"\n[OK] {nome} processado com sucesso!");

                    // Exclui o arquivo processado da pasta
                    try{
                        File.Delete(arquivo);
                        Logger.Log($"  [OK] Arquivo '{nome}' excluído da pasta.");
                    }
                    catch (Exception exDel){
                        Logger.Log($"  [AVISO] Não foi possível excluir '{nome}': {exDel.Message}", LogLevel.Warn);
                    }
                }
                catch (Exception ex){
                    erros++;
                    Console.WriteLine($"\n[ERRO] Falha no processamento de {nome}: {ex.Message}");
                    Logger.RegistrarErro(nome, ex);
                    PromobWorkflow.TentarRecuperar(automation);
                    
                    Logger.Log($"  [INFO] O arquivo '{nome}' permanecerá na pasta para reprocessamento.");
                }

                Console.WriteLine();
            }
        }

        static void Banner(){
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║   Automação Promob - Gerador de XML      ║");
            Console.WriteLine("║      Versão otimizada com FlaUI + IA     ║");
            Console.WriteLine("╚══════════════════════════════════════════╝\n");
        }
    }
}