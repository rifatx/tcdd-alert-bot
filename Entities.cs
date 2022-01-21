using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TCDDAlertBot;

internal class MobilWsDetay
{
    [JsonPropertyName("mobilKanali")] public int MobilKanali { get; set; } = 1;
    [JsonPropertyName("mobilVersiyon")] public int MobilVersiyon { get; set; } = 183;
    [JsonPropertyName("osVersiyon")] public string OsVersiyon { get; set; } = "9";
    [JsonPropertyName("model")] public string Model { get; set; } = "Xiaomi Mi 9T";
    [JsonPropertyName("yeniMobil")] public bool YeniMobil { get; set; } = true;
}

internal class IstasyonTrenSorgulaRequest
{
    [JsonPropertyName("kanalKodu")] public string KanalKodu { get; set; } = "3";
    [JsonPropertyName("mobilWsDetay")] public MobilWsDetay MobilWsDetay { get; set; } = new MobilWsDetay();
    [JsonPropertyName("tarih")] public string Tarih { get; set; }
    [JsonPropertyName("stationName")] public string StationName { get; set; }
    [JsonPropertyName("dil")] public int Dil { get; set; } = 0;
}

internal class IstasyonTrenList
{
    [JsonPropertyName("trainNo")] public string TrainNo { get; set; }

    [JsonPropertyName("seferNoTanim")] public string SeferNoTanim { get; set; }

    [JsonPropertyName("ilkIstCikis")] public string IlkIstCikis { get; set; }

    [JsonPropertyName("istasyonaGelis")] public string IstasyonaGelis { get; set; }

    [JsonPropertyName("sonIstVaris")] public string SonIstVaris { get; set; }

    [JsonPropertyName("ilkIst")] public string IlkIst { get; set; }

    [JsonPropertyName("sonIst")] public string SonIst { get; set; }

    [JsonPropertyName("trenTuru")] public string TrenTuru { get; set; }

    [JsonPropertyName("seferBaslikId")] public object SeferBaslikId { get; set; }

    [JsonPropertyName("orerId")] public object OrerId { get; set; }

    [JsonPropertyName("type")] public int Type { get; set; }
}

internal class CevapBilgileri
{
    [JsonPropertyName("cevapKodu")] public string CevapKodu { get; set; }

    [JsonPropertyName("cevapMsj")] public string CevapMsj { get; set; }
}

internal class IstasyonTrenSorgulaResponse
{
    [JsonPropertyName("istasyonTrenList")] public List<IstasyonTrenList> IstasyonTrenList { get; set; }

    [JsonPropertyName("cevapBilgileri")] public CevapBilgileri CevapBilgileri { get; set; }
}

[TypeConverter(typeof(AlertRequestConverter))]
internal class AlertRequest
{
    internal DateOnly Date { get; }
    internal string DepartureStation { get; }
    internal string ArrivalStation { get; }

    public AlertRequest(DateOnly date, string departureStation, string arrivalStation)
    {
        Date = date;
        DepartureStation = departureStation;
        ArrivalStation = arrivalStation;
    }

    private bool Equals(AlertRequest? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other.Date == Date
               && other.DepartureStation == DepartureStation
               && other.ArrivalStation == ArrivalStation;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == typeof(AlertRequest) && Equals((AlertRequest) obj);
    }

    public override int GetHashCode() =>
        Date.GetHashCode() ^ DepartureStation.GetHashCode() ^ ArrivalStation.GetHashCode();

    public override string? ToString() =>
        $"{Date.ToString("dd/MM/yyyy")} {DepartureStation}->{ArrivalStation}";

    public static object? Parse(string s)
    {
        s = s.Trim();

        if (s.IndexOf(' ') is { } i and > 0)
        {
            var d = DateOnly.ParseExact(s[..i], "dd/MM/yyyy");

            s = s[(i + 1)..];
            var sl = s.Split("->");

            if (sl.Length > 1)
            {
                return new AlertRequest(d, sl[0], sl[1]);
            }
        }

        return default;
    }
}