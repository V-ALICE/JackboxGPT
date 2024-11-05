using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using JackboxGPT.Extensions;
using JackboxGPT.Games.Common.Models;
using JackboxGPT.Games.Quiplash3;
using JackboxGPT.Games.Quiplash3.Models;
using JackboxGPT.Services;
using Serilog;

namespace JackboxGPT.Engines
{
    public class Quiplash3Engine : BaseQuiplashEngine<Quiplash3Client>
    {
        protected override string Tag => "quiplash3";

        private bool _selectedAvatar;

        public Quiplash3Engine(ICompletionService completionService, ILogger logger, Quiplash3Client client, ManagedConfigFile configFile, int instance)
            : base(completionService, logger, client, configFile, instance)
        {
            JackboxClient.OnRoomUpdate += OnRoomUpdate;
            JackboxClient.OnSelfUpdate += OnSelfUpdate;
            JackboxClient.Connect();
        }

        private void OnSelfUpdate(object sender, Revision<Quiplash3Player> revision)
        {
            if (revision.Old.State != revision.New.State)
                LogDebug($"New room state: {revision.New.State}", true);

            switch (revision.New.State)
            {
                case RoomState.EnterSingleText:
                    SubmitQuip(revision.New);
                    break;
                case RoomState.EnterTextList when revision.New.Entries == false:
                    SubmitThrip(revision.New);
                    break;
                case RoomState.MakeSingleChoice when revision.New.ChoiceType == ChoiceType.ChoseQuip:
                    VoteQuip(revision.New);
                    break;
                case RoomState.MakeSingleChoice when revision.New.ChoiceType == ChoiceType.ChooseQuip:
                    VoteThrip(revision.New);
                    break;
            }
        }

        private void OnRoomUpdate(object sender, Revision<Quiplash3Room> room)
        {
            if (_selectedAvatar) return;
            
            _selectedAvatar = true;
            var availableChars = room.New.Characters.Where(c => c.Available).ToList();
            var charIndex = availableChars.RandomIndex();
            JackboxClient.ChooseCharacter(availableChars[charIndex].Name);
        }

        private async void VoteQuip(Quiplash3Player self)
        {
            if (self.Prompt.Html == null) return;

            var quips = self.Choices.Select(choice => choice.Html).ToList();
            var favorite = await FormVote(self.Prompt.Html, quips);
            JackboxClient.ChooseFavorite(favorite);
        }
        
        private async void VoteThrip(Quiplash3Player self)
        {
            if (self.Prompt.Html == null) return;
            var prompt = CleanPromptForSelection(self.Prompt.Html);
            if (prompt == "") return;

            var thrips = self.Choices.Select(choice => CleanThripForSelection(choice.Html)).ToList();
            
            var favorite = await ProvideFavorite(prompt, thrips);
            LogDebug($"Choosing \"{thrips[favorite]}\"");
            JackboxClient.ChooseFavorite(favorite);
        }

        private async void SubmitQuip(Quiplash3Player self)
        {
            if (self.Prompt.Html == null) return;

            var quip = await FormQuip(self.Prompt.Html);
            if (quip == "")
            {
                LogInfo("GPT-3 failed to come up with a response, so a Safety Quip will be used");
                JackboxClient.RequestSafetyQuip();
            }
            else
            {
                JackboxClient.SubmitQuip(quip);
            }
        }

        private async void SubmitThrip(Quiplash3Player self)
        {
            if (self.Prompt.Html == null) return;
            var prompt = CleanPromptForEntry(self.Prompt.Html);
            if (prompt == "") return;
            
            var quip = await ProvideThrip(prompt);
            LogInfo($"GPT responded to \"{prompt}\" with \"{quip.Replace("\n", " | ")}\"");
            JackboxClient.SubmitQuip(quip);
        }
        
        private async Task<string> ProvideThrip(string qlPrompt, int maxLength = 45)
        {
            var prompt = $@"In the third round of the game Quiplash, players must take a prompt and give three different short answers that make sense, separated by a | character.

Question: Three things every good orgy has
Funny Answer: scented oils|a non-disclosure agreement|disgraced politician

Question: The only three things that can bring true happiness
Funny Answer: money|sex|a long hug

Question: The three things you must do to survive a zombie apocalypse
Funny Answer: hunker down|play video games|hope this all blows over

Question: {qlPrompt}
Funny Answer:";
            
            var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
            {
                Temperature = Config.Quiplash.GenTemp,
                MaxTokens = 32,
                TopP = 1,
                FrequencyPenalty = 0.2,
                PresencePenalty = 0.1,
                StopSequences = new[] { "\n" }
            }, 
            completion =>
            {
                if (completion.Text.Split("|").Length == 3 && !completion.Text.Contains("__") && completion.Text.Length <= 3*maxLength) 
                    return true;

                LogDebug($"Received unusable ProvideThrip response: {completion.Text.Trim()}");
                return false;
            },
            maxTries: Config.Quiplash.MaxRetries,
            defaultResponse: "GPT has failed|to formulate|an answer");

            var split = result.Text.Split("|");
            var output = split.Aggregate("", (current, part) => current + CleanResult(part.Trim()) + "\n");
            return output.Trim();
        }

        #region Prompt Cleanup
        protected override string CleanPromptForEntry(string prompt)
        {
            prompt = prompt.ToLower();
            
            var doc = new XmlDocument();
            doc.LoadXml($"<root>{prompt}</root>");
            return doc.FirstChild?.ChildNodes[1]?.InnerText.Trim() ?? "";
        }

        protected override string CleanPromptForSelection(string prompt)
        {
            prompt = prompt.ToLower();
            return Regex.Replace(prompt, "<br \\/>.+", string.Empty).Trim();
        }

        protected override string CleanQuipForSelection(string quip)
        {
            quip = quip.ToLower();
            
            var doc = new XmlDocument();
            doc.LoadXml($"<root>{quip}</root>");
            return doc.InnerText.Trim();
        }

        protected static string CleanThripForSelection(string thrip)
        {
            thrip = thrip.ToLower();
            
            var doc = new XmlDocument();
            doc.LoadXml($"<root>{thrip}</root>");
            
            var quips = doc.FirstChild?.ChildNodes
                .Cast<XmlNode>()
                .Select(node => node.InnerText);

            return string.Join("|", quips ?? throw new InvalidOperationException()).Trim();
        }
        #endregion
    }
}
