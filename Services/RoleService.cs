using labsupport.Models;
using Microsoft.EntityFrameworkCore;

namespace labsupport.Services
{
    public class RoleService
    {
        private readonly LabsupportContext _context;

        public RoleService(LabsupportContext context)
        {
            _context = context;
        }

        public async Task<List<Role>> GetAllRolesAsync()
        {
            return await _context.Roles.ToListAsync();
        }
    }
}