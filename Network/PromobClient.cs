using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PromobAutomacao.Network{
    //--------------------------------------------------------------------------------------
        /// <summary>
        /// Cliente TCP que conecta ao PromobServer, recebe logs e métricas em tempo real
        /// e envia comandos de controle para o servidor (Computador Y).
        /// </summary>
    //--------------------------------------------------------------------------------------
    public class PromobClient : IDisposable{
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private readonly CancellationTokenSource _cts = new();

        //--------------------------------------------------------------------------------------
            /// <summary>Disparado quando uma mensagem é recebida do servidor.</summary>
        //--------------------------------------------------------------------------------------
        public event Action<WsMessage>? OnMessage;

        //--------------------------------------------------------------------------------------
            /// <summary>Disparado quando a conexão com o servidor é perdida.</summary>
        //--------------------------------------------------------------------------------------
        public event Action? OnDisconnected;

        /// <summary>Indica se o cliente está atualmente conectado ao servidor.</summary>
        public bool IsConnected => _tcpClient?.Connected ?? false;

        //--------------------------------------------------------------------------------------
            /// <summary>
            /// Conecta ao servidor no endereço e porta especificados.
            /// Retorna true se a conexão foi estabelecida, false caso contrário.
            /// </summary>
        //--------------------------------------------------------------------------------------
        public async Task<bool> ConnectAsync(string host, int port){
            try{
                _tcpClient = new TcpClient{ NoDelay = true };
                await _tcpClient.ConnectAsync(host, port);
                _stream = _tcpClient.GetStream();
                _ = ReceiveLoopAsync(_cts.Token);
                return true;
            }
            catch{
                return false;
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token){
            var buffer = new byte[65536];
            var partial = new StringBuilder();

            try{
                while (!token.IsCancellationRequested && (_tcpClient?.Connected ?? false)){
                    int bytesRead;
                    try{ bytesRead = await _stream!.ReadAsync(buffer, 0, buffer.Length, token); }
                    catch{ break; }

                    if (bytesRead == 0) break;

                    partial.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    var content = partial.ToString();
                    var idx = content.IndexOf('\n');
                    while (idx >= 0){
                        var line = content[..idx].Trim();
                        content = content[(idx + 1)..];
                        idx = content.IndexOf('\n');

                        if (!string.IsNullOrEmpty(line)){
                            var msg = WsMessage.Deserialize(line);
                            if (msg != null) OnMessage?.Invoke(msg);
                        }
                    }
                    partial.Clear();
                    partial.Append(content);
                }
            }
            catch (OperationCanceledException){ }
            catch{ }
            finally{
                if (!token.IsCancellationRequested){
                    OnDisconnected?.Invoke();
                }
            }
        }

        //--------------------------------------------------------------------------------------
            /// <summary>Envia uma mensagem de comando ao servidor.</summary>
        //--------------------------------------------------------------------------------------
        public void Send(WsMessage message){
            try{
                if (_stream == null || !IsConnected) return;
                var data = Encoding.UTF8.GetBytes(message.Serialize() + "\n");
                _stream.Write(data, 0, data.Length);
            }
            catch{ }
        }

        public void Dispose(){
            _cts.Cancel();
            try{ _stream?.Dispose(); } catch{ }
            try{ _tcpClient?.Close(); } catch{ }
        }
    }
}
