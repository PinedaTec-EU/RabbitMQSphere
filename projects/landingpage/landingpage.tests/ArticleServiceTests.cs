using Xunit;
using Moq;
using landingpage.application.Services;
using landingpage.domain.Entities;
using landingpage.domain.Interfaces;
using landingpage.domain.ValueObjects;

namespace landingpage.tests;

public class ArticleServiceTests
{
    [Fact]
    public async Task GetArticlesAsync_ReturnsPagedArticles()
    {
        // Arrange
        var mockRepository = new Mock<IArticleRepository>();
        var expectedArticles = new PagedResult<Article>
        {
            Items = new List<Article>
            {
                new Article { Id = "1", Title = "Test Article 1" },
                new Article { Id = "2", Title = "Test Article 2" }
            },
            CurrentPage = 1,
            PageSize = 10,
            TotalItems = 2
        };

        mockRepository
            .Setup(repo => repo.GetArticlesAsync(1, 10, default))
            .ReturnsAsync(expectedArticles);

        var service = new ArticleService(mockRepository.Object);

        // Act
        var result = await service.GetArticlesAsync(1, 10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.CurrentPage);
        mockRepository.Verify(repo => repo.GetArticlesAsync(1, 10, default), Times.Once);
    }

    [Fact]
    public async Task GetArticlesAsync_WithInvalidPageNumber_DefaultsToOne()
    {
        // Arrange
        var mockRepository = new Mock<IArticleRepository>();
        var expectedArticles = new PagedResult<Article>
        {
            Items = new List<Article>(),
            CurrentPage = 1,
            PageSize = 10,
            TotalItems = 0
        };

        mockRepository
            .Setup(repo => repo.GetArticlesAsync(1, 10, default))
            .ReturnsAsync(expectedArticles);

        var service = new ArticleService(mockRepository.Object);

        // Act
        var result = await service.GetArticlesAsync(-1, 10);

        // Assert
        Assert.NotNull(result);
        mockRepository.Verify(repo => repo.GetArticlesAsync(1, 10, default), Times.Once);
    }

    [Fact]
    public async Task GetArticlesAsync_WithInvalidPageSize_DefaultsToTen()
    {
        // Arrange
        var mockRepository = new Mock<IArticleRepository>();
        var expectedArticles = new PagedResult<Article>
        {
            Items = new List<Article>(),
            CurrentPage = 1,
            PageSize = 10,
            TotalItems = 0
        };

        mockRepository
            .Setup(repo => repo.GetArticlesAsync(1, 10, default))
            .ReturnsAsync(expectedArticles);

        var service = new ArticleService(mockRepository.Object);

        // Act
        var result = await service.GetArticlesAsync(1, 0);

        // Assert
        Assert.NotNull(result);
        mockRepository.Verify(repo => repo.GetArticlesAsync(1, 10, default), Times.Once);
    }

    [Fact]
    public async Task GetArticleByIdAsync_ReturnsArticle()
    {
        // Arrange
        var mockRepository = new Mock<IArticleRepository>();
        var expectedArticle = new Article 
        { 
            Id = "1", 
            Title = "Test Article",
            Content = "Test Content"
        };

        mockRepository
            .Setup(repo => repo.GetArticleByIdAsync("1", default))
            .ReturnsAsync(expectedArticle);

        var service = new ArticleService(mockRepository.Object);

        // Act
        var result = await service.GetArticleByIdAsync("1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("1", result.Id);
        Assert.Equal("Test Article", result.Title);
        mockRepository.Verify(repo => repo.GetArticleByIdAsync("1", default), Times.Once);
    }

    [Fact]
    public async Task GetArticleByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<IArticleRepository>();

        mockRepository
            .Setup(repo => repo.GetArticleByIdAsync("999", default))
            .ReturnsAsync((Article?)null);

        var service = new ArticleService(mockRepository.Object);

        // Act
        var result = await service.GetArticleByIdAsync("999");

        // Assert
        Assert.Null(result);
        mockRepository.Verify(repo => repo.GetArticleByIdAsync("999", default), Times.Once);
    }
}
