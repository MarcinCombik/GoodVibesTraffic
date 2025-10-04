namespace GoodVibes.Traffic.Domain;

public record OpenAiRequest(string Prompt);

public record OpenAiResponse(string Result);