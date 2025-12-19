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
                Id = $"01J8Z9A0B1C2D3E4F5G6H7Z8J{i}",
                Texts = new List<ArticleText>
                {
                    new ArticleText
                    {
                        Language = "es-ES",
                        Title = $"Desarrollo impulsado por IA: Artículo {i}",
                        Excerpt = $"Este es un extracto del artículo {i}. Aprende sobre las últimas tendencias en desarrollo de software impulsado por IA, edge computing y patrones de arquitectura moderna."
                    },
                    new ArticleText
                    {
                        Language = "en-US",
                        Title = $"AI-Driven Development: Article {i}",
                        Excerpt = $"This is an excerpt for article {i}. Learn about the latest trends in AI-driven software development, edge computing, and modern architecture patterns."
                    }
                },
                PublishedDate = DateTime.UtcNow.AddDays(-i),
                Url = $"https://www.linkedin.com/in/jmrpineda/article-{i}",
                Image = $"./img/article-{i}.png"
            });
        }

        return articles;
    }
}
