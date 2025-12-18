using landingpage.domain.Entities;
using landingpage.domain.ValueObjects;

namespace landingpage.domain.Interfaces;

public interface IArticleRepository
{
    Task<PagedResult<Article>> GetArticlesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<Article?> GetArticleByIdAsync(string id, CancellationToken cancellationToken = default);
}
