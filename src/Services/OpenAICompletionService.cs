using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using OpenAI_API.Models;
using static JackboxGPT.Services.ICompletionService;

namespace JackboxGPT.Services
{
    // ReSharper disable once InconsistentNaming
    public class OpenAICompletionService : ICompletionService
    {
        private readonly OpenAIAPI _api;
        private readonly Model _chatModel;
        private readonly Model _completionModel;

        private const string ChatBaseContext = "You are an AI player in a game that may have other AI players in it. You should add variety to your responses to avoid overlapping with other AI players. Please do not include emoji or unicode in your responses as this game does not allow them.";
        private string _chatPersonalityContext = "";
        private readonly Dictionary<string, Conversation> _convoLookup = new(); 

        /// <summary>
        /// Instantiate an <see cref="OpenAICompletionService"/> from the environment.
        /// </summary>
        public OpenAICompletionService(IConfigurationProvider configuration)
            : this(Environment.GetEnvironmentVariable("OPENAI_API_KEY"), configuration)
        {
        }

        private OpenAICompletionService(string apiKey, IConfigurationProvider configuration)
        {
            _api = new OpenAIAPI(apiKey);
            _chatModel = new Model(configuration.OpenAIChatEngine);
            _completionModel = new Model(configuration.OpenAICompletionEngine);
        }

        public void ApplyPersonalityType(string personalityInfo)
        {
            _chatPersonalityContext = $"To make things more varied, please incorporate being {personalityInfo.Replace("`", null)} in your responses.";
        }

        private async Task<CompletionResponse?> GetCompletion(string prompt, CompletionParameters completionParameters)
        {
            CompletionResult apiResult;
            try
            {
                apiResult = await _api.Completions.CreateCompletionAsync(
                    prompt,
                    _completionModel,
                    completionParameters.MaxTokens,
                    completionParameters.Temperature,
                    null,
                    1,
                    logProbs: completionParameters.LogProbs,
                    echo: completionParameters.Echo,
                    presencePenalty: completionParameters.PresencePenalty,
                    frequencyPenalty: completionParameters.FrequencyPenalty,
                    stopSequences: completionParameters.StopSequences
                );
            }
            catch (HttpRequestException)
            {
                await Task.Delay(100);
                return null;
            }

            return new CompletionResponse
            {
                Text = apiResult.Completions[0].Text,
                FinishReason = apiResult.Completions[0].FinishReason
            };
        }

        private static async Task<CompletionResponse?> GetChatCompletion(string prompt, Conversation ai)
        {
            ai.AppendUserInput(prompt);
            try
            {
                await ai.GetResponseFromChatbotAsync();
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.Message);
                await Task.Delay(100);
                return null;
            }

            var response = new CompletionResponse
            {
                Text = ai.MostRecentApiResult.Choices[0].Message.TextContent,
                FinishReason = ai.MostRecentApiResult.Choices[0].FinishReason
            };
            return response;
        }

        private Conversation GetConversation(string systemMsg, CompletionParameters completionParameters)
        {
            if (systemMsg.Length == 0)
                return null;

            if (_convoLookup.ContainsKey(systemMsg))
                return _convoLookup[systemMsg];

            var convo = _api.Chat.CreateConversation();
            convo.Model = _chatModel;
            convo.RequestParameters.NumChoicesPerMessage = 1;
            if (!_chatModel.ModelID.StartsWith("gpt-5"))
            {
                convo.RequestParameters.MaxTokens = completionParameters.MaxTokens;
                convo.RequestParameters.Temperature = completionParameters.Temperature;
                convo.RequestParameters.PresencePenalty = completionParameters.PresencePenalty;
                convo.RequestParameters.FrequencyPenalty = completionParameters.FrequencyPenalty;
            }

            convo.AppendSystemMessage(ChatBaseContext);
            if (_chatPersonalityContext.Length != 0)
                convo.AppendSystemMessage(_chatPersonalityContext);
            convo.AppendSystemMessage(systemMsg);

            _convoLookup[systemMsg] = convo;
            return convo;
        }

        public void ResetOne(string key)
        {
            _convoLookup.Remove(key);
        }

        public void ResetAll()
        {
            _convoLookup.Clear();
        }

        public async Task<CompletionResponse> CompletePrompt(TextInput prompt, bool chatCompletion,
            CompletionParameters completionParameters, Func<CompletionResponse, bool> conditions = null,
            int maxTries = 5, string defaultResponse = "")
        {
            var result = new CompletionResponse();
            var convo = GetConversation(prompt.ChatSystemMessage, completionParameters);
            var validResponse = false;
            var tries = 0;

            while (!validResponse && tries < maxTries)
            {
                tries++;

                CompletionResponse? output;
                if (chatCompletion)
                {
                    if (tries == 1)
                        output = await GetChatCompletion(prompt.ChatStylePrompt, convo);
                    else
                        output = await GetChatCompletion("Try again", convo);
                }
                else
                {
                    output = await GetCompletion(prompt.CompletionStylePrompt, completionParameters);
                }
                if (!output.HasValue) continue;

                result = output.Value;
                if (conditions == null) break;
                validResponse = conditions(result);
            }

            if (validResponse) return result;

            return new CompletionResponse
            {
                FinishReason = "no_valid_responses",
                Text = defaultResponse
            };
        }

        public async Task<T> CompletePrompt<T>(TextInput prompt, bool chatCompletion,
            CompletionParameters completionParameters, Func<CompletionResponse, T> process, T defaultResponse,
            Func<T, bool> conditions = null, int maxTries = 5)
        {
            var processedResult = defaultResponse;
            var convo = GetConversation(prompt.ChatSystemMessage, completionParameters);
            var validResponse = false;
            var tries = 0;

            while (!validResponse && tries < maxTries)
            {
                tries++;

                CompletionResponse? output;
                if (chatCompletion)
                {
                    if (tries == 1)
                        output = await GetChatCompletion(prompt.ChatStylePrompt, convo);
                    else
                        output = await GetChatCompletion("Try again", convo);
                }
                else
                {
                    output = await GetCompletion(prompt.CompletionStylePrompt, completionParameters);
                }
                if (!output.HasValue) continue;

                processedResult = process(output.Value);

                if (conditions == null) break;
                validResponse = conditions(processedResult);
            }

            return processedResult;
        }

        private static double CosineSimilarity(IList<float> vector1, IList<float> vector2)
        {
            var dotProduct = 0.0;
            var norm1 = 0.0;
            var norm2 = 0.0;
            for (var i = 0; i < vector1.Count; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                norm1 += Math.Pow(vector1[i], 2);
                norm2 += Math.Pow(vector2[i], 2);
            }
            return dotProduct / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }

        public async Task<List<SearchResponse>> SemanticSearch(string query, IList<string> documents, int maxTries = 3)
        {
            var queryEmbedding = Array.Empty<float>();
            var documentEmbeddings = Array.Empty<float[]>();
            var tries = 0;

            while (tries < maxTries)
            {
                tries++;
                try
                {
                    queryEmbedding = await _api.Embeddings.GetEmbeddingsAsync(query);
                    documentEmbeddings = await Task.WhenAll(documents.Select(doc => _api.Embeddings.GetEmbeddingsAsync(doc)));
                    break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100);
                }
            }

            var similarities = documentEmbeddings.Select((embedding, i) =>
                (index: i, similarity: CosineSimilarity(queryEmbedding, embedding))).ToList();

            var searchResults = similarities.OrderByDescending(similarity => similarity.similarity)
                .Select(similarity => new SearchResponse
                {
                    Index = similarity.index,
                    Score = similarity.similarity * 300 // TODO: Change; Doing this currently to deal with the fact that original values were 0-300 and now 0-1
                })
                .ToList();

            return searchResults;
        }
    }
}
