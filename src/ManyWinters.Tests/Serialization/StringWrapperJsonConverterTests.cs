using System.Text.Json;
using ManyWinters.Core.Knowledge;
using ManyWinters.Core.World;

namespace ManyWinters.Tests.Serialization;

public class StringWrapperJsonConverterTests
{
    [Fact]
    public void ResourceKindIdSerializesAsAPlainString()
    {
        var json = JsonSerializer.Serialize(new ResourceKindId("apple"));

        Assert.Equal("\"apple\"", json);
    }

    [Fact]
    public void ResourceKindIdRoundTripsThroughJson()
    {
        var json = JsonSerializer.Serialize(new ResourceKindId("apple"));

        var restored = JsonSerializer.Deserialize<ResourceKindId>(json);

        Assert.Equal(new ResourceKindId("apple"), restored);
    }

    [Fact]
    public void SkillTypeIdRoundTripsThroughJson()
    {
        var json = JsonSerializer.Serialize(new SkillTypeId("foraging"));

        var restored = JsonSerializer.Deserialize<SkillTypeId>(json);

        Assert.Equal(new SkillTypeId("foraging"), restored);
    }

    [Fact]
    public void TechniqueIdRoundTripsThroughJson()
    {
        var json = JsonSerializer.Serialize(new TechniqueId("efficient_foraging"));

        var restored = JsonSerializer.Deserialize<TechniqueId>(json);

        Assert.Equal(new TechniqueId("efficient_foraging"), restored);
    }

    [Fact]
    public void ReadingANonStringTokenThrows()
    {
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ResourceKindId>("123"));

        // "Expected" is unique to our message; System.Text.Json substitutes its own generic
        // message (which also names the type) when a converter throws with an empty one.
        Assert.Contains("Expected", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadingANullTokenThrowsRatherThanProducingANullValue()
    {
        var ex = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ResourceKindId>("null"));

        Assert.Contains("Expected", ex.Message, StringComparison.Ordinal);
    }
}
