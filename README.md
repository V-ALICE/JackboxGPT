# JackboxGPT

Because everyone wants to use AI for party games instead of "useful" things, right?

This project is a Jackbox client controlled by OpenAI's GPT models. It currently supports these games:

- Blather 'Round
- Fibbage XL/2/3/4
- Joke Boat
- Quiplash XL/2/3 _(Party starter has support as well)_
- Survey Scramble _(all modes besides Bounce)_
- Survive the Internet
- Word Spud

## Usage

To run this you'll first need to [create an OpenAI API key](https://platform.openai.com/docs/quickstart#create-and-export-an-api-key) and add it to the included `.env` file (or alternatively as an environment variable named `OPENAI_API_KEY`).

To play a game, run `JackboxGPT.exe` and enter "Number of Instances" and "Room Code" when prompted. The executable can also be run with command line args as input, run with the `--help` option to see usage information.

## Adding Support for More Games

See [this guide](Extending.md) for some information on adding support for more games.

## FAQ

- "What model types are supported?"
> This project was created before ChatGPT existed, so the original implementation used Completion models only. It now supports Chat models as well, and there are options to use either type or a mix of both.

- "How well do the Chat models work in Jackbox games?"
> Pretty well, unsurprisingly. The one area they do stuggle in is variety. Since these model types are "smarter" they have a tendency to want to generate the same responses to equivalent prompts. This is especially prevalent in games with multiple AI players, as they may all end up generating the same or similar responses. There has been an effort to nudge the AI to try to minimize this, but it will still occur at least sometimes. In the future I may look into an 'overseer' module to track AI instances to make sure they don't duplicate answers, if configured to.

- "How well do the Completion models work in Jackbox games?"
> For normal prompts/answers they do a decent job for what they are, giving a mix of answers in a range from simple/boring to wild/outlandish. These models are less adept at things like voting on answers though, unsurprisingly, but it's possible to configure the program to use a Chat model for this type of prompt specifically. Please note that Completion models are somewhat more chaotic than Chat models, so rarely there will likely be really unfun answers that make their way into Completion responses.

- "Can the AI play without human players?"
> This is possible for any game that has "Start Game from Controller Only" option (Party Pack 3+).
