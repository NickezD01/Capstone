namespace cpms_Application.Request.WorkCategory;

public sealed class CreateWorkCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class UpdateWorkCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
