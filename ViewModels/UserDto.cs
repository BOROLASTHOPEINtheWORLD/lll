namespace labsupport.ViewModels
{
    public class CreateUserDto
    {
        public string Username { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string? MiddleName { get; set; }
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public short RoleId { get; set; }
        public short? DepartmentId { get; set; }
        public short? PositionId { get; set; }
        public bool IsActive { get; set; } = true;
        public string? AvatarPath { get; set; }
    }

    public class UpdateUserDto
    {
        public string Username { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string FirstName { get; set; } = null!;
        public string? MiddleName { get; set; }
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public short RoleId { get; set; }
        public short? DepartmentId { get; set; }
        public short? PositionId { get; set; }
        public bool IsActive { get; set; }
        public string? AvatarPath { get; set; }
    }
}
