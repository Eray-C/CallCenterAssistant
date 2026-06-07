using AsterNET.Manager;
using AsterNET.Manager.Action;
using CallCenterAssistant.Models.Request;

namespace CallCenterAssistant.Services
{
    public interface IAsteriskService
    {
        Task<bool> OriginateCallAsync(OriginateRequest request);
    }

    public class AsteriskService : IAsteriskService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly ILogger<AsteriskService> _logger;

        public AsteriskService(IConfiguration configuration, ILogger<AsteriskService> logger)
        {
            var section = configuration.GetSection("Asterisk");
            _host = section["Host"] ?? "localhost";
            _port = int.TryParse(section["Port"], out var p) ? p : 5038;
            _username = section["Username"] ?? "admin";
            _password = section["Password"] ?? "password";
            _logger = logger;

            // Docker container ortamında otomatik ağ/konak çözümleme
            if (_host == "localhost" && Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
            {
                try
                {
                    var addresses = System.Net.Dns.GetHostAddresses("asterisk");
                    if (addresses.Length > 0)
                    {
                        _host = "asterisk";
                    }
                }
                catch
                {
                    _host = "host.docker.internal";
                }
            }
        }

        public async Task<bool> OriginateCallAsync(OriginateRequest request)
        {
            return await Task.Run(() =>
            {
                ManagerConnection? managerConnection = null;
                try
                {
                    managerConnection = new ManagerConnection(_host, _port, _username, _password);

                    _logger.LogInformation("Asterisk AMI bağlantısı kuruluyor: {Host}:{Port}", _host, _port);


                    managerConnection.Login();




                    var originateAction = new OriginateAction
                    {
                        Channel = request.Channel,
                        Exten = request.Exten,
                        Context = request.Context,
                        Priority = request.Priority.ToString(),
                        CallerId = request.CallerId,
                        Timeout = request.Timeout,
                        Async = true
                    };

                    _logger.LogInformation("Arama başlatılıyor. Kanal: {Channel}, Hedef: {Exten}", request.Channel, request.Exten);
                    var response = managerConnection.SendAction(originateAction);

                    if (response.Response == "Success")
                    {
                        _logger.LogInformation("Arama başarıyla tetiklendi.");
                        return true;
                    }
                    else
                    {
                        _logger.LogError("Arama başlatma başarısız oldu. Yanıt: {Response}, Mesaj: {Message}", response.Response, response.Message);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Asterisk AMI araması sırasında beklenmeyen bir hata oluştu.");
                    return false;
                }
                finally
                {
                    if (managerConnection != null && managerConnection.IsConnected())
                    {
                        try
                        {
                            managerConnection.Logoff();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Asterisk AMI bağlantısı kapatılırken hata oluştu.");
                        }
                    }
                }
            });
        }
    }
}
