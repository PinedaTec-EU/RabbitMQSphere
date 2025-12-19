namespace landingpage.domain.Entities;

public class Article
{
    public string Id { get; set; } = string.Empty;
    public List<ArticleText> Texts { get; set; } = new();
    public DateTime PublishedDate { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    
    // Helper methods to get text in specific language
    public string GetTitle(string language)
    {
        var text = Texts.FirstOrDefault(t => t.Language == language);
        if (text != null)
            return text.Title;
        
        var fallback = Texts.FirstOrDefault();
        return fallback?.Title ?? string.Empty;
    }
    
    public string GetExcerpt(string language)
    {
        var text = Texts.FirstOrDefault(t => t.Language == language);
        if (text != null)
            return text.Excerpt;
        
        var fallback = Texts.FirstOrDefault();
        return fallback?.Excerpt ?? string.Empty;
    }
}

public class ArticleText
{
    public string Language { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
}
