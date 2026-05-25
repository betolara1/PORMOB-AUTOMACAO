using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AutomacaoPromobTeste.Utils;

namespace AutomacaoPromobTeste.Network{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Servidor TCP que aceita conexões de clientes remotos, transmite logs em tempo real
        /// e recebe comandos de controle para executar localmente (Computador X).
        /// </summary>
    //--------------------------------------------------------------------------------------
    public class PromobServer : IDisposable{
        private TcpListener? _listener;
        private readonly ConcurrentDictionary<Guid, TcpClient> _clients = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly int _port;

        //--------------------------------------------------------------------------------------
            /// <summary>Disparado quando um comando JSON válido é recebido de um cliente.</summary>
        //--------------------------------------------------------------------------------------
        public event Action<string>? OnCommandReceived;

        //--------------------------------------------------------------------------------------
            /// <summary>Disparado quando o número de clientes conectados muda.</summary>
        //--------------------------------------------------------------------------------------
        public event Action? OnClientCountChanged;

        /// <summary>Número atual de clientes conectados.</summary>
        public int ClientCount => _clients.Count;

        public PromobServer(int port){ _port = port; }

        //--------------------------------------------------------------------------------------
            /// <summary>Inicia o listener TCP e começa a aceitar conexões em background.</summary>
        //--------------------------------------------------------------------------------------
        public void Start(){
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _ = AcceptClientsAsync(_cts.Token);
        }

        private async Task AcceptClientsAsync(CancellationToken token){
            while (!token.IsCancellationRequested){
                try{
                    var client = await _listener!.AcceptTcpClientAsync(token);
                    client.NoDelay = true;
                    var id = Guid.NewGuid();
                    _clients[id] = client;
                    Logger.Log($"[REDE] Novo cliente conectado ({_clients.Count} total).");
                    OnClientCountChanged?.Invoke();
                    _ = HandleClientAsync(id, client, token);
                }
                catch (OperationCanceledException){ break; }
                catch (Exception ex) when (!token.IsCancellationRequested){
                    Logger.Log($"[REDE] Erro ao aceitar conexão: {ex.Message}", LogLevel.Warn);
                }
            }
        }

        private async Task HandleClientAsync(Guid id, TcpClient client, CancellationToken token){
            var buffer = new byte[4096];
            var partial = new StringBuilder();

            try{
                var stream = client.GetStream();
                while (!token.IsCancellationRequested && client.Connected){
                    int bytesRead;
                    try{ bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token); }
                    catch{ break; }

                    if (bytesRead == 0) break;

                    partial.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    // Processa todas as linhas completas recebidas
                    var content = partial.ToString();
                    var idx = content.IndexOf('\n');
                    while (idx >= 0){
                        var line = content[..idx].Trim();
                        content = content[(idx + 1)..];
                        idx = content.IndexOf('\n');

                        if (!string.IsNullOrEmpty(line)){
                            var msg = WsMessage.Deserialize(line);
                            if (msg?.Type == MessageType.Command && msg.Action != null){
                                OnCommandReceived?.Invoke(msg.Action);
                            }
                        }
                    }
                    partial.Clear();
                    partial.Append(content);
                }
            }
            catch{ }
            finally{
                _clients.TryRemove(id, out _);
                try{ client.Close(); } catch{ }
                Logger.Log($"[REDE] Cliente desconectado ({_clients.Count} restantes).");
                OnClientCountChanged?.Invoke();
            }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>Envia uma mensagem para todos os clientes conectados.</summary>
        //--------------------------------------------------------------------------------------
        public void Broadcast(WsMessage message){
            var data = Encoding.UTF8.GetBytes(message.Serialize() + "\n");
            var toRemove = new List<Guid>();

            foreach (var (id, client) in _clients){
                try{
                    if (client.Connected){
                        client.GetStream().Write(data, 0, data.Length);
                    } else{
                        toRemove.Add(id);
                    }
                }
                catch{
                    toRemove.Add(id);
                }
            }

            foreach (var id in toRemove){
                _clients.TryRemove(id, out var dead);
                try{ dead?.Close(); } catch{ }
                OnClientCountChanged?.Invoke();
            }
        }

        public void Dispose(){
            _cts.Cancel();
            try{ _listener?.Stop(); } catch{ }
            foreach (var client in _clients.Values) try{ client.Close(); } catch{ }
            _clients.Clear();
        }
    }
}
