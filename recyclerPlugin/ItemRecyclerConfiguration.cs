using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;

public class ItemRecyclerConfiguration : IRocketPluginConfiguration
{
    public RecyclerConfig Recyclers;
    [XmlArrayItem("item")]
    public List<RecyclableItem> Items;

    public void LoadDefaults()
    {
        Recyclers = new RecyclerConfig { RecyclerStorage = 123, RecycledStorage = 456 };
        Items = new List<RecyclableItem>();
    }
}

public class RecyclerConfig
{
    public ushort RecyclerStorage;
    public ushort RecycledStorage;
}

public class RecyclableItem
{
    public ushort Id;
    public uint RecycleTime;
    [XmlArray("recycledIds"), XmlArrayItem("unSignedByte")]
    public List<ushort> RecycledIds;
}
