namespace Wopi;

public class WopiFile
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Version { get; set; } = default!;
    public byte[] Contents { get; set; } = default!;
}
