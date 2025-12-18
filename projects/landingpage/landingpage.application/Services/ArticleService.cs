using landingpage.domain.Entities;
using landingpage.domain.Interfaces;
using landingpage.domain.ValueObjects;

namespace landingpage.application.Services;

public class ArticleService
{
    private readonly IArticleRepository _articleRepository;

    public ArticleService(IArticleRepository articleRepository)
    {
        _articleRepository = articleRepository;
    }

    public async Task<PagedResult<Article>> GetArticlesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
            pageNumber = 1;

        if (pageSize < 1)
            pageSize = 10;

        return await _articleRepository.GetArticlesAsync(pageNumber, pageSize, cancellationToken);
    }

    public async Task<Article?> GetArticleByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _articleRepository.GetArticleByIdAsync(id, cancellationToken);
    }
}
