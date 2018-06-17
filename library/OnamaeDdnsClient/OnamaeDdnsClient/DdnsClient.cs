using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace OnamaeDdnsClient
{
    public sealed class DdnsClient
    {
        private static readonly string DefaultHost = "ddnsclient.onamae.com";
        private static readonly ushort DefaultSslPort = 65010;
        private static readonly int DefaultWriteTimeout = 60000;
        private static readonly int DefaultReadTimeout = 60000;

        public DdnsClient(string userId, string password)
        {
            UserId = userId;
            Password = password;
        }

        public string UserId { get; set; }
        public string Password { get; set; }
        public int WriteTimeout { get; set; } = DefaultWriteTimeout;
        public int ReadTimeout { get; set; } = DefaultReadTimeout;
        public string Host { get; set; } = DefaultHost;
        public ushort SslPort { get; set; } = DefaultSslPort;

        public async Task UpdateAsync(string hostName, string domainName, IPAddress address)
        {
            try
            {
                if (string.IsNullOrEmpty(domainName))
                    throw new ArgumentNullException(nameof(domainName));
                if (string.IsNullOrEmpty(hostName))
                    throw new ArgumentNullException(nameof(hostName));
                if (address == null)
                    throw new ArgumentNullException(nameof(address));
                if (address.AddressFamily != AddressFamily.InterNetwork)
                    throw new ArgumentException("Invalid address family", nameof(address));

                using (var client = CreteInternalClinet())
                {
                    await client.ConnectAsync().ConfigureAwait(false);
                    try
                    {
                        var loginResult = await client.SendCommandAsync(new LoginCommand(UserId, Password));
                        if (loginResult.Code != CommandResponseCode.Success)
                            throw new DdnsClientCommandException(loginResult.Code);
                        var modifyIpResult =
                            await client.SendCommandAsync(new ModifyIpAddressCommand(hostName, domainName,
                                address.ToString()));
                        if (modifyIpResult.Code != CommandResponseCode.Success)
                            throw new DdnsClientCommandException(modifyIpResult.Code);
                    }
                    catch (DdnsClientException)
                    {
                        try
                        {
                            await client.SendCommandAsync(new LogoutCommand());
                        }
                        catch
                        {
                            // ignored
                        }

                        throw;
                    }
                    var logoutResult = await client.SendCommandAsync(new LogoutCommand());
                    if (logoutResult.Code != CommandResponseCode.Success)
                        throw new DdnsClientCommandException(logoutResult.Code);
                }
            }
            catch (DdnsClientException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new DdnsClientException("DdnsClientException", e);
            }
        }

        private SslClient CreteInternalClinet()
        {
            return new SslClient(Host, SslPort, ReadTimeout, WriteTimeout);
        }
    }
}