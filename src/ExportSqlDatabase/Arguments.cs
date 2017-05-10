using CommandLineParser.Arguments;
using SceneSkope.Utilities.CommandLineApplications;

namespace ExportSqlDatabase
{
    internal class Arguments : ArgumentsBase
    {
        [SwitchArgument('c', "noconsole", false, Description = "No console", Optional = true)]
        public override bool NoConsole { get; set; }

        [ValueArgument(typeof(string), 'd', "outputdirectory", Description = "Output directory", Optional = false)]
        public string OutputDirectory { get; set; }

        [ValueArgument(typeof(string), 'e', "sqlconnection", Description = "Sql connection string", Optional = false)]
        public string SqlConnectionString { get; set; }

        [SwitchArgument('h', "help", false, Description = "Show usage", Optional = true)]
        public override bool Help { get; set; }

        [ValueArgument(typeof(string), 'k', "key", Description = "Application insights key", Optional = true)]
        public override string Key { get; set; }

        [ValueArgument(typeof(string), 'l', "lockfile", Description = "Lock file", Optional = true)]
        public override string LockFile { get; set; }

        [ValueArgument(typeof(string), 'o', "seqtoken", Description = "Seq token", Optional = true)]
        public override string SeqToken { get; set; }

        [ValueArgument(typeof(string), 'q', "seqhost", Description = "Seq server host", Optional = true)]
        public override string SeqHost { get; set; }
    }
}
