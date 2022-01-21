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
        new(() => RestService.For<ITcddApi>("https://ebilet.tcddtasimacilik.gov.tr"),
            LazyThreadSafetyMode.None);

    internal static ITcddApi Client => _client.Value;
}