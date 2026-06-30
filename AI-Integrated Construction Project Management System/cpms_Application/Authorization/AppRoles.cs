namespace cpms_Application.Authorization
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string WarehouseManager = "WarehouseManager";
        public const string ProjectManager = "ProjectManager";
        public const string Customer = "Customer";

        public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Admin,
            WarehouseManager,
            ProjectManager,
            Customer
        };

        public static string Normalize(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return Customer;
            }

            return All.FirstOrDefault(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase)) ?? Customer;
        }

        public static bool IsValid(string? role)
        {
            return !string.IsNullOrWhiteSpace(role) && All.Contains(role);
        }
    }
}
