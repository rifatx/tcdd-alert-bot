using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace TCDDAlertBot;

public static class Program
{
    private const int RETRY_PERIOD = 30;

    private const string TELEGRAM_TOKEN_ENV_VAR = "TELEGRAM_TOKEN";
    private const string TCDD_HEADER_ENV_VAR = "TCDD_AUTH_HEADER_VALUE";
    private const string STATE_FILE_ENV_VAR = "STATE_FILE";

    private static readonly Regex _reCreateAlertArgs =
        new(@"(?<date>\d{8}|\*) (?<departureStation>.+):(?<arrivatStation>.+)");

    private static string _telegramToken;
    private static string _authorizationHeaderValue;
    private static string _stateFile;

    private static readonly Dictionary<AlertRequest, List<long>> _dictAlertRequestChatId = new();

    static Program()
    {
        _telegramToken = Environment.GetEnvironmentVariable(TELEGRAM_TOKEN_ENV_VAR);

        if (string.IsNullOrEmpty(_telegramToken))
        {
            throw new Exception($"Telegram token not set: {TELEGRAM_TOKEN_ENV_VAR}");
        }

        _authorizationHeaderValue = Environment.GetEnvironmentVariable(TCDD_HEADER_ENV_VAR);

        if (string.IsNullOrEmpty(_authorizationHeaderValue))
        {
            throw new Exception($"TCDD auth header not set: {TCDD_HEADER_ENV_VAR}");
        }

        _stateFile = Environment.GetEnvironmentVariable(STATE_FILE_ENV_VAR);

        if (string.IsNullOrEmpty(_stateFile))
        {
            throw new Exception($"State file not set: {STATE_FILE_ENV_VAR}");
        }
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.Lifetime.ApplicationStarted.Register(() => LoadState(_stateFile));
        app.Lifetime.ApplicationStopping.Register(() => SaveState(_stateFile));

        var bot = new TelegramBotClient(_telegramToken);

        var t = new Timer(async o =>
        {
            var tod = DateTime.UtcNow.TimeOfDay;

            if (tod > TimeSpan.FromHours(9) && tod < TimeSpan.FromHours(20))
            {
                return;
            }

            var b = (o as TelegramBotClient)!;

            HashSet<AlertRequest> reqsToRemove = new();

            foreach (var (key, value) in _dictAlertRequestChatId)
            {
                if ((await GetTrainNamesOnDate(key.Date, key.DepartureStation, key.ArrivalStation))
                    .ToList() is var trainList
                    && !trainList.Any())
                {
                    continue;
                }

                foreach (var chatId in value.Distinct())
                {
                    Console.WriteLine($"Sending alert '{key.DepartureStation} -> {key.ArrivalStation}' to {chatId}");
                    await b.SendTextMessageAsync(chatId,
                        $"[{string.Join(", ", trainList)}]: {key.DepartureStation} -> {key.ArrivalStation} on {key.Date} is now available.");
                }

                reqsToRemove.Add(key);
            }

            foreach (var r in reqsToRemove)
            {
                _dictAlertRequestChatId.Remove(r);
            }
            
        }, bot, TimeSpan.Zero, TimeSpan.FromSeconds(RETRY_PERIOD));


        bot.StartReceiving(new DefaultUpdateHandler(HandleUpdateAsync, HandleErrorAsync));

        app.Run();
    }

    private static async Task<IEnumerable<string>> GetTrainNamesOnDate(
        DateOnly date,
        string departureStation,
        string arrivalStation)
    {
        var req = new IstasyonTrenSorgulaRequest
        {
            Tarih = $"{date.ToString("MMM dd, yyyy")} 00:00:00 PM",
            StationName = departureStation
        };

        var res = await RestClient.Client.IstasyonTrenSorgula(req, _authorizationHeaderValue);

        if (res.CevapBilgileri.CevapKodu == "000")
        {
            return res.IstasyonTrenList
                .Where(t => t.IlkIst == departureStation && t.SonIst == arrivalStation)
                .Select(itl => itl.SeferNoTanim);
        }

        return Enumerable.Empty<string>();
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"message: '{update.Message!.Text}'");

            var text = update.Message?.Text;
            string? command;
            var index = -1;

            if (!string.IsNullOrEmpty(text) && text.Trim()?.IndexOf(' ') is { } i and > 0)
            {
                index = i;
                command = text[..index];
            }
            else
            {
                command = text;
            }

            switch (command)
            {
                case "?":
                    await botClient.SendTextMessageAsync(update.Message!.Chat.Id,
                        $"? l | cl | rm | time | c ddMMyyyy departureStation:arrivalStation",
                        cancellationToken: cancellationToken);
                    break;
                case "d":
                    var x = await GetTrainNamesOnDate(
                        DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
                        "Ankara Gar",
                        "Kars");
                    await botClient.SendTextMessageAsync(update.Message!.Chat.Id, $"{string.Join(",", x)}",
                        cancellationToken: cancellationToken);
                    break;
                case "l":
                    var list = string.Join(Environment.NewLine,
                        _dictAlertRequestChatId.Select(kvp => kvp.Key.ToString()));
                    await botClient.SendTextMessageAsync(update.Message!.Chat.Id, list,
                        cancellationToken: cancellationToken);
                    break;
                case "cl":
                    _dictAlertRequestChatId.Clear();
                    break;
                case "rm":
                {
                    var arg = update.Message!.Text?[(index + 1)..]!;

                    if (_reCreateAlertArgs.Match(arg) is var m && m.Success)
                    {
                        var departureStation = m.Groups["departureStation"].Value;
                        var arrivalStation = m.Groups["arrivatStation"].Value;

                        var v = _dictAlertRequestChatId.Where(
                                kvp =>
                                    m.Groups["date"].Value is { } d
                                    && (d == "*"
                                        || DateOnly.TryParseExact(d, "ddMMyyyy", out var date)
                                        && kvp.Key.Date == date)
                                    && (departureStation == "*"
                                        || kvp.Key.DepartureStation == departureStation)
                                    && (arrivalStation == "*"
                                        || kvp.Key.DepartureStation == arrivalStation))
                            .Select(kvp => kvp.Key);

                        var chatId = update.Message!.Chat.Id;

                        foreach (var kvp in v)
                        {
                            var l = _dictAlertRequestChatId[kvp];

                            l.Remove(chatId);

                            if (!l.Any())
                            {
                                _dictAlertRequestChatId.Remove(kvp);
                            }
                        }
                    }

                    SaveState(_stateFile);

                    break;
                }
                case "time":
                    await botClient.SendTextMessageAsync(update.Message!.Chat.Id,
                        $"sys: {DateTime.Now}, utc: {DateTime.UtcNow}",
                        cancellationToken: cancellationToken);
                    break;
                case "c":
                {
                    var arg = update.Message!.Text?[(index + 1)..]!;
                    var chatId = update.Message!.Chat.Id;

                    if (_reCreateAlertArgs.Match(arg) is var m && m.Success)
                    {
                        if (!DateOnly.TryParseExact(m.Groups["date"].Value, "ddMMyyyy", out var date))
                        {
                            return;
                        }

                        var departureStation = m.Groups["departureStation"].Value;
                        var arrivalStation = m.Groups["arrivatStation"].Value;

                        var req = new AlertRequest(date, departureStation, arrivalStation);

                        if (!_dictAlertRequestChatId.ContainsKey(req))
                        {
                            _dictAlertRequestChatId.Add(req, new List<long>());
                        }

                        if (!_dictAlertRequestChatId[req].Contains(chatId))
                        {
                            _dictAlertRequestChatId[req].Add(chatId);
                        }

                        SaveState(_stateFile);
                    }

                    break;
                }
            }
        }
        catch (Exception exception)
        {
            await HandleErrorAsync(botClient, exception, cancellationToken);
        }
    }

    private static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException =>
                $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    private static void SaveState(string stateFile)
    {
        lock (_dictAlertRequestChatId)
        {
            File.WriteAllText(stateFile, JsonConvert.SerializeObject(_dictAlertRequestChatId));
        }
    }

    private static void LoadState(string stateFile)
    {
        if (!File.Exists(stateFile))
        {
            return;
        }

        var dict =
            JsonConvert.DeserializeObject<Dictionary<AlertRequest, List<long>>>(File.ReadAllText(stateFile));

        if (dict == null)
        {
            return;
        }

        _dictAlertRequestChatId.Clear();

        foreach (var (k, v) in dict)
        {
            _dictAlertRequestChatId.Add(k, v);
        }
    }
}