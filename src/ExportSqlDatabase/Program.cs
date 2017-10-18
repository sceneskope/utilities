using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using SceneSkope.Utilities.CommandLineApplications;
using Serilog;

namespace ExportSqlDatabase
{
    internal class Program : ApplicationBase<Arguments>
    {
        public static void Main(string[] args) => new Program().ApplicationMain(args);

        protected override async Task RunAsync(Arguments arguments, CancellationToken ct)
        {
            var outputDirectory = new DirectoryInfo(arguments.OutputDirectory);
            if (!outputDirectory.Exists)
            {
                outputDirectory.Create();
            }

            using (var connection = new SqlConnection(arguments.SqlConnectionString))
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);

                var tables = (await connection.QueryAsync<string>(
                    new CommandDefinition("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE'", cancellationToken: ct)
                ).ConfigureAwait(false)).AsList();

                Log.Information("Processing {Count} tables", tables.Count);
                foreach (var table in tables)
                {
                    Log.Information("Processing {Table}", table);
                    var rowCount = 0;
                    var outputFileName = $@"{outputDirectory.FullName}\{table}.tsv";
                    using (var reader = await connection.ExecuteReaderAsync(new CommandDefinition($"select * from {table}", cancellationToken: ct)).ConfigureAwait(false) as DbDataReader)
                    using (var writer = File.CreateText(outputFileName))
                    {
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            if (i > 0)
                            {
                                writer.Write("\t");
                            }
                            writer.Write(reader.GetName(i));
                        }
                        await writer.WriteLineAsync().ConfigureAwait(false);

                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            rowCount++;
                            for (var i = 0; i < reader.FieldCount; i++)
                            {
                                if (i > 0)
                                {
                                    writer.Write("\t");
                                }
                                if (!reader.IsDBNull(i))
                                {
                                    var value = reader.GetValue(i);
                                    writer.Write(value);
                                }
                            }
                            await writer.WriteLineAsync().ConfigureAwait(false);
                        }
                    }
                    Log.Information("Written {RowCount} rows", rowCount);
                }
            }
        }
    }
}