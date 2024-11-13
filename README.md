# JackboxGPT

Because we wanted to use AI for party games instead of "useful" things.

This project is a Jackbox client controlled by GPT-3 (note: this is a pre-ChatGPT model). It currently supports these games:

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

- "Why GPT-3 specifically and not ChatGPT or another newer model?"
> Mostly because this project was created before ChatGPT existed, and also because I have a preference for the older models. Adding support for newer models as an option is something I'll probably look into in the future though.

- "How well does GPT-3 perform in Jackbox games?"
> For normal prompts/answers it does a pretty decent job (by my standards), giving a mix of answers in a range from simple/boring to wild/outlandish. Some of the games also make requests for voting on answers though, which the AI isn't any good at (newer models would be better for that particular use case).

- "How 'behaved' is GPT-3 in Jackbox games?"
> Currently the actual wordage of AI responses are sent to the game unfiltered (punctuation and formatting are cleaned up), so any garbage that GPT-3 might generate would come through into the game. I don't know how filtered the AI is on OpenAI's side, but rarely there are some really unfun answers that make their way into responses, so just be aware of that.

- "No releases?"
> Since I've been working on this mostly just for my own use there hasn't really been a need to make one. A new person running this project would already have to do extra setup (an OpenAI account with billing prepped and an API key) even if there was a prebuilt exe available, so it still wouldn't be super accessible.

- "Can the AI play without human players?"
> This is possible for any game that has "Start Game from Controller Only" option (Party Pack 3+).