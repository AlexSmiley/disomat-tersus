using System.Data;
using System.Data.Odbc;
using Disomat.Core;

namespace Disomat.Infrastructure;

public sealed class OdbcWeightRepository(string connectionString) : IWeightRepository
{
    public async Task<bool> SaveAsync(int scaleNumber, decimal tonnes, CancellationToken cancellationToken)
    {
        using var connection = new OdbcConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE VAGI_MERENJA_FTP SET IZMERENO = ?, IND_SOSTOJBA = 2 WHERE BROJ_VAGA = ?";
        command.CommandType = CommandType.Text;
        // ODBC parameters are positional, not named.
        command.Parameters.Add("weight", OdbcType.Decimal).Value = tonnes;
        command.Parameters.Add("scale", OdbcType.Int).Value = scaleNumber;
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
