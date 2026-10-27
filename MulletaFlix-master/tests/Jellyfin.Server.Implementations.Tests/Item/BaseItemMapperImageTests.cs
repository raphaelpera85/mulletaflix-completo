using System;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Item;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Item;

public class BaseItemMapperImageTests
{
    [Fact]
    public void ImageProvenanceIsPreservedInBothMappingDirections()
    {
        var domainImage = new ItemImageInfo
        {
            Path = "poster.jpg",
            Type = ImageType.Primary,
            ProviderName = "TheMovieDb",
            Language = "pt"
        };

        var entity = BaseItemMapper.MapImageToEntity(Guid.NewGuid(), domainImage);
        var mappedBack = BaseItemMapper.MapImageFromEntity(entity, null);

        Assert.Equal("TheMovieDb", entity.ProviderName);
        Assert.Equal("pt", entity.Language);
        Assert.Equal(domainImage.ProviderName, mappedBack.ProviderName);
        Assert.Equal(domainImage.Language, mappedBack.Language);
    }
}
