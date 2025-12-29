using AIPlatform.Core.Interfaces;
using System.Text.RegularExpressions;

namespace AIPlatform.Infrastructure.Services;

public class SmartTextChunker : ITextChunker
{
    private readonly int _maxChunkSize;
    private readonly int _overlap;
    private readonly char[] _separators = new[] { '\n', '.', '?', '!' };

    // Default: ~1000 tokens (approx 4000 chars) with 800 chars overlap
    public SmartTextChunker(int maxChunkSize = 4000, int overlap = 800)
    {
        _maxChunkSize = maxChunkSize;
        _overlap = overlap;
    }

    public List<string> SplitText(string text)
    {
        // Normalize line endings
        text = Regex.Replace(text, @"\r\n", "\n");

        var chunks = new List<string>();
        int position = 0;

        while (position < text.Length)
        {
            int targetEnd = position + _maxChunkSize;

            if (targetEnd >= text.Length)
            {
                chunks.Add(text.Substring(position).Trim());
                break;
            }

            // Find best split point (look backwards for punctuation)
            int bestSplit = -1;
            int lookBackLimit = Math.Max(position, targetEnd - (_maxChunkSize / 2));

            for (int i = targetEnd; i > lookBackLimit; i--)
            {
                if (_separators.Contains(text[i]))
                {
                    bestSplit = i + 1; // Include the punctuation
                    break;
                }
            }

            if (bestSplit == -1) // Fallback to space
                bestSplit = text.LastIndexOf(' ', targetEnd, targetEnd - lookBackLimit);

            if (bestSplit == -1) // Hard cut
                bestSplit = targetEnd;

            string chunk = text.Substring(position, bestSplit - position).Trim();
            if (!string.IsNullOrWhiteSpace(chunk)) chunks.Add(chunk);

            // Calculate next start with overlap
            int nextStart = bestSplit - _overlap;
            if (nextStart <= position) nextStart = position + 1;

            // Align to word boundary for cleaner start
            if (nextStart < text.Length)
            {
                int safeStart = text.IndexOf(' ', nextStart);
                if (safeStart != -1 && safeStart < bestSplit) nextStart = safeStart + 1;
            }

            position = nextStart;
        }

        return chunks;
    }
}