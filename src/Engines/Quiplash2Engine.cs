using System.Linq;
using System.Threading.Tasks;
using JackboxGPT3.Games.Common;
using JackboxGPT3.Games.Common.Models;
using JackboxGPT3.Games.Quiplash2;
using JackboxGPT3.Games.Quiplash2.Models;
using JackboxGPT3.Services;
using Serilog;
using RoomState = JackboxGPT3.Games.Quiplash2.Models.RoomState;

namespace JackboxGPT3.Engines
{
    public class Quiplash2Engine : BaseQuiplashEngine<Quiplash2Client>
    {
        protected override string Tag => "quiplash2";

        private readonly ImageDescriptionProvider _descriptionProvider;

        public Quiplash2Engine(ICompletionService completionService, ILogger logger, Quiplash2Client client, int instance)
            : base(completionService, logger, client, instance)
        {
            _descriptionProvider = new ImageDescriptionProvider("ql2_comic_descriptions.json");

            JackboxClient.OnSelfUpdate += OnSelfUpdate;
            JackboxClient.OnRoomUpdate += OnRoomUpdate;
            JackboxClient.Connect();
        }

        private void OnSelfUpdate(object sender, Revision<Quiplash2Player> revision)
        {
            if (revision.New.State != RoomState.Gameplay_AnswerQuestion || revision.New.Question == null)
            {
                if (revision.New.State == RoomState.Gameplay_R3Vote)
                    VoteR3Quip(revision.New);
                return;
            }

            if (revision.New.ShowError)
            {
                LogInfo("The submitted answer was already sent by another player. Generating a new lie...");
            }

            if (revision.New.Question?.Type == R3Type.None)
                SubmitQuip(revision.New);
            else
                SubmitR3Quip(revision.New);
        }

        private void OnRoomUpdate(object sender, Revision<Quiplash2Room> revision)
        {
            if (revision.Old.State != revision.New.State)
                LogDebug($"New room state: {revision.New.State}", true);

            if (revision.New.State == RoomState.Gameplay_Vote)
                VoteQuip(revision.New);
        }

        private async void VoteQuip(Quiplash2Room room)
        {
            var choices = room.ResponseChoices;
            if (choices.Count == 0) return;

            var quips = choices.Select(choice => choice.Answer).ToList();
            var favoriteIdx = await FormVote(room.Question.Prompt, quips);
            JackboxClient.ChooseFavorite(choices[favoriteIdx].Key);
        }

        private async void VoteR3Quip(Quiplash2Player self)
        {
            var question = JackboxClient.GameState.Room.Question.Prompt;
            var choices = self.ResponseChoices;
            if (choices.Count == 0) return;

            var quips = choices.Select(choice => choice.Answer).ToList();
            var favoriteIdx = await FormVote(question, quips);
            JackboxClient.ChooseFavorite(choices[favoriteIdx].PlayerIndex);
        }

        private async void SubmitQuip(Quiplash2Player self)
        {
            if (self.Question is not { } question) return;

            var quip = await FormQuip(question.Prompt);
            if (quip == "")
            {
                LogInfo("GPT failed to come up with a response, so a Safety Quip will be used");
                JackboxClient.RequestSafetyQuip(question.ID);
            }
            else
            {
                JackboxClient.SubmitQuip(question.ID, quip);
            }
        }

        private async void SubmitR3Quip(Quiplash2Player self)
        {
            if (self.Question is not { } question) return;

            var quip = "";
            switch (question.Type)
            {
                case R3Type.WordLash: // Response to QUIP that includes PROMPT word
                    var prompt = CleanPromptForEntry(question.Quip);
                    quip = await ProvideWordLashQuip(prompt, question.Prompt, 45);
                    if (quip != "")
                        LogInfo($"GPT responded to \"{prompt} '{question.Prompt}'\" with \"{quip}\"");
                    break;
                case R3Type.AcroLash: // Response based on PROMPT acroynm
                    quip = await ProvideAcroLashQuip(question.Prompt, 45);
                    if (quip != "")
                        LogInfo($"GPT provided \"{quip}\" for the acronym \"{question.Prompt}\"");
                    break;
                case R3Type.ComicLash: // Finish comic strip
                    LogWarning($"This is comic ID {question.ID}");
                    prompt = _descriptionProvider.ProvideDescriptionForImageId(question.ID.ToString());
                    quip = await ProvideComicLashQuip(prompt, 45);
                    if (quip != "")
                        LogInfo($"GPT responded to the comic \"{prompt}\" with \"{quip}\"");
                    break;
            }

            if (quip == "")
            {
                LogInfo("GPT failed to come up with a response, using default response");
                quip = $"AI {Instance} failed to formulate an answer"; // Needs to be unique
            }
            JackboxClient.SubmitQuip(question.ID, quip);
        }

        private async Task<string> ProvideWordLashQuip(string qlPrompt, string word, int maxLength)
        {
            var prompt = $@"Below are some prompts and outlandish, funny, ridiculous answers to them.

Q: Come up with a new cartoon character with this word in his name: SLIME
Funny Answer: Slime E. Mann

Q: Come up with a fast food restaurant with this word in its name: TOOTH
Funny Answer: The Tooth Fairy's Pillowy Doughnut House

Q: Come up with a new hilarious sitcom with this word in the title: CORN
Funny Answer: My Life as a Teenage Corn Stalker

Q: Come up with a shocking newspaper headline with this word in its title: PANTS
Funny Answer: Cat wearing no pants arrested for indecent exposure!

Q: {qlPrompt} {word}
Funny Answer:";

            var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
                {
                    Temperature = 0.6,
                    MaxTokens = 16,
                    TopP = 1,
                    FrequencyPenalty = 0.2,
                    PresencePenalty = 0.1,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim());
                    if (cleanText.Length > 0 && cleanText.Length <= maxLength && cleanText.Contains(word.ToUpper()))
                        return true;

                    LogDebug($"Received unusable ProvideWordLashQuip response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                defaultResponse: "");

            return CleanResult(result.Text.Trim(), true);
        }

        private async Task<string> ProvideAcroLashQuip(string qlPrompt, int maxLength)
        {
            var prompt = $@"Below are some acroynms and outlandish, funny, ridiculous interpretations of them.

Q: Come up with a funny interpretation of this acronym: F.F.E.
Funny Answer: Frog Farmer Energy

Q: Come up with a funny interpretation of this acronym: B.Y.D.
Funny Answer: Boldly You Dance

Q: Come up with a funny interpretation of this acronym: J.O.E.
Funny Answer: Jazzed Out Ears

Q: Come up with a funny interpretation of this acronym: F.T.T.
Funny Answer: Forget The Tea

Q: {qlPrompt}
Funny Answer:";

            bool ValidAcronymExpansion(string acronym, string expansion)
            {
                var letters = acronym[..^1].Split('.');
                var words = expansion.ToUpper().Split(' ');

                if (letters.Length != words.Length) return false;
                return !letters.Where((t, i) => !words[i].StartsWith(t)).Any();
            }

            var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
                {
                    Temperature = 0.6,
                    MaxTokens = 16,
                    TopP = 1,
                    FrequencyPenalty = 0.2,
                    PresencePenalty = 0.1,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim());
                    if (cleanText.Length > 0 && cleanText.Length <= maxLength && ValidAcronymExpansion(qlPrompt, cleanText))
                        return true;

                    LogDebug($"Received unusable ProvideAcroLashQuip response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                defaultResponse: "");

            return CleanResult(result.Text.Trim(), true);
        }

        private async Task<string> ProvideComicLashQuip(string qlPrompt, int maxLength)
        {
            var prompt = $@"Below are some prompts and outlandish, funny, ridiculous responses to them.

Prompt: A customer is in a coffin shop looking questioningly at a specific coffin and asks ""Wait, why is it discounted?"" to which the sales person responds:
Response: I just had it dug up

Prompt: An astronaut is floating in space above the earth looking at a display and asks ""Any message from my family on Earth?"" to which the person on the display responds:
Response: Close Kerbal Space Program and come for dinner

Prompt: Someone sitting on an examination table in a doctor's office asks ""Isn't there a quicker way to lose weight?"" to which the doctor responds: 
Response: Have you considered amputation

Prompt: {qlPrompt}
Response:";

            var result = await CompletionService.CompletePrompt(prompt, new ICompletionService.CompletionParameters
                {
                    Temperature = 0.6,
                    MaxTokens = 16,
                    TopP = 1,
                    FrequencyPenalty = 0.2,
                    PresencePenalty = 0.1,
                    StopSequences = new[] { "\n" }
                },
                completion =>
                {
                    var cleanText = CleanResult(completion.Text.Trim());
                    if (cleanText.Length > 0 && cleanText.Length <= maxLength)
                        return true;

                    LogDebug($"Received unusable ProvideComicLashQuip response: \"{completion.Text.Trim()}\"");
                    return false;
                },
                defaultResponse: "");

            return CleanResult(result.Text.Trim().Trim('"'), true);
        }

    }
}
