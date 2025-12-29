namespace AIPlatform.Core.Interfaces;

public interface ITextChunker
{
    List<string> SplitText(string fullText);
}