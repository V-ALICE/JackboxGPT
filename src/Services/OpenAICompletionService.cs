using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Models;
using static JackboxGPT3.Services.ICompletionService;

namespace JackboxGPT3.Services
{
    // ReSharper disable once InconsistentNaming
    public class OpenAICompletionService : ICompletionService
    {
        private readonly OpenAIAPI _api;
        private readonly Model _model;

        /// <summary>
        /// Instantiate an <see cref="OpenAICompletionService"/> from the environment.
        /// </summary>
        public OpenAICompletionService(IConfigurationProvider configuration) : this(Environment.GetEnvironmentVariable("OPENAI_KEY"), configuration) { }

        private OpenAICompletionService(string apiKey, IConfigurationProvider configuration)
        {
            _api = new OpenAIAPI(apiKey);
            _model = new Model(configuration.OpenAIEngine);
        }

        public async Task<CompletionResponse> CompletePrompt(
            string prompt,
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
                var apiResult = await _api.Completions.CreateCompletionAsync(
                    prompt,
                    _model,
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

                result = ChoiceToCompletionResponse(apiResult.Completions[0]);

                if (conditions == null) break;
                validResponse = conditions(result);
            }

            if (!validResponse)
                result = new CompletionResponse
                {
                    FinishReason = "no_valid_responses",
                    Text = defaultResponse
                };

            return result;
        }
        
        public async Task<T> CompletePrompt<T>(
            string prompt,
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
                var apiResult = await _api.Completions.CreateCompletionAsync(
                    prompt,
                    _model,
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

                var result = ChoiceToCompletionResponse(apiResult.Completions[0]);
                processedResult = process(result);

                if (conditions == null) break;
                validResponse = conditions(processedResult);
            }

            return processedResult;
        }

        private static CompletionResponse ChoiceToCompletionResponse(Choice choice)
        {
            return new()
            {
                Text = choice.Text,
                FinishReason = choice.FinishReason
            };
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

        public async Task<List<SearchResponse>> SemanticSearch(string query, IList<string> documents)
        {

            var queryEmbedding = Array.Empty<float>();
            var documentEmbeddings = Array.Empty<float[]>();
            try
            {
                queryEmbedding = await _api.Embeddings.GetEmbeddingsAsync(query);
                documentEmbeddings = await Task.WhenAll(documents.Select(doc => _api.Embeddings.GetEmbeddingsAsync(doc)));
            }
            catch (HttpRequestException)
            {
                // Sometimes this will throw a 502 error, not sure why it happens randomly
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
