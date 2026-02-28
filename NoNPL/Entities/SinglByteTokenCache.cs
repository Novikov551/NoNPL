namespace NoNPL.Entities
{
    public static class SinglByteTokenCache
    {
        static SinglByteTokenCache()
        {
            Tokens = new(256);
            for (int i = 0; i < 256; i++)
            {
                Tokens[(byte)i] = new Token([(byte)i]);
            }
        }

        public static Dictionary<byte, Token> Tokens { get; private set; }
    }
}
