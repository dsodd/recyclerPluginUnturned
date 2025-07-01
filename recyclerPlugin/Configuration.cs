using Rocket.API;
using System.Collections.Generic;
using System.Xml.Serialization;

public class Configuration : IRocketPluginConfiguration
{
    public RecyclerConfig Recyclers;
    [XmlArrayItem("Item")]
    public List<RecyclableItem> Items;

    public void LoadDefaults()
    {
        Recyclers = new RecyclerConfig { RecyclerInput = 36628, RecyclerOutput = 36628 };
        Items = new List<RecyclableItem>
        {
            new RecyclableItem
            {
                Id = 121,
                RecycleTime = 2000,
                OutputItemIDs = new List<OutputItem>
                {
                    new OutputItem { Id = 67, Amount = 5 }
                }
            }
        };
    }
}

public class RecyclerConfig
{
    public ushort RecyclerInput;
    public ushort RecyclerOutput;
}

public class RecyclableItem
{
    public ushort Id;
    public uint RecycleTime;

    [XmlArray("OutputItems")]
    [XmlArrayItem("Item")]
    public List<OutputItem> OutputItemIDs;
}

public class OutputItem
{
    [XmlText]
    public ushort Id;

    [XmlAttribute("amount")]
    public byte Amount;
}