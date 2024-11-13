using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JackboxGPT.Extensions;
using JackboxGPT.Games.BlatherRound;
using JackboxGPT.Games.BlatherRound.Models;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Engines
{
    public class BlatherRoundEngine : BaseJackboxEngine<BlatherRoundClient>
    {
        protected override string Tag => "blanky-blank";

        private readonly Random _random = new();
        private readonly List<string> _guessesUsedThisRound = new();
        private bool _writing;
        
        public BlatherRoundEngine(ICompletionService completionService, ILogger logger, BlatherRoundClient client, ManagedConfigFile configFile, int instance)
            : base(completionService, logger, client, configFile, instance)
        {
            JackboxClient.OnSelfUpdate += OnSelfUpdate;
            JackboxClient.OnWriteNewSentence += OnWriteNewSentence;
            JackboxClient.OnPlayerStartedPresenting += OnPlayerStartedPresenting;
            JackboxClient.OnNewSentence += OnNewSentence;
            JackboxClient.Connect();
        }

        private void OnNewSentence(object sender, string sentence)
        {
            if (JackboxClient.GameState.Self.State != PlayerState.EnterSingleText) return;
            SubmitGuess();
        }

        private void OnPlayerStartedPresenting(object sender, EventArgs e)
        {
            _guessesUsedThisRound.Clear();
        }

        private void OnSelfUpdate(object sender, Revision<BlatherRoundPlayer> revision)
        {
            if(revision.Old.State != revision.New.State && revision.New.State == PlayerState.MakeSingleChoice)
                ChoosePassword(revision.New);
            //else if (revision.New.State == PlayerState.EnterSingleText && revision.Old.EntryId != revision.New.EntryId)
            //    SubmitGuess();
        }

        private async void SubmitGuess()
        {
            var guesses = await ProvideGuesses();
            _guessesUsedThisRound.AddRange(guesses);

            foreach (var guess in guesses)
            {
                if (JackboxClient.GameState.Self.State != PlayerState.EnterSingleText) return;
                await Task.Delay(_random.Next(Config.BlatherRound.GuessDelayMinMs, Config.BlatherRound.GuessDelayMaxMs));
                JackboxClient.SubmitGuess(guess);
                LogInfo($"Guessing: {guess}");
            }
        }

        private void ChoosePassword(BlatherRoundPlayer self)
        {
            _guessesUsedThisRound.Clear();
            var choice = self.Choices.Where(c => c.ClassName != "refresh").ToList().RandomIndex();
            JackboxClient.ChoosePassword(choice);

            var nonPasswordEndings = new[] { "Skip Tutorials", "My Bad", "blew it!" };
            if (nonPasswordEndings.Any(check => self.Choices[choice].Html.EndsWith(check)))
                LogVerbose($"Selecting option: {self.Choices[choice].Html}");
            else
                LogInfo($"Password is: {self.Choices[choice].Html}");
        }

        private async void OnWriteNewSentence(object sender, Sentence sentence)
        {
            if (_writing) return;
            await Task.Delay(Config.BlatherRound.SentenceDelayMs);

            _writing = true;
            
            while (true)
            {
                var sentenceResult = await WriteSentence();

                if (sentenceResult == SentenceResult.Skip)
                {
                    JackboxClient.SubmitSentence(true);
                    await Task.Delay(Config.BlatherRound.SkipDelayMs);
                }
                else if (sentenceResult == SentenceResult.Submit)
                {
                    JackboxClient.SubmitSentence();
                    break;
                }
                else if (sentenceResult == SentenceResult.DoNothing)
                {
                    break;
                }
            }

            _writing = false;
        }

        private async Task<SentenceResult> WriteSentence()
        {
            if (JackboxClient.CurrentSentence == null || JackboxClient.GameState.Self.Prompt.Html == null) return SentenceResult.DoNothing;

            var parts = JackboxClient.CurrentSentence.Parts;
            var prompt = GetCleanPrompt();
            
            LogVerbose($"Prompt is {prompt}.");

            if (JackboxClient.CurrentSentence.Type != SentenceType.Response)
            {
                // Randomly decide which order to select the parts in
                if (_random.Next(0, 2) == 0)
                {
                    LogVerbose("Processing parts from last to first.");
                    for (var index = parts.Count - 1; index >= 0; index--)
                        if (!await ProcessPart(prompt, parts, index, PartOrder.Before))
                            return SentenceResult.Skip;
                }
                else
                {
                    LogVerbose("Processing parts from first to last.");
                    for (var index = 0; index < parts.Count; index++)
                        if (!await ProcessPart(prompt, parts, index))
                            return SentenceResult.Skip;
                }
            }
            else
            {
                var unusedChoices = parts[1].Choices.Where(choice => !_guessesUsedThisRound.Contains(choice)).ToList();

                if (unusedChoices.Count == 0)
                    return SentenceResult.Skip;

                var results = await CompletionService.SemanticSearch(prompt, unusedChoices);
                if (results.Count == 0)
                    return SentenceResult.Skip;

                results.Sort((a, b) => (int)(b.Score - a.Score));
                
                foreach (var result in results)
                    LogVerbose($"{unusedChoices[result.Index]}: {result.Score}");
                
                // Randomly decide whether to check best or worst performers
                if (_random.Next(0, 2) == 0 && results[0].Score >= 50)
                {
                    var qualifierIndex = -1;
                    foreach (var qualifier in Sentence.PHRASES_SIMILAR_TO)
                    {
                        qualifierIndex = parts[0].Choices.IndexOf(qualifier);
                        if (qualifierIndex != -1) break;
                    }
                    
                    JackboxClient.ChooseWord(0, qualifierIndex);
                    await Task.Delay(Config.BlatherRound.WordDelayMs);
                    
                    _guessesUsedThisRound.Add(unusedChoices[results[0].Index]);
                    
                    JackboxClient.ChooseWord(1, parts[1].Choices.IndexOf(unusedChoices[results[0].Index]));
                }
                else if(results.Last().Score <= -20)
                {
                    var qualifierIndex = -1;
                    foreach (var qualifier in Sentence.PHRASES_DISSIMILAR_FROM)
                    {
                        qualifierIndex = parts[0].Choices.IndexOf(qualifier);
                        if (qualifierIndex != -1) break;
                    }
                    
                    JackboxClient.ChooseWord(0, qualifierIndex);
                    await Task.Delay(Config.BlatherRound.WordDelayMs);
                    
                    _guessesUsedThisRound.Add(unusedChoices[results.Last().Index]);
                    
                    JackboxClient.ChooseWord(1, parts[1].Choices.IndexOf(unusedChoices[results.Last().Index]));
                }
                else
                    return SentenceResult.Skip;

                await Task.Delay(Config.BlatherRound.WordDelayMs);
            }

            return SentenceResult.Submit;
        }

        private async Task<bool> ProcessPart(string prompt, IList<SentencePart> parts, int index, PartOrder order = PartOrder.After)
        {
            var part = parts[index];
            if (!part.ShouldChoose) return true;
            
            var chosenWords = string.Join(' ', JackboxClient.CurrentSentence.ChosenWords);
            
            if (chosenWords.Length > 0)
                LogDebug($"chosenWords: {chosenWords}");

            var newChoices =
                order == PartOrder.After ?
                    part.Choices.Select(c => $"{chosenWords} {c}".Trim()).ToList() :
                    part.Choices.Select(c => $"{c} {chosenWords}".Trim()).ToList();
        
            var results = await CompletionService.SemanticSearch(prompt, newChoices);
            if (results.Count == 0)
                return false;

            results.Sort((a, b) => (int)(b.Score - a.Score));

            foreach (var result in results)
                LogVerbose($"{newChoices[result.Index]}: {result.Score}");
        
            // Top performers are the results within 20 points of the top performing result.
            // Will be chosen randomly.
            var topPerformers = results.Where(r => results[0].Score - r.Score <= 10).ToList();
            
            if (JackboxClient.CurrentSentence.Type != SentenceType.Writing && chosenWords == "" && results[0].Score < 10)
            {
                // not confident enough; skip
                return false;
            }

            // choose from top 3
            var chosenWord = topPerformers[topPerformers.RandomIndex()].Index;
            JackboxClient.ChooseWord(index, chosenWord);
                
            await Task.Delay(Config.BlatherRound.PartDelayMs);
            return true;
        }

        private async Task<IList<string>> ProvideGuesses()
        {
            /*
I was given a list of sentences to describe a thing:

It's a neat-o rectangle object.
It is the gadget.
Talk about silvery!
It has the knob.
Oh, line art!
Quite simply, it's a silvery art.
Talk about plasticky!
It makes the art.
Oh, childhood!

My guess: Etch-a-Sketch
###
             */
            
            var prompt =
                $@"A list of sentences to describe a place:

It's a fantastic food place.
It's where you have the guilty pleasure.
So much din-din!
It's where you delight in the wrap.
It's a spicy food.

Guesses: Taco Bell; Wendy's; restaurant; McDonald's; Subway
###
A list of sentences to describe a {JackboxClient.CurrentCategory}:

{string.Join('\n', JackboxClient.CurrentSentences)}

Guesses:";
            
            LogVerbose($"GPT-3 Prompt: {prompt}");
            
            var result = await CompletionService.CompletePrompt(
                prompt,
                new ICompletionService.CompletionParameters
                {
                    Temperature = Config.BlatherRound.GenTemp,
                    MaxTokens = 64,
                    TopP = 1,
                    FrequencyPenalty = 0.3,
                    PresencePenalty = 0.2,
                    StopSequences = new[] { "\n", "###" }
                },
                completion => completion.Text.Trim().Split(";").Select(CleanAnswer).Where(answer =>
                    answer != "" && answer.Length <= 40 && !_guessesUsedThisRound.Contains(answer)).ToList(),
                new List<string>(),
                maxTries: Config.BlatherRound.MaxRetries
            );

            return result;
        }

        private string GetCleanPrompt()
        {
            return JackboxClient.GameState.Self.Prompt.Html.Replace("Describe ", "");
        }

        private static string CleanAnswer(string answer)
        {
            answer = answer.Trim();
            var articlesRegex = new Regex("^(a|an|the) ", RegexOptions.IgnoreCase);
            answer = articlesRegex.Replace(answer, "").Trim();
            return answer.TrimEnd('.').TrimQuotes();
        }
    }

    internal enum SentenceResult
    {
        Skip,
        Submit,
        DoNothing
    }

    internal enum PartOrder
    {
        Before,
        After
    }
}