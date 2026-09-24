namespace cpms_Application.Response.WorkCategory;

public sealed class WorkCategoryResponse
{
    public int WorkCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
