namespace landingpage.domain.Entities;

public class Article
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedDate { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}
