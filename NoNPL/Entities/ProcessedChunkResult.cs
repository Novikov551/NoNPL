using System.Diagnostics.CodeAnalysis;

namespace NoNPL.Entities
{
    public record ProcessedChunkResult
    {
        [SetsRequiredMembers]
        public ProcessedChunkResult(Dictionary<PreToken, int> preTokens, Dictionary<TokenPair, PairData> tokenPairData)
        {
            PreTokens = preTokens;
            TokenPairsData = tokenPairData;
        }

        public required Dictionary<PreToken, int> PreTokens { get; init; }
        public required Dictionary<TokenPair, PairData> TokenPairsData { get; init; }
    }
}
