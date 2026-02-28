namespace NoNPL.Services.Serializers
{
    public static class SerializerFactory
    {
        public static ISerializer Create(VocabFileFormat format)
        {
            return format switch
            {
                VocabFileFormat.Json => new JsonSerializer(),
                VocabFileFormat.MessagePack => new MessagePackSerializer(),
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };
        }
    }
}
