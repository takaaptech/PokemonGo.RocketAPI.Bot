@ECHO OFF
:start
ping -n 11 127.0.0.1 > nul
start PokemonGo.RocketAPI.Console.exe
ping -n 1801 127.0.0.1 > nul
TASKKILL /F /IM PokemonGo.RocketAPI.Console.exe
goto start
PAUSE