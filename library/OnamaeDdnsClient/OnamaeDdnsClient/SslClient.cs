using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OnamaeDdnsClient
{
    internal sealed class SslClient : IDisposable
    {
        private readonly string _host;
        private readonly ushort _port;
        private readonly int _readTimeout;
        private readonly int _writeTimeout;
        private StreamReader _reader;
        private SslStream _sslStrem;
        private TcpClient _tcpClient;
        private StreamWriter _writer;

        public SslClient(string host, ushort port, int readTimeout, int writeTimeout)
        {
            _host = host;
            _port = port;
            _readTimeout = readTimeout;
            _writeTimeout = writeTimeout;
        }

        public async Task ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_host, _port).ConfigureAwait(false);
                _sslStrem = new SslStream(_tcpClient.GetStream(), false,
                    (sender, certificate, chain, errors) => true)
                {
                    ReadTimeout = _readTimeout,
                    WriteTimeout = _writeTimeout
                };
                await _sslStrem.AuthenticateAsClientAsync(_host);
                _reader = new StreamReader(_sslStrem, Encoding.ASCII, false, 2048, true);
                _writer = new StreamWriter(_sslStrem, Encoding.ASCII, 2048, true)
                {
                    NewLine = "\n"
                };

                var connectResponse = CommandResponse.Parse(await ReadMessage());
                if (connectResponse.Code != CommandResponseCode.Success)
                    throw new DdnsClientCommandException(connectResponse.Code);
            }
            catch (Exception)
            {
                _writer?.Dispose();
                _writer = null;
                _reader?.Dispose();
                _reader = null;
                _sslStrem?.Dispose();
                _sslStrem = null;
                _tcpClient?.Dispose();
                _tcpClient = null;
                throw;
            }
        }

        public async Task<CommandResponse> SendCommandAsync(Command request)
        {
            if (_tcpClient == null) throw new InvalidOperationException();

            await WriteMessage(request.GetMessageLines()).ConfigureAwait(false);
            return CommandResponse.Parse(await ReadMessage());
        }

        private async Task WriteMessage(IEnumerable<string> messageLines)
        {
            foreach (var message in messageLines)
                await _writer.WriteLineAsync(message).ConfigureAwait(false);
            await _writer.WriteLineAsync(".").ConfigureAwait(false);
            await _writer.FlushAsync();
        }

        private async Task<IList<string>> ReadMessage()
        {
            var messageLines = new List<string>(1);
            string line;
            while ((line = await _reader.ReadLineAsync().ConfigureAwait(false)) != null)
                if (line == ".")
                    break;
                else
                    messageLines.Add(line);

            return messageLines;
        }

        #region IDisposable Support

        private bool _disposedValue; // 重複する呼び出しを検出するには

        private void Dispose(bool disposing)
        {
            if (_disposedValue) return;
            if (disposing)
            {
                _writer?.Dispose();
                _reader?.Dispose();
                _sslStrem?.Dispose();
                _tcpClient?.Dispose();
            }

            _disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }

    internal abstract class Command
    {
        public abstract IEnumerable<string> GetMessageLines();
    }

    internal class LoginCommand : Command
    {
        public LoginCommand(string userId, string password)
        {
            UserId = userId;
            Password = password;
        }

        public string UserId { get; }
        public string Password { get; }

        public override IEnumerable<string> GetMessageLines()
        {
            yield return "LOGIN";
            yield return $"USERID:{UserId}";
            yield return $"PASSWORD:{Password}";
        }
    }

    internal class LogoutCommand : Command
    {
        public override IEnumerable<string> GetMessageLines()
        {
            yield return "LOGOUT";
        }
    }

    internal class ModifyIpAddressCommand : Command
    {
        public ModifyIpAddressCommand(string hostName, string domainName, string ipv4Address)
        {
            HostName = hostName;
            DomainName = domainName;
            IpV4Address = ipv4Address;
        }

        public string HostName { get; }
        public string DomainName { get; }
        public string IpV4Address { get; }

        public override IEnumerable<string> GetMessageLines()
        {
            yield return "MODIP";
            yield return $"HOSTNAME:{HostName}";
            yield return $"DOMNAME:{DomainName}";
            yield return $"IPV4:{IpV4Address}";
        }
    }

    internal class CommandResponse
    {
        public CommandResponse(CommandResponseCode code, string message)
        {
            Code = code;
            Message = message;
        }

        public CommandResponseCode Code { get; }
        public string Message { get; }

        public static CommandResponse Parse(IList<string> messages)
        {
            if (messages == null || messages.Count == 0)
                return new CommandResponse(CommandResponseCode.NoResponse, string.Empty);

            var message = string.Join("\n", messages);
            var separatorIndex = message.IndexOf(" ", StringComparison.Ordinal);
            if (separatorIndex <= 0)
                return new CommandResponse(CommandResponseCode.InvalidResponse, message);
            if (!int.TryParse(message.Substring(0, separatorIndex), out var code))
                return new CommandResponse(CommandResponseCode.InvalidResponse, message);

            return new CommandResponse((CommandResponseCode) code, message.Substring(separatorIndex + 1));
        }
    }

    public enum CommandResponseCode
    {
        InvalidResponse = -2,
        NoResponse = -1,
        Success = 0,
        Error = 1,
        LoginError = 2,
        DbError = 3,
        InvalidIpAddress = 4,
        ConnectionError = 5,
        InvalidHostNameOrDomainName = 6
    }
}