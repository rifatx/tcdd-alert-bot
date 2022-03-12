using System.Net;
using Refit;

namespace TCDDAlertBot;

internal interface ITcddApi
{
    [Headers("Content-Type: application/json; charset=utf-8")]
    [Post("/WebServisWeb/rest/EybisRestApplication/istasyonTrenSorgula")]
    Task<IstasyonTrenSorgulaResponse> IstasyonTrenSorgula(
        IstasyonTrenSorgulaRequest user,
        [Header("Authorization")] string authHeaderValue);
}

internal class RestClient
{
    private static Lazy<ITcddApi> _client =>
        new(() =>
            {
                var handler = new HttpClientHandler
                {
                    UseProxy = true,
                    Proxy = new WebProxy("torproxy",8118)
                };
                var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri("https://ebilet.tcddtasimacilik.gov.tr")
                };

                return RestService.For<ITcddApi>(client);
            },
            LazyThreadSafetyMode.None);

    internal static ITcddApi Client => _client.Value;
}