using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Desk.Data;

public class DeskDbContext(DbContextOptions<DeskDbContext> options) : IdentityDbContext<DeskUser>(options);
