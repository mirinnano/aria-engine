using System.Collections.Generic;
using AriaEngine.Core;
using FluentAssertions;
using Xunit;

namespace AriaEngine.Tests;

public class FastSpriteDictionaryTests
{
    [Fact]
    public void CopyTo_CopiesFastAndDictionaryBackedSpritesInEnumerationOrder()
    {
        var sprites = new FastSpriteDictionary
        {
            [2] = new Sprite { Id = 2, Type = SpriteType.Rect },
            [120] = new Sprite { Id = 120, Type = SpriteType.Text }
        };
        var target = new KeyValuePair<int, Sprite>[3];

        sprites.CopyTo(target, 1);

        target[1].Key.Should().Be(2);
        target[1].Value.Id.Should().Be(2);
        target[2].Key.Should().Be(120);
        target[2].Value.Id.Should().Be(120);
    }
}
