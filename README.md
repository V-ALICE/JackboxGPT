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

## Usage

To run this you'll first need to [create an OpenAI API key](https://platform.openai.com/docs/quickstart#create-and-export-an-api-key) and add it to the included `.env` file (or alternatively as an environment variable named `OPENAI_API_KEY`).

To play a game, run `JackboxGPT.exe` and enter "Number of Instances" and "Room Code" when prompted. The executable can also be run with command line args as input, run with the `--help` option to see usage information.

## Adding Support for More Games

See [this guide](Extending.md) for some information on adding support for more games.

## FAQ

- "Why GPT-3 specifically and not ChatGPT or another newer model?"
> Mostly because this project was created before ChatGPT existed, and also because I have a preference for the older models. Adding support for newer models as an option is something I'll probably look into in the future though.

- "How well does GPT-3 perform in Jackbox games?"
> For normal prompts/answers it does a pretty decent job (by my standards), giving a mix of answers in a range from simple/boring to wild/outlandish. Some of the games also make requests for voting on answers though, which the AI isn't any good at (newer models would be better for that particular use case).

- "How 'behaved' is GPT-3 in Jackbox games?"
> Currently the actual wordage of AI responses are sent to the game unfiltered (punctuation and formatting are cleaned up), so any garbage that GPT-3 might generate would come through into the game. I don't know how filtered the AI is on OpenAI's side, but rarely there are some really unfun answers that make their way into responses, so just be aware of that.

- "Can the AI play without human players?"
> This is possible for any game that has "Start Game from Controller Only" option (Party Pack 3+).
