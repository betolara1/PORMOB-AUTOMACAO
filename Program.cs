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
            Directory.CreateDirectory(PromobConfig.PastaPromobErro);

            using var automation = new UIA3Automation();

            int processados = 0;
            int erros = 0;

            Logger.Log("[INFO] Modo contínuo ativado. Monitorando pasta para novos arquivos...");
            Logger.Log($"[INFO] Pasta: {PromobConfig.PastaPromob}\n");

            // Sinaliza quando um novo arquivo é detectado ou quando iniciamos (para processar arquivos existentes)
            using var fileAddedEvent = new AutoResetEvent(true);

            // Configura o FileSystemWatcher para monitorar a pasta de forma eficiente
            using var watcher = new FileSystemWatcher(PromobConfig.PastaPromob, "*.promob"){
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            // Eventos que disparam a verificação
            watcher.Created += (s, e) => fileAddedEvent.Set();
            watcher.Renamed += (s, e) => fileAddedEvent.Set();

            while (true){
                // Obtém todos os arquivos pendentes
                var arquivos = Directory.GetFiles(PromobConfig.PastaPromob, "*.promob")
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (arquivos.Count == 0){
                    // Pasta vazia: aguarda sinal do sistema operacional sem consumir CPU
                    Console.Write($"\r[AGUARDANDO] Nenhum arquivo na pasta. Processados: {processados} | Erros: {erros} — Monitorando...");
                    fileAddedEvent.WaitOne();
                    continue;
                }

                foreach (var arquivo in arquivos){
                    var nome = Path.GetFileName(arquivo);

                    Console.WriteLine();
                    Console.WriteLine("══════════════════════════════════════════");
                    Console.WriteLine($"[NOVO] Processando: {nome}");
                    Console.WriteLine($"       Processados até agora: {processados} | Erros: {erros}");
                    Console.WriteLine("══════════════════════════════════════════");

                    try{
                        // Pequena pausa para garantir que o arquivo não esteja bloqueado (ex: acabando de ser movido ou salvo)
                        Thread.Sleep(500);

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
                    catch (AutomacaoPromobTeste.Promob.PromobExportException exErp){
                        // ===================================================================
                        // Erro de exportação ERP ("Abortado com erro!")
                        // O projeto já foi fechado pelo FecharProjeto dentro de ProcessarArquivo.
                        // Aqui só movemos o arquivo para "promob erro" e continuamos.
                        // ===================================================================
                        erros++;
                        Console.WriteLine($"\n[ERRO EXPORTAÇÃO] {nome}: {exErp.Message}");
                        Logger.RegistrarErro(nome, exErp);

                        // Move o arquivo para a pasta "promob erro"
                        try{
                            var destino = Path.Combine(PromobConfig.PastaPromobErro, nome);
                            // Se já existe um arquivo com mesmo nome, adiciona timestamp
                            if (File.Exists(destino)){
                                var semExtensao = Path.GetFileNameWithoutExtension(nome);
                                var extensao = Path.GetExtension(nome);
                                destino = Path.Combine(PromobConfig.PastaPromobErro, $"{semExtensao}_{DateTime.Now:yyyyMMdd_HHmmss}{extensao}");
                            }
                            File.Move(arquivo, destino);
                            Logger.Log($"  [OK] Arquivo '{nome}' movido para '{PromobConfig.PastaPromobErro}'.");
                        }
                        catch (Exception exMove){
                            Logger.Log($"  [AVISO] Não foi possível mover '{nome}' para 'promob erro': {exMove.Message}", LogLevel.Warn);
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
        }

        static void Banner(){
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║   Automação Promob - Gerador de XML      ║");
            Console.WriteLine("║      Versão otimizada com FlaUI + IA     ║");
            Console.WriteLine("╚══════════════════════════════════════════╝\n");
        }
    }
}