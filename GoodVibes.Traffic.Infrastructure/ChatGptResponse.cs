namespace GoodVibes.Traffic.Infrastructure;

public class ChatGptResponse
{
    public string Model { get; set; }

    public UsageObj Usage { get; set; }

    public List<ChoiceObj> Choices { get; set; }

    public class ChoiceObj
    {
        public int Index { get; set; }

        public MessageObj Message { get; set; }

        public string FinishReason { get; set; }

        public class MessageObj
        {
            public string Role { get; set; }

            public string Content { get; set; }

            public FunctionCallObj FunctionCall { get; set; }

            public class FunctionCallObj
            {
                public string Name { get; set; }

                public string Arguments { get; set; }
            }
        }
    }

    public class UsageObj
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }
}