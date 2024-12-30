using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Models;
using static JackboxGPT.Services.ICompletionService;

namespace JackboxGPT.Services
{
    // ReSharper disable once InconsistentNaming
    public class OpenAICompletionService : ICompletionService
    {
        private readonly OpenAIAPI _api;
        private readonly Model _chat_model;
        private readonly Model _completion_model;

        /// <summary>
        /// Instantiate an <see cref="OpenAICompletionService"/> from the environment.
        /// </summary>
        public OpenAICompletionService(IConfigurationProvider configuration) : this(Environment.GetEnvironmentVariable("OPENAI_API_KEY"), configuration) { }

        private OpenAICompletionService(string apiKey, IConfigurationProvider configuration)
        {
            _api = new OpenAIAPI(apiKey);
            _chat_model = new Model(configuration.OpenAIChatEngine);
            _completion_model = new Model(configuration.OpenAICompletionEngine);
        }

        private async Task<CompletionResponse?> GetCompletion(TextInput prompt, CompletionParameters completionParameters)
        {
            CompletionResult apiResult;
            try
            {
                apiResult = await _api.Completions.CreateCompletionAsync(
                    prompt.CompletionStylePrompt,
                    _completion_model,
                    completionParameters.MaxTokens,
                    completionParameters.Temperature,
                    completionParameters.TopP,
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

        private async Task<CompletionResponse?> GetChatCompletion(TextInput prompt, CompletionParameters completionParameters)
        {
            // Probably better to just create an entirely new conversation every attempt since the previous attempts are just wasted tokens
            var ai = _api.Chat.CreateConversation();
            ai.Model = _chat_model;
            ai.RequestParameters.MaxTokens = completionParameters.MaxTokens;
            ai.RequestParameters.Temperature = completionParameters.Temperature;
            ai.RequestParameters.TopP = completionParameters.TopP;
            ai.RequestParameters.PresencePenalty = completionParameters.PresencePenalty;
            ai.RequestParameters.FrequencyPenalty = completionParameters.FrequencyPenalty;
            //ai.RequestParameters.MultipleStopSequences = completionParameters.StopSequences;
            ai.RequestParameters.NumChoicesPerMessage = 1;
            ai.AppendSystemMessage(prompt.ChatSystemMessage);

            ai.AppendUserInput(prompt.ChatStylePrompt);
            try
            {
                await ai.GetResponseFromChatbotAsync();
            }
            catch (HttpRequestException)
            {
                await Task.Delay(100);
                return null;
            }

            return new CompletionResponse
            {
                Text = ai.MostRecentApiResult.Choices[0].Message.TextContent,
                FinishReason = ai.MostRecentApiResult.Choices[0].FinishReason
            };
        }

        public async Task<CompletionResponse> CompletePrompt(
            TextInput prompt,
            bool chatCompletion,
            CompletionParameters completionParameters,
            Func<CompletionResponse, bool> conditions = null,
            int maxTries = 5,
            string defaultResponse = ""
        ) {
            var result = new CompletionResponse();
            var validResponse = false;
            var tries = 0;

            while(!validResponse && tries < maxTries)
            {
                tries++;

                CompletionResponse? output;
                if (chatCompletion)
                    output = await GetChatCompletion(prompt, completionParameters);
                else
                    output = await GetCompletion(prompt, completionParameters);
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
        
        public async Task<T> CompletePrompt<T>(
            TextInput prompt,
            bool chatCompletion,
            CompletionParameters completionParameters,
            Func<CompletionResponse, T> process,
            T defaultResponse,
            Func<T, bool> conditions = null,
            int maxTries = 5
        ) {
            var processedResult = defaultResponse;
            var validResponse = false;
            var tries = 0;

            while(!validResponse && tries < maxTries)
            {
                tries++;

                CompletionResponse? output;
                if (chatCompletion)
                    output = await GetChatCompletion(prompt, completionParameters);
                else
                    output = await GetCompletion(prompt, completionParameters);
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
