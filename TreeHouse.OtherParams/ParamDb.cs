using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TreeHouse.OtherParams.Model;

namespace TreeHouse.OtherParams;

public class ParamDb : DbContext
{
    public DbSet<Class> Classes { get; set; }
    public DbSet<Param> Params { get; set; }
    public DbSet<ParamDeclaration> ParamDeclarations { get; set; }
    public DbSet<Table> Tables { get; set; }

    protected DbSet<Globals> GlobalsSet { get; set; }

    public Task<Globals?> GetGlobalsAsync() => GlobalsSet.SingleOrDefaultAsync();

    public async Task SetGlobalsAsync(Globals globals)
    {
        Globals? currentGlobals = await GetGlobalsAsync();
        if (currentGlobals != null)
        {
            globals.Id = currentGlobals.Id;
            Entry(currentGlobals).CurrentValues.SetValues(globals);
        }
        else
        {
            await GlobalsSet.AddAsync(globals);
        }
    }

    public ParamDb(DbContextOptions<ParamDb> options) : base(options)
    {
    }

    public static ParamDb Open(string path, bool write = false, bool log = false)
    {
        string connString = new SqliteConnectionStringBuilder()
        {
            DataSource = path,
            Mode = write ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadOnly
        }.ConnectionString;

        DbContextOptionsBuilder<ParamDb> optBuilder = new();
        optBuilder.UseSqlite(connString);
            
        if (log)
            optBuilder.UseLoggerFactory(LoggerFactory.Create(c => c.AddConsole())).EnableSensitiveDataLogging();

        return new ParamDb(optBuilder.Options);
    }
}
