open System
open System.Net.Http
open System.Text
open FSharp.Data

type JsonType = JsonProvider<"sample.json">

let getAsync (client: HttpClient) (url: string) =
    async {
        client.DefaultRequestHeaders.Add("User-Agent", "Weather YOUcast/0.1")
        let! response = client.GetAsync(url) |> Async.AwaitTask

        let! body =
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask

        return
            match response.IsSuccessStatusCode with
            | true -> Ok body
            | false -> Error body
    }

let postAsync (client: HttpClient) (url: string) (postContent: string) =
    async {
        let parameters =
            "{\"blocks\": [{\"type\": \"section\",\"text\": {\"type\": \"mrkdwn\",\"text\": \""
            + postContent
            + "\"}}]}"

        let content =
            new StringContent(parameters, Encoding.UTF8, "application/json")

        let! response = client.PostAsync(url, content) |> Async.AwaitTask

        let! body =
            response.Content.ReadAsStringAsync()
            |> Async.AwaitTask

        return
            match response.IsSuccessStatusCode with
            | true -> Ok body
            | false -> Error body
    }


let makeOneDay (data: JsonType.Forecast) =
    let intToStringTemperature (t: int option) =
        match t with
        | Some t -> t.ToString() + "℃"
        | None -> "---"

    $"%s{data.DateLabel} (%s{data.Date.ToShortDateString()})\n\
    %s{data.Detail.Weather}\n\
    最高気温 %s{intToStringTemperature data.Temperature.Max.Celsius}\n\
    最低気温 %s{intToStringTemperature data.Temperature.Min.Celsius}\n\
    降水確率\n\
    | 0- 6| 6-12|12-18|18-24|\n\
    |  %s{data.ChanceOfRain.T0006.String.Value}|  %s{data.ChanceOfRain.T0612.String.Value}|  %s{data.ChanceOfRain.T1218.String.Value}|  %s{data.ChanceOfRain.T1824.String.Value}|\n\
    ------------------------------"

let makePostContent (fetchedForecasts: JsonType.Root) =
    let forecasts = fetchedForecasts.Forecasts

    $"%s{fetchedForecasts.Title}\n\
    ------------------------------\n\
    %s{makeOneDay (forecasts[0])}\n\
    %s{makeOneDay (forecasts[1])}\n\
    %s{makeOneDay (forecasts[2])}\n"

let run =
    async {
        let cityId =
            Environment.GetEnvironmentVariable("CITY_ID")

        let weatherForecastsUrl =
            "https://weather.tsukumijima.net/api/forecast/city/"
            + cityId

        use httpClient = new HttpClient()

        let! fetchedForecasts = getAsync httpClient weatherForecastsUrl

        let postContent =
            match fetchedForecasts with
            | Ok body -> makePostContent (JsonType.Parse(body))
            | Error err -> "天気予報データ取得時エラー"

        let slackUrl =
            Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL")

        let! postToSlackResult = postAsync httpClient slackUrl postContent

        match postToSlackResult with
        | Ok body -> printfn $"%s{body}"
        | Error err -> printfn $"エラー: %s{err}"
    }

[<EntryPoint>]
let main argv =
    run |> Async.RunSynchronously

    0
