# JackboxGPT

Because everyone wants to use AI for party games instead of "useful" things, right?

This project is a Jackbox client controlled by OpenAI's GPT models. It currently supports these games:

- Blather 'Round
- Bracketeering
- Fibbage XL/2/3/4
- Joke Boat
- Quiplash XL/2/3
- Survey Scramble _(all modes besides Bounce)_
- Survive the Internet
- Word Spud

## Usage

To run this you'll first need to [create an OpenAI API key](https://platform.openai.com/docs/quickstart#create-and-export-an-api-key) and add it to an `.env` file (or alternatively as an environment variable named `OPENAI_API_KEY`).

You'll also need .NET Runtime 8.0 present. You can either [download/install](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) this, or just use [Docker](https://docs.docker.com/engine/install/) along these lines:
```
docker run -ti --rm -w /app --name JackboxGPT -v </path/to/JackboxGPT/folder>:/app mcr.microsoft.com/dotnet/runtime:8.0 ./JackboxGPT
```

To play a game, run `JackboxGPT.exe` and enter "Number of Instances" and "Room Code" when prompted. The executable can also be run with command line args as input, run with the `--help` option to see usage information.

## Adding Support for More Games

See [this guide](Extending.md) for some information on adding support for more games.

## FAQ

- "What model types are supported?"
> This project was created before ChatGPT existed, so the original implementation used Completion models only. It now supports Chat models as well, and there are options to use either type or a mix of both.

- "How well does this work in Jackbox games?"
> Decently, by my standards. Chat models can struggle with variety, especially when multiple AI players are in the same game, and Completion models can struggle generally. Mitigations have been put in to try to make the AI less overpowered and less of a hivemind.

- "Can the AI play without human players?"
> This is possible for any game that has "Start Game from Controller Only" option (Party Pack 3+).

- "Anything else a user should know?"
> OpenAI models are usually fairly tame, but they're still very capable of generating horrible responses. Occasionally the AI can and will produce really unfun responses, so please keep that in mind.

- "Add support for X or Y game?"
> Maybe!
