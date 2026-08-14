namespace MinecartNetwork.Models;

public sealed class MinecartSaveData
{
    public int DataVersion { get; set; } = 1;
    public List<MinecartStation> Stations { get; set; } = new();
}
