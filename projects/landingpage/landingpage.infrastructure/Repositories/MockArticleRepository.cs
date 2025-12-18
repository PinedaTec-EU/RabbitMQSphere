using landingpage.domain.Entities;
using landingpage.domain.Interfaces;
using landingpage.domain.ValueObjects;

namespace landingpage.infrastructure.Repositories;

/// <summary>
/// Mock implementation of IArticleRepository for demonstration purposes.
/// In production, this would connect to an actual API endpoint.
/// </summary>
public class MockArticleRepository : IArticleRepository
{
    private readonly List<Article> _articles;

    public MockArticleRepository()
    {
        _articles = GenerateMockArticles();
    }

    public Task<PagedResult<Article>> GetArticlesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalItems = _articles.Count;
        var items = _articles
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PagedResult<Article>
        {
            Items = items,
            CurrentPage = pageNumber,
            PageSize = pageSize,
            TotalItems = totalItems
        };

        return Task.FromResult(result);
    }

    public Task<Article?> GetArticleByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var article = _articles.FirstOrDefault(a => a.Id == id);
        return Task.FromResult(article);
    }

    private List<Article> GenerateMockArticles()
    {
        var articles = new List<Article>();

        for (int i = 1; i <= 25; i++)
        {
            articles.Add(new Article
            {
                Id = $"article-{i}",
                Title = $"AI-Driven Development: Article {i}",
                Excerpt = $"This is an excerpt for article {i}. Learn about the latest trends in AI-driven software development, edge computing, and modern architecture patterns.",
                Content = $"Full content for article {i}. Deep dive into AI technologies, machine learning operations, and how they're transforming the software development landscape.",
                PublishedDate = DateTime.UtcNow.AddDays(-i),
                Author = "José Manuel Redondo Pineda",
                Url = $"https://www.linkedin.com/in/jmrpineda/article-{i}",
                Tags = new List<string> { "AI", "Development", "Innovation", "Edge Computing" }
            });
        }

        return articles;
    }
}
