namespace NoNPL.Services.Serializers
{
    public static class SerializerFactory
    {
        public static ISerializer Create(SerializerFormat format)
        {
            return format switch
            {
                SerializerFormat.Json => new JsonSerializer(),
                SerializerFormat.MessagePack => new MessagePackSerializer(),
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };
        }
    }
}
