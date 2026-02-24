namespace NoNPL.Entities;

public struct PairData
{
    public HashSet<PreToken> PreTokens;
    public int Count;

    public PairData(PreToken firstPreToken)
    {
        PreTokens = new HashSet<PreToken> { firstPreToken };
        Count = 1;
    }
}
