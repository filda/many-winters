using System.Text.Json.Serialization;

namespace ManyWinters.Core.World;

[JsonConverter(typeof(JsonStringEnumConverter<Climate>))]
public enum Climate
{
    Cold,
    Mild,
    Hot,
}
