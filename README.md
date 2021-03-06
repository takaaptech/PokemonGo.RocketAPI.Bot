# Pokemon Go API Client Library #


## About

This is my improved version of the latest version of the Pokemon Go RocketAPI made by FeroxRev.
Feel free to add pull requests, but don't expect too much from me solving problems as I am not really a c# developer.

Link to Discord Server: https://discord.gg/v8MDe9M



## Features


```
#PTC Login / Google
#Get Map Objects and Inventory
#Search for gyms/pokestops/spawns
#Farm pokestops
#Farm all pokemons in neighbourhood and throw berries/use best pokeball
#Transfers duplicate pokemons
#Evolve all pokemons
#Throws away unneeded items
#Humanlike Walking
#Output Username, Level, Stardust, XP/hour, Pokemon/hour in Console Title

```

## Setting it up
Note: You need some basic Computer Expierience, if you need help somewhere, ask the community and do not spam us via private messages. **The Issue Tracker is not for help!**


1. Download and Install [Visual Studio 2015](https://go.microsoft.com/fwlink/?LinkId=691979&clcid=0x407)
2. Download [this Repository](https://github.com/VWPole/PokemonGo.RocketAPI.Bot/archive/master.zip)
3. Open Pokemon Go Rocket API.sln
4. On the right hand side, double click on UserSettings.settings
5. Enter the DefaultLatitude and DefaultLongitude [can be found here](http://mondeca.com/index.php/en/any-place-en)
6. Select the AuthType (Google or Ptc for Pokémon Trainer Club)
7. If selected Ptc , enter the Username and Password of your Account
8. Right click on PokemonGo.RocketAPI.Console and Set it as Startup Project
9. Press CTRL + F5 and follow the Instructions
10. Have fun! 
11. In Settings.cs you can change how much of which item should be kept, which pokemons should be evolved, and which should not be transferred

## License
This Project is licensed as GNU (GNU GENERAL PUBLIC LICENSE v3) 

You can find all necessary Information [here] (https://github.com/VWPole/PokemonGo.RocketAPI.Bot/blob/master/LICENSE.md)


# Pokemon-Go-Rocket-API
Note: To use the API you have to be an experienced Developer, do not touch or modify Project Files if you are not experienced.

Example:

```
<<<<<<< HEAD
var clientSettings = new Settings();
var client = new Client(_clientSettings);
=======

var client = new Client(new Settings()); //Define your own ISettings implementation
>>>>>>> c1cf16ad226d77ad1a14c0fc527eb2f14f91c69e
await _client.DoGoogleLogin();
await _client.SetServer();
var profile = await _client.GetProfile();
var settings = await _client.GetSettings();
var mapObjects = await _client.GetMapObjects();
<<<<<<< HEAD

//Logic related methods, which you could also build yourself
await EvolveAllPokemonWithEnoughCandy();
await TransferDuplicatePokemon();
await RecycleItems();
await ExecuteFarmingPokestopsAndPokemons();
=======
>>>>>>> c1cf16ad226d77ad1a14c0fc527eb2f14f91c69e
```
#   P o k e m o n G o . R o c k e t A P I . B o t  
 