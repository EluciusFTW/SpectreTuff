module Config

open System
open System.IO
open System.Text.Json

[<CLIMutable>]
type ConfigFile = {
  FirebaseUrl: string
  FirebaseSecret: string
  TuigetherUser: string
}

type Settings = {
  FirebaseUrl: string
  FirebaseSecret: string
  TuigetherUser: string
}

let private configPath () =
  Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.ApplicationData, "tuigether", "config.json")

let private templateJson =
  """{
  "firebaseUrl": "",
  "firebaseSecret": "",
  "tuigetherUser": ""
}
"""

let private writeTemplate (path: string) =
  Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
  File.WriteAllText(path, templateJson)

let private tryReadFile (path: string) =
  match File.Exists path with
  | false -> None
  | true ->
    let json = File.ReadAllText path
    let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
    Some(JsonSerializer.Deserialize<ConfigFile>(json, options))

let private resolve (environmentName: string) (fileValue: string) =
  match Environment.GetEnvironmentVariable environmentName with
  | value when not (String.IsNullOrWhiteSpace value) -> Some value
  | _ ->
    match fileValue with
    | value when not (String.IsNullOrWhiteSpace value) -> Some value
    | _ -> None

let load () : Result<Settings, string> =
  let path = configPath ()

  match tryReadFile path with
  | None ->
    writeTemplate path

    Error(
      sprintf
        "Created config template at %s. Fill it in, or set FIREBASE_URL, FIREBASE_SECRET, and TUIGETHER_USER."
        path
    )
  | Some file ->
    let firebaseUrl = resolve "FIREBASE_URL" file.FirebaseUrl
    let firebaseSecret = resolve "FIREBASE_SECRET" file.FirebaseSecret
    let tuigetherUser = resolve "TUIGETHER_USER" file.TuigetherUser

    match firebaseUrl, firebaseSecret, tuigetherUser with
    | Some url, Some secret, Some user ->
      Ok {
        FirebaseUrl = url
        FirebaseSecret = secret
        TuigetherUser = user
      }
    | _ ->
      Error(
        sprintf
          "Missing required config. Set values in %s or as FIREBASE_URL, FIREBASE_SECRET, and TUIGETHER_USER environment variables."
          path
      )
