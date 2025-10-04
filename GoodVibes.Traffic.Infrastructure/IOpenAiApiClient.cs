namespace GoodVibes.Traffic.Application;

public interface IOpenAiApiClient
{
    Task<string> GetResponse<T>(string prompt);
    Task<string> UploadFile(string fileString);
}