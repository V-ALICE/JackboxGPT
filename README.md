# JackboxGPT

Because everyone wants to use AI for party games instead of "useful" things, right?

This project is a Jackbox client controlled by OpenAI's GPT models. It currently supports these games:

- Fibbage XL/2/3/4
- Quiplash XL/2/3
- Blather 'Round
- Joke Boat
- Survey Scramble _(all modes besides Bounce)_
- Survive the Internet _(currently chooses images/votes randomly)_
- Word Spud _(currently always votes positively)_

## Playing

For now the only way to run JackboxGPT is to build it yourself (requires .NET 6.0). You'll also need to provide an OpenAI API key as an environment variable, either set or in a `.env` file, named `OPENAI_API_KEY`.

To play a game, simply run the compiled executable and enter "Number of Instances" and "Room Code" when prompted. The executable can also be run with command line args as input, run with the `--help` option to see usage information.

## Adding Support for More Games

See [this guide](Extending.md) for some information on adding more games.

## FAQ

- "What model types are supported?"
> This project was created before ChatGPT existed, so the original implementation used Completion models only. It now supports Chat models as well, and there are options to use either type or a mix of both.

- "How well do the Completion models work in Jackbox games?"
> For normal prompts/answers they do a decent job for what they are, giving a mix of answers in a range from simple/boring to wild/outlandish. These models are less adept at things like voting on answers though, unsurprisingly, but it's possible to configure things to use a Chat model for this type of prompt specifically. Please note that Completion models are somewhat more chaotic than Chat models, so rarely there are some really unfun answers that make their way into Completion responses.

- "Can the AI play without human players?"
> This is possible for any game that has "Start Game from Controller Only" option (Party Pack 3+).

- "No releases?"
> Since I've been working on this mostly just for my own use there hasn't really been a need to make one. Anyone downloading this project would already have to do extra setup (an OpenAI account with billing prepped and an API key) even if there was a prebuilt executable available, so it still wouldn't be super accessible. I'd still like to look into this eventually though.