using System.Net.Http.Headers;
using System.Text;
using GoodVibes.Traffic.Application;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GoodVibes.Traffic.Infrastructure;

public class OpenAiApiClient(IHttpClientFactory httpClientFactory, ILogger<OpenAiApiClient> logger) : IOpenAiApiClient
{
    public async Task<string> GetResponse<T>(string prompt)
    {
        var httpClient = httpClientFactory.CreateClient(OpenAiApiConfig.HTTP_CLIENT_NAME);
        string? responseContent = null;

        var payload = new
        {
            model = "gpt-5-nano",
            messages = new[]
            {
                new { role = "user", content = prompt },
            }
        };
        
        var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        
        var response = await httpClient.PostAsync("v1/chat/completions", content);
        response.EnsureSuccessStatusCode();

        responseContent = await response.Content.ReadAsStringAsync();

        var deserializationData = JsonConvert.DeserializeObject<ChatCompletionResponse>(responseContent);

        logger.LogDebug("ChatGPT request: {ChatGPTPayload}, response: {ChatGPTResponse}", content, deserializationData);

        return deserializationData.Choices[0].Message.Content;
    }

    public async Task<string> UploadFile(string fileString)
    {
        var httpClient = httpClientFactory.CreateClient(OpenAiApiConfig.HTTP_CLIENT_NAME);
        using var form = new MultipartFormDataContent();
        
        var bytes = Encoding.UTF8.GetBytes(fileString);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
        form.Add(fileContent, "file","dane-analityczne");
        form.Add(new StringContent("assistants"), "purpose");

        var resp = await httpClient.PostAsync("/v1/files", form);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
       
        var respText = await resp.Content.ReadAsStringAsync();

        resp.EnsureSuccessStatusCode();

        var j = JObject.Parse(respText);
        return j["id"]?.ToString() ?? throw new Exception("No file id in response");
    }
}


public class ChatCompletionResponse
{
    public string Id { get; set; }
    public string Object { get; set; }
    public long Created { get; set; }
    public string Model { get; set; }
    public List<Choice> Choices { get; set; }
    public Usage Usage { get; set; }
    public string ServiceTier { get; set; }
    public string SystemFingerprint { get; set; }
}

public class Choice
{
    public int Index { get; set; }
    public Message Message { get; set; }
    public object Logprobs { get; set; }
    public string FinishReason { get; set; }
}

public class Message
{
    public string Role { get; set; }
    public string Content { get; set; }
    public object Refusal { get; set; }
    public List<object> Annotations { get; set; }
}

public class Usage
{
    [JsonProperty("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonProperty("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonProperty("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonProperty("prompt_tokens_details")]
    public PromptTokensDetails PromptTokensDetails { get; set; }

    [JsonProperty("completion_tokens_details")]
    public CompletionTokensDetails CompletionTokensDetails { get; set; }
}

public class PromptTokensDetails
{
    [JsonProperty("cached_tokens")]
    public int CachedTokens { get; set; }

    [JsonProperty("audio_tokens")]
    public int AudioTokens { get; set; }
}

public class CompletionTokensDetails
{
    [JsonProperty("reasoning_tokens")]
    public int ReasoningTokens { get; set; }

    [JsonProperty("audio_tokens")]
    public int AudioTokens { get; set; }

    [JsonProperty("accepted_prediction_tokens")]
    public int AcceptedPredictionTokens { get; set; }

    [JsonProperty("rejected_prediction_tokens")]
    public int RejectedPredictionTokens { get; set; }
}