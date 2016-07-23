using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using AllEnum;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Exceptions;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.GeneratedCode;
using PokemonGo.RocketAPI.Logic.Utils;

namespace PokemonGo.RocketAPI.Logic
{
    public class Logic
    {
        private readonly Client _client;
        private readonly ISettings _clientSettings;
        private readonly Inventory _inventory;
        private readonly Navigation _navigation;
        private static DateTime TimeStarted = DateTime.Now;
        private static int TotalExperience = 0;
        private static int TotalPokemon = 0;

        public Logic(ISettings clientSettings)
        {
            _clientSettings = clientSettings;
            _client = new Client(_clientSettings);
            _inventory = new Inventory(_client);
            _navigation = new Navigation(_client);
        }

        public async Task Execute()
        {
            Logger.Write($"Starting Execute on login server: {_clientSettings.AuthType}", LogLevel.Info);

            while (true)
            {
                try
                {
                    if (_clientSettings.AuthType == AuthType.Ptc)
                        await _client.DoPtcLogin(_clientSettings.PtcUsername, _clientSettings.PtcPassword);
                    else if (_clientSettings.AuthType == AuthType.Google)
                        await _client.DoGoogleLogin();

                    await _client.SetServer();

                    await PostLoginExecute();
                }
                catch (AccessTokenExpiredException)
                {
                    Logger.Write($"Access token expired", LogLevel.Info);
                }
                await Task.Delay(10000);
            }
        }

        public async Task PostLoginExecute()
        {
            while (true)
            {
                try
                {
                    ConsoleLevelTitle("Trainer", _client);
                    await EvolveAllPokemonWithEnoughCandy();
                    await TransferDuplicatePokemon();
                    await RecycleItems();
                    await ExecuteFarmingPokestopsAndPokemons();

                    /*
            * Example calls below
            *
            var profile = await _client.GetProfile();
            var settings = await _client.GetSettings();
            var mapObjects = await _client.GetMapObjects();
            var inventory = await _client.GetInventory();
            var pokemons = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.Pokemon).Where(p => p != null && p?.PokemonId > 0);
            */
                }
                catch (AccessTokenExpiredException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Write($"Exception: {ex}", LogLevel.Error);
                }

                await Task.Delay(10000);
            }
        }

        public static float CalculatePokemonPerfection(PokemonData poke)
        {
            return ((float)(poke.IndividualAttack*2 + poke.IndividualDefense + poke.IndividualStamina) / (4.0f * 15.0f)) * 100.0f;
        }

        public async Task RepeatAction(int repeat, Func<Task> action)
        {
            for (int i = 0; i < repeat; i++)
                await action();
        }

        
        private async Task ExecuteFarmingPokestopsAndPokemons()
        {
            var mapObjects = await _client.GetMapObjects();

            var pokeStops = mapObjects.MapCells.SelectMany(i => i.Forts).Where(i => i.Type == FortType.Checkpoint && i.CooldownCompleteTimestampMs < DateTime.UtcNow.ToUnixTime()).OrderBy(i => LocationUtils.CalculateDistanceInMeters(new Navigation.Location(_client.CurrentLat, _client.CurrentLng), new Navigation.Location(i.Latitude, i.Longitude)));

            foreach (var pokeStop in pokeStops)
            {
                var distance = Navigation.DistanceBetween2Coordinates(_client.CurrentLat, _client.CurrentLng, pokeStop.Latitude, pokeStop.Longitude);

                var update = await _navigation.HumanLikeWalking(new Navigation.Location(pokeStop.Latitude, pokeStop.Longitude), _clientSettings.WalkingSpeedInKilometerPerHour);

                var fortInfo = await _client.GetFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                var fortSearch = await _client.SearchFort(pokeStop.Id, pokeStop.Latitude, pokeStop.Longitude);
                Logger.Write($"Using Pokestop: {fortInfo.Name} in {Math.Round(distance)}m distance");
                Logger.Write($"Farmed XP: {fortSearch.ExperienceAwarded}, Gems: { fortSearch.GemsAwarded}, Eggs: {fortSearch.PokemonDataEgg} Items: {StringUtils.GetSummedFriendlyNameOfItemAwardList(fortSearch.ItemsAwarded)}", LogLevel.Info);

                await Task.Delay(1000);
                await RecycleItems();
                await ExecuteCatchAllNearbyPokemons();
                await TransferDuplicatePokemon();
            }
        }

        private async Task ExecuteCatchAllNearbyPokemons()
        {
            var mapObjects = await _client.GetMapObjects();

            var pokemons = mapObjects.MapCells.SelectMany(i => i.CatchablePokemons).OrderBy(i => LocationUtils.CalculateDistanceInMeters(new Navigation.Location(_client.CurrentLat, _client.CurrentLng), new Navigation.Location(i.Latitude, i.Longitude)));

            foreach (var pokemon in pokemons)
            {
                var distance = Navigation.DistanceBetween2Coordinates(_client.CurrentLat, _client.CurrentLng, pokemon.Latitude, pokemon.Longitude);
                await Task.Delay(distance > 100 ? 15000 : 500);

                var encounter = await _client.EncounterPokemon(pokemon.EncounterId, pokemon.SpawnpointId);

                if (encounter.Status == EncounterResponse.Types.Status.EncounterSuccess)
                await CatchEncounter(encounter, pokemon);
                else
                    Logger.Write($"Encounter problem: {encounter?.Status}");
            }
                await Task.Delay(15000);
            }

        private async Task CatchEncounter(EncounterResponse encounter, MapPokemon pokemon)
        {

            CatchPokemonResponse caughtPokemonResponse;
            do
            {
                var probability = encounter?.CaptureProbability?.CaptureProbability_?.FirstOrDefault();
                if (probability.HasValue && probability.Value < 0.25)
                {
                    //Throw berry is we can
                    await UseBerry(pokemon.EncounterId, pokemon.SpawnpointId);
                }

                var pokeball = await GetBestBall(encounter?.WildPokemon);
                var distance = Navigation.DistanceBetween2Coordinates(_client.CurrentLat, _client.CurrentLng, pokemon.Latitude, pokemon.Longitude);
                caughtPokemonResponse = await _client.CatchPokemon(pokemon.EncounterId, pokemon.SpawnpointId, pokemon.Latitude, pokemon.Longitude, pokeball);
                if (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
                {
                    foreach (int xp in caughtPokemonResponse.Scores.Xp)
                        TotalExperience += xp;
                    TotalPokemon += 1;
                }
                Logger.Write(caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess ? $"We caught a {pokemon.PokemonId} with CP {encounter?.WildPokemon?.PokemonData?.Cp} ({CalculatePokemonPerfection(encounter?.WildPokemon?.PokemonData).ToString("0.00")}% perfect) and CaptureProbability: {encounter?.CaptureProbability.CaptureProbability_.First()} using a {pokeball} in {Math.Round(distance)}m distance" : $"{pokemon.PokemonId} with CP {encounter?.WildPokemon?.PokemonData?.Cp} CaptureProbability: {encounter?.CaptureProbability.CaptureProbability_.First()} in {Math.Round(distance)}m distance {caughtPokemonResponse.Status} while using a {pokeball}..", LogLevel.Info);
                await Task.Delay(2000);
            }
            while (caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed || caughtPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchEscape) ;
        }
        
        private async Task EvolveAllPokemonWithEnoughCandy(IEnumerable<PokemonId> filter = null)
        {
            var pokemonToEvolve = await _inventory.GetPokemonToEvolve(filter);
            foreach (var pokemon in pokemonToEvolve)
            {
                var evolvePokemonOutProto = await _client.EvolvePokemon((ulong)pokemon.Id);

                if (evolvePokemonOutProto.Result == EvolvePokemonOut.Types.EvolvePokemonStatus.PokemonEvolvedSuccess)
                    Logger.Write($"Evolved {pokemon.PokemonId} successfully for {evolvePokemonOutProto.ExpAwarded}xp", LogLevel.Info);
                else
                        Logger.Write($"Failed to evolve {pokemon.PokemonId}. EvolvePokemonOutProto.Result was {evolvePokemonOutProto.Result}, stopping evolving {pokemon.PokemonId}", LogLevel.Info);
                    
                await Task.Delay(3000);
            }
        }

        private async Task TransferDuplicatePokemon(bool keepPokemonsThatCanEvolve = false)
        {
            var duplicatePokemons = await _inventory.GetDuplicatePokemonToTransfer(keepPokemonsThatCanEvolve, _clientSettings.PokemonsNotToTransfer);

            foreach (var duplicatePokemon in duplicatePokemons)
            {
                if (CalculatePokemonPerfection(duplicatePokemon) >= _clientSettings.KeepMinIVPercentage || duplicatePokemon.Cp > _clientSettings.KeepMinCP)
                    continue;
                
                var transfer = await _client.TransferPokemon(duplicatePokemon.Id);
                var bestPokemonOfType = await _inventory.GetHighestCPofType(duplicatePokemon);
                Logger.Write($"Transfer {duplicatePokemon.PokemonId} with {duplicatePokemon.Cp} CP (Best: {bestPokemonOfType})", LogLevel.Info);
                await Task.Delay(500);
            }
        }

        private async Task RecycleItems()
        {
            var items = await _inventory.GetItemsToRecycle(_clientSettings);

            foreach (var item in items)
            {
                var transfer = await _client.RecycleItem((AllEnum.ItemId)item.Item_, item.Count);
                Logger.Write($"Recycled {item.Count}x {(AllEnum.ItemId)item.Item_}", LogLevel.Info);
                await Task.Delay(500);
            }
        }

        private async Task<MiscEnums.Item> GetBestBall(WildPokemon pokemon)
        {
            var pokemonCp = pokemon?.PokemonData?.Cp;

            var pokeBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_POKE_BALL);
            var greatBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_GREAT_BALL);
            var ultraBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_ULTRA_BALL);
            var masterBallsCount = await _inventory.GetItemAmountByType(MiscEnums.Item.ITEM_MASTER_BALL);

            if (masterBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_MASTER_BALL;
            else if (ultraBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            else if (greatBallsCount > 0 && pokemonCp >= 1000)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (ultraBallsCount > 0 && pokemonCp >= 750)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            else if (greatBallsCount > 0 && pokemonCp >= 750)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (greatBallsCount > 0 && pokemonCp >= 350)
                return MiscEnums.Item.ITEM_GREAT_BALL;

            if (pokeBallsCount > 0)
                return MiscEnums.Item.ITEM_POKE_BALL;
            if (greatBallsCount > 0)
                return MiscEnums.Item.ITEM_GREAT_BALL;
            if (ultraBallsCount > 0)
                return MiscEnums.Item.ITEM_ULTRA_BALL;
            if (masterBallsCount > 0)
                return MiscEnums.Item.ITEM_MASTER_BALL;

            return MiscEnums.Item.ITEM_POKE_BALL;
        }

        public async Task UseBerry(ulong encounterId, string spawnPointId)
        {
            var inventoryBalls = await _inventory.GetItems();
            var berries = inventoryBalls.Where(p => (ItemId) p.Item_ == ItemId.ItemRazzBerry);
            var berry = berries.FirstOrDefault();

            if (berry == null)
                return;
            
            var useRaspberry = await _client.UseCaptureItem(encounterId, AllEnum.ItemId.ItemRazzBerry, spawnPointId);
            Logger.Write($"Use Rasperry. Remaining: {berry.Count}", LogLevel.Info);
            await Task.Delay(3000);
        }

        public static async Task ConsoleLevelTitle(string Username, Client _client)
        {
            var inventory = await _client.GetInventory();
            var stats = inventory.InventoryDelta.InventoryItems.Select(i => i.InventoryItemData?.PlayerStats).ToArray();
            var profile = await _client.GetProfile();
            foreach (var v in stats)
                if (v != null)
                {
                    int XpDiff = GetXpDiff(_client, v.Level);
                    System.Console.Title = string.Format(Username + " | Level: {0:0} - ({1:0} / {2:0}) | Stardust: {3:0}", v.Level, (v.Experience - v.PrevLevelXp - XpDiff), (v.NextLevelXp - v.PrevLevelXp - XpDiff), profile.Profile.Currency.ToArray()[1].Amount) + " | XP/Hour: " + Math.Round(TotalExperience / GetRuntime()) + " | Pokemon/Hour: " + Math.Round(TotalPokemon / GetRuntime());
                }
            await Task.Delay(1000);
            ConsoleLevelTitle(Username, _client);
        }
        public static double GetRuntime()
        {
            return ((DateTime.Now - TimeStarted).TotalSeconds) / 3600;
        }

        public static int GetXpDiff(Client client, int Level)
        {
            switch (Level)
            {
                case 1:
                    return 0;
                case 2:
                    return 1000;
                case 3:
                    return 2000;
                case 4:
                    return 3000;
                case 5:
                    return 4000;
                case 6:
                    return 5000;
                case 7:
                    return 6000;
                case 8:
                    return 7000;
                case 9:
                    return 8000;
                case 10:
                    return 9000;
                case 11:
                    return 10000;
                case 12:
                    return 10000;
                case 13:
                    return 10000;
                case 14:
                    return 10000;
                case 15:
                    return 15000;
                case 16:
                    return 20000;
                case 17:
                    return 20000;
                case 18:
                    return 20000;
                case 19:
                    return 25000;
                case 20:
                    return 25000;
                case 21:
                    return 50000;
                case 22:
                    return 75000;
                case 23:
                    return 100000;
                case 24:
                    return 125000;
                case 25:
                    return 150000;
                case 26:
                    return 190000;
                case 27:
                    return 200000;
                case 28:
                    return 250000;
                case 29:
                    return 300000;
                case 30:
                    return 350000;
                case 31:
                    return 500000;
                case 32:
                    return 500000;
                case 33:
                    return 750000;
                case 34:
                    return 1000000;
                case 35:
                    return 1250000;
                case 36:
                    return 1500000;
                case 37:
                    return 2000000;
                case 38:
                    return 2500000;
                case 39:
                    return 1000000;
                case 40:
                    return 1000000;
            }
            return 0;
        }
    }
}
