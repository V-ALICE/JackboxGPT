# Extending JackboxGPT

If you wish to add support for a new game, here are some rudimentary instructions:

1. Make sure it's actually feasible for the game you're interested in to be played this way
1. Get JSON log(s) of the game you're planning to add (browser console logging is your friend)
1. Run the template generator (written in python) with the name of the game (e.g. `JokeBoat`) and the directory where your logs for this specific game are (e.g. `F:/Logs/JokeBoat`)
    - Usage: `jb_api_gen.py <game_name> <input_folder> [enum_name...]`
    - If you know any field names that are equivalent to enums you include them at the end of the python call. Common ones are `state`, `lobbyState`, `choiceType`, `context`, `kind`
    - Example Usage: `jb_api_gen.py JokeBoat F:/Logs/jackbox/jokeboat state lobbyState choiceType`
1. Set the tag in the generated Engine file to the correct app tag. This tag is similar to the game name but sometimes not the same
    - This will usually be in the JSON logs you already have, but you can also find a list online (for example, [here](https://github.com/smpial/jackbox-private-server/blob/main/games.json))
1. In `Startup.cs` register the new Client and Engine in the `RegisterGameEngines` function using the same tag you entered in the engine
1. Fill out the Engine, Client, and any extra needed Models (aka "Draw the rest of the f\*\*\*ing owl")
    - This is a fairly manual and undocumented process at the moment, but there are some extra details below

## Components

### Client

The Client abstracts the communication between the Jackbox server into a nice API that can be consumed by the Engine.

You will need to do some reverse engineering work to understand the ins and outs of how each game communicates. However, all Jackbox games share at least some similarities: the raw WebSocket data is sent in a consistent JSON structure, including a sequence identifier, opcode, and some body. The `BaseJackboxClient` will deserialize this, convert it to the corresponding `Player` and `Room` Models, and send it to your Engine's `OnSelfUpdate` and `OnRoomUpdate` methods for further processing.

The API you create will reflect actions a player would take in a game. For example, the Fibbage clients have a method `SubmitLie` which will submit a lie that answers the prompt.

### Models

These Models are C# structures representing the JSON messages being sent and received from the Jackbox servers.

The main Player and Room models will be generated, but there may be other supplementary models that are needed (e.g. models of messages that get sent back to the server).

### Engine

The Engine is what uses the Client API you defined above and uses GPT-3 (or any other service provided via `ICompletionService`) to play the game.

With `ICompletionService` you can provide the prompt, any GPT-3 parameters you want, a set of conditions for a valid response, and how many times it should try to get a valid response before giving up. You can find examples in `src/Engines/BaseFibbageEngine.cs`, such as within the function `ProvideLie`.
